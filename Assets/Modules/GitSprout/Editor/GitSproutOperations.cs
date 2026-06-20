using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;

namespace GitSprout
{
    internal static class GitSproutOperations
    {
        private static CancellationTokenSource operationTokenSource;

        public static void Fetch()
        {
            if (!EditorUtility.DisplayDialog("Git Sprout", "Fetch remote changes?", "Fetch", "Cancel"))
                return;

            RunOperation("Fetch", new[] { "fetch", "--prune" });
        }

        public static void PullFastForward()
        {
            if (!EditorUtility.DisplayDialog("Git Sprout", "Pull remote changes using fast-forward only?", "Pull", "Cancel"))
                return;

            PullFastForwardAsync();
        }

        public static void Push()
        {
            if (!EditorUtility.DisplayDialog("Git Sprout", "Push committed changes to the current upstream?", "Push", "Cancel"))
                return;

            PushConfirmed();
        }

        public static void PushConfirmed()
        {
            RunOperation("Push", new[] { "push" });
        }

        public static void RevertSelected(IReadOnlyList<GitSproutChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                EditorUtility.DisplayDialog("Git Sprout", "No selected changes to revert.", "OK");
                return;
            }

            var revertible = changes
                .Where(change => change.State == GitSproutState.Modified || change.State == GitSproutState.Deleted)
                .OrderBy(change => change.Path, StringComparer.Ordinal)
                .ToArray();

            var skipped = changes
                .Where(change => change.State != GitSproutState.Modified && change.State != GitSproutState.Deleted)
                .OrderBy(change => change.Path, StringComparer.Ordinal)
                .ToArray();

            if (revertible.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Git Sprout - Revert",
                    "No safely revertible selected files.\n\nAdded, untracked, and conflicted files are intentionally skipped.",
                    "OK");
                return;
            }

            var message = BuildRevertMessage(revertible, skipped);
            if (!EditorUtility.DisplayDialog("Git Sprout - Revert Selected", message, "Revert", "Cancel"))
                return;

            RevertSelectedAsync(revertible, skipped.Length);
        }

        private static async void RunOperation(string title, string[] arguments)
        {
            operationTokenSource?.Cancel();
            operationTokenSource = new CancellationTokenSource();

            try
            {
                EditorUtility.DisplayProgressBar("Git Sprout", title + "...", 0.5f);
                var result = await GitSproutGit.RunAsync(arguments, operationTokenSource.Token);
                EditorUtility.ClearProgressBar();

                GitSproutStatusService.RefreshNow();

                var message = BuildMessage(result);
                EditorUtility.DisplayDialog("Git Sprout - " + title, message, "OK");
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async void PullFastForwardAsync()
        {
            operationTokenSource?.Cancel();
            operationTokenSource = new CancellationTokenSource();

            try
            {
                EditorUtility.DisplayProgressBar("Git Sprout", "Checking working tree...", 0.25f);
                var statusResult = await GitSproutGit.RunAsync(new[] { "status", "--porcelain=v1", "-uall" }, operationTokenSource.Token);
                if (!statusResult.Success)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Git Sprout - Pull", BuildMessage(statusResult), "OK");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(statusResult.Output))
                {
                    EditorUtility.ClearProgressBar();
                    GitSproutStatusService.RefreshNow();
                    EditorUtility.DisplayDialog("Git Sprout", "Pull needs a clean working tree. Commit your changes first. Tiny seatbelt, big consequences.", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Git Sprout", "Pulling...", 0.65f);
                var pullResult = await GitSproutGit.RunAsync(new[] { "pull", "--ff-only" }, operationTokenSource.Token);
                EditorUtility.ClearProgressBar();

                GitSproutStatusService.RefreshNow();
                EditorUtility.DisplayDialog("Git Sprout - Pull", BuildMessage(pullResult), "OK");
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async void RevertSelectedAsync(IReadOnlyList<GitSproutChange> changes, int skippedCount)
        {
            operationTokenSource?.Cancel();
            operationTokenSource = new CancellationTokenSource();

            try
            {
                var arguments = new List<string> { "restore", "--worktree", "--staged", "--" };
                arguments.AddRange(changes.Select(change => change.Path));

                EditorUtility.DisplayProgressBar("Git Sprout", "Reverting selected files...", 0.5f);
                var result = await GitSproutGit.RunAsync(arguments, operationTokenSource.Token);
                EditorUtility.ClearProgressBar();

                GitSproutStatusService.QueueRefresh();
                AssetDatabase.Refresh();

                var message = BuildMessage(result);
                if (skippedCount > 0)
                    message += "\n\nSkipped " + skippedCount + " added, untracked, or conflicted file(s).";

                EditorUtility.DisplayDialog("Git Sprout - Revert", message, "OK");
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string BuildMessage(GitSproutCommandResult result)
        {
            var output = string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output;
            output = string.IsNullOrWhiteSpace(output) ? "Done." : output.Trim();

            if (result.Success)
                return output;

            return "Git failed:\n\n" + output;
        }

        private static string BuildRevertMessage(IReadOnlyList<GitSproutChange> revertible, IReadOnlyList<GitSproutChange> skipped)
        {
            var message = "Revert " + revertible.Count + " selected file(s)?\n\n"
                + "This discards local edits for tracked files. Unity .meta files are included when selected.\n\n"
                + BuildPathPreview(revertible);

            if (skipped.Count > 0)
                message += "\n\nSkipped for safety: " + skipped.Count + " added, untracked, or conflicted file(s).";

            return message;
        }

        private static string BuildPathPreview(IReadOnlyList<GitSproutChange> changes)
        {
            const int maxLines = 10;
            var lines = changes.Take(maxLines).Select(change => change.Path).ToArray();
            var preview = string.Join("\n", lines);
            if (changes.Count > maxLines)
                preview += "\n...and " + (changes.Count - maxLines) + " more";
            return preview;
        }
    }
}
