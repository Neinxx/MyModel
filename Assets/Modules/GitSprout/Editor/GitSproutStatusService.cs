using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace GitSprout
{
    [InitializeOnLoad]
    internal static class GitSproutStatusService
    {
        private static readonly Dictionary<string, GitSproutChange> Changes = new Dictionary<string, GitSproutChange>(StringComparer.Ordinal);
        private static readonly Dictionary<string, GitSproutState> AssetVisualStates = new Dictionary<string, GitSproutState>(StringComparer.Ordinal);
        private static readonly Dictionary<string, GitSproutState> FolderVisualStates = new Dictionary<string, GitSproutState>(StringComparer.Ordinal);

        private static CancellationTokenSource refreshTokenSource;
        private static double nextAllowedRefresh;
        private static bool refreshQueued;

        public static bool IsRefreshing { get; private set; }
        public static string LastError { get; private set; }
        public static int ChangeCount { get; private set; }
        public static string BranchSummary { get; private set; } = string.Empty;
        public static bool HasChanges
        {
            get { return ChangeCount > 0; }
        }

        static GitSproutStatusService()
        {
            EditorApplication.projectChanged += QueueRefresh;
            EditorApplication.focusChanged += hasFocus =>
            {
                if (hasFocus)
                    QueueRefresh();
            };
            EditorApplication.update += OnEditorUpdate;
            QueueRefresh();
        }

        public static void QueueRefresh()
        {
            refreshQueued = true;
        }

        public static void RefreshNow()
        {
            refreshQueued = false;
            nextAllowedRefresh = EditorApplication.timeSinceStartup + 0.25;
            _ = RefreshAsync();
        }

        public static GitSproutState GetVisualState(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return GitSproutState.Clean;

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                var folderState = GitSproutState.Clean;
                if (AssetVisualStates.TryGetValue(assetPath, out var ownState))
                    folderState = GitSproutVisuals.Max(folderState, ownState);
                if (FolderVisualStates.TryGetValue(assetPath, out var aggregatedState))
                    folderState = GitSproutVisuals.Max(folderState, aggregatedState);
                return folderState;
            }

            if (AssetVisualStates.TryGetValue(assetPath, out var state))
                return state;

            return GitSproutState.Clean;
        }

        public static string GetTooltip(string assetPath)
        {
            var state = GetVisualState(assetPath);
            if (state == GitSproutState.Clean)
                return string.Empty;

            var label = GitSproutVisuals.LabelFor(state);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                var count = Changes.Keys.Count(path => IsUnderFolder(path, assetPath));
                return count > 0 ? label + " inside (" + count + " files)" : label;
            }

            var hasAsset = Changes.ContainsKey(assetPath);
            var hasMeta = Changes.ContainsKey(assetPath + ".meta");
            if (hasAsset && hasMeta)
                return label + " - asset + meta";
            if (hasMeta)
                return label + " - meta";
            return label;
        }

        public static IReadOnlyList<GitSproutChange> GetChangesForSelection()
        {
            var selectedPaths = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (selectedPaths.Length == 0)
                return Array.Empty<GitSproutChange>();

            var selected = new Dictionary<string, GitSproutChange>(StringComparer.Ordinal);
            foreach (var path in selectedPaths)
                AddChangesForAssetPath(path, selected);

            return selected.Values
                .OrderBy(change => change.Path, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<GitSproutChange> GetAllProjectChanges()
        {
            return Changes.Values
                .Where(change => change.Path.StartsWith("Assets/", StringComparison.Ordinal) || change.Path == "Assets")
                .OrderBy(change => change.Path, StringComparer.Ordinal)
                .ToArray();
        }

        public static void MarkPathsClean(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                Changes.Remove(path);
            RebuildVisualCaches();
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnEditorUpdate()
        {
            if (!refreshQueued || IsRefreshing)
                return;

            if (EditorApplication.timeSinceStartup < nextAllowedRefresh)
                return;

            refreshQueued = false;
            nextAllowedRefresh = EditorApplication.timeSinceStartup + 3.0;
            _ = RefreshAsync();
        }

        private static async System.Threading.Tasks.Task RefreshAsync()
        {
            refreshTokenSource?.Cancel();
            refreshTokenSource = new CancellationTokenSource();
            var token = refreshTokenSource.Token;

            IsRefreshing = true;
            LastError = string.Empty;

            try
            {
                var result = await GitSproutGit.RunAsync(new[] { "status", "--porcelain=v1", "-z", "--branch", "-uall" }, token);
                if (token.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    LastError = result.Error.Trim();
                    return;
                }

                Changes.Clear();
                BranchSummary = string.Empty;
                foreach (var change in ParsePorcelain(result.Output))
                    Changes[change.Path] = change;

                ChangeCount = Changes.Count;
                RebuildVisualCaches();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                IsRefreshing = false;
                EditorApplication.RepaintProjectWindow();
            }
        }

        private static IEnumerable<GitSproutChange> ParsePorcelain(string output)
        {
            var entries = output.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Length < 4)
                    continue;

                var rawStatus = entry.Substring(0, 2);
                if (rawStatus == "##")
                {
                    BranchSummary = entry.Substring(3);
                    continue;
                }

                var path = entry.Substring(3).Replace('\\', '/');
                var state = ParseState(rawStatus);

                if (path.StartsWith("Assets/", StringComparison.Ordinal) || path == "Assets")
                    yield return new GitSproutChange(path, state, rawStatus);

                if (rawStatus[0] == 'R' || rawStatus[0] == 'C')
                    i++;
            }
        }

        private static GitSproutState ParseState(string rawStatus)
        {
            if (rawStatus.Length < 2)
                return GitSproutState.Modified;

            var x = rawStatus[0];
            var y = rawStatus[1];

            if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D'))
                return GitSproutState.Conflict;
            if (x == '?' && y == '?')
                return GitSproutState.Untracked;
            if (x == 'A' || y == 'A')
                return GitSproutState.Added;
            if (x == 'D' || y == 'D')
                return GitSproutState.Deleted;
            if (x == 'M' || y == 'M' || x == 'R' || y == 'R' || x == 'C' || y == 'C')
                return GitSproutState.Modified;

            return GitSproutState.Modified;
        }

        private static void AddChangesForAssetPath(string assetPath, Dictionary<string, GitSproutChange> selected)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                foreach (var pair in Changes)
                {
                    if (IsUnderFolder(pair.Key, assetPath))
                        selected[pair.Key] = pair.Value;
                }

                AddIfChanged(assetPath + ".meta", selected);
                return;
            }

            AddIfChanged(assetPath, selected);
            AddIfChanged(assetPath + ".meta", selected);
        }

        private static void AddIfChanged(string path, Dictionary<string, GitSproutChange> selected)
        {
            if (Changes.TryGetValue(path, out var change))
                selected[path] = change;
        }

        private static void RebuildVisualCaches()
        {
            AssetVisualStates.Clear();
            FolderVisualStates.Clear();

            foreach (var change in Changes.Values)
            {
                var path = change.Path;
                var isMeta = path.EndsWith(".meta", StringComparison.Ordinal);
                var assetPath = isMeta ? path.Substring(0, path.Length - ".meta".Length) : path;

                if (!AssetVisualStates.TryGetValue(assetPath, out var current))
                    current = GitSproutState.Clean;
                AssetVisualStates[assetPath] = GitSproutVisuals.Max(current, change.State);

                AddFolderAggregates(assetPath, change.State);
            }
        }

        private static void AddFolderAggregates(string path, GitSproutState state)
        {
            var slash = path.LastIndexOf('/');
            while (slash > 0)
            {
                var folder = path.Substring(0, slash);
                if (!FolderVisualStates.TryGetValue(folder, out var current))
                    current = GitSproutState.Clean;
                FolderVisualStates[folder] = GitSproutVisuals.Max(current, state);
                slash = folder.LastIndexOf('/');
            }
        }

        private static bool IsUnderFolder(string path, string folder)
        {
            return path.StartsWith(folder + "/", StringComparison.Ordinal) || path == folder || path == folder + ".meta";
        }
    }
}
