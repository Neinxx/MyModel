using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GitSprout
{
    internal sealed class GitSproutCommitWindow : EditorWindow
    {
        private const long LargeFileBytes = 10 * 1024 * 1024;

        private IReadOnlyList<GitSproutChange> changes = Array.Empty<GitSproutChange>();
        private string windowTitle = "Commit Selected";
        private TextField messageField;
        private Button commitButton;
        private Label statusLabel;
        private CancellationTokenSource commitTokenSource;

        public static void Open(IReadOnlyList<GitSproutChange> changes, string title)
        {
            var window = CreateInstance<GitSproutCommitWindow>();
            window.titleContent = new GUIContent("Git Sprout");
            window.changes = changes ?? Array.Empty<GitSproutChange>();
            window.windowTitle = string.IsNullOrEmpty(title) ? "Commit Selected" : title;
            window.minSize = new Vector2(420f, 300f);
            window.maxSize = new Vector2(620f, 640f);
            window.ShowUtility();
            window.position = Centered(520f, 460f);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.13f, 0.13f)
                : new Color(0.93f, 0.93f, 0.93f);

            var title = new Label(windowTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            root.Add(title);

            var subtitle = new Label(BuildSubtitle());
            subtitle.style.marginTop = 4;
            subtitle.style.opacity = 0.72f;
            root.Add(subtitle);

            messageField = new TextField { multiline = true };
            messageField.label = "Message";
            messageField.style.marginTop = 14;
            messageField.style.height = 72;
            messageField.RegisterValueChangedCallback(_ => UpdateCommitButton());
            root.Add(messageField);

            var warning = BuildWarning();
            if (!string.IsNullOrEmpty(warning))
            {
                var warningLabel = new Label(warning);
                warningLabel.style.marginTop = 8;
                warningLabel.style.color = new Color(0.95f, 0.62f, 0.22f);
                warningLabel.style.whiteSpace = WhiteSpace.Normal;
                root.Add(warningLabel);
            }

            var listTitle = new Label(changes.Count + " files");
            listTitle.style.marginTop = 14;
            listTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(listTitle);

            var scroll = new ScrollView();
            scroll.style.marginTop = 6;
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 120;
            scroll.style.borderTopWidth = 1;
            scroll.style.borderBottomWidth = 1;
            scroll.style.borderTopColor = BorderColor();
            scroll.style.borderBottomColor = BorderColor();

            foreach (var change in changes)
                scroll.Add(BuildChangeRow(change));

            root.Add(scroll);

            statusLabel = new Label(string.Empty);
            statusLabel.style.marginTop = 8;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(statusLabel);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 12;

            var cancelButton = new Button(Close) { text = "Cancel" };
            cancelButton.style.marginRight = 8;
            buttons.Add(cancelButton);

            commitButton = new Button(() => _ = CommitAsync()) { text = "Commit" };
            buttons.Add(commitButton);
            root.Add(buttons);

            UpdateCommitButton();
            messageField.Focus();
        }

        private VisualElement BuildChangeRow(GitSproutChange change)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 22;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.marginRight = 8;
            dot.style.backgroundColor = GitSproutVisuals.ColorFor(change.State);
            row.Add(dot);

            var path = new Label(change.Path);
            path.style.flexGrow = 1;
            path.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(path);

            var state = new Label(GitSproutVisuals.LabelFor(change.State));
            state.style.opacity = 0.62f;
            state.style.marginLeft = 8;
            row.Add(state);

            return row;
        }

        private string BuildSubtitle()
        {
            if (changes.Count == 0)
                return "Nothing changed. Suspiciously peaceful.";

            var metaCount = changes.Count(change => change.Path.EndsWith(".meta", StringComparison.Ordinal));
            return metaCount > 0
                ? changes.Count + " files, including " + metaCount + " Unity meta files"
                : changes.Count + " files";
        }

        private string BuildWarning()
        {
            if (changes.Any(change => change.State == GitSproutState.Conflict))
                return "Conflicts detected. Resolve them before committing.";

            var largeFiles = changes.Where(IsLargeFile).Take(3).Select(change => Path.GetFileName(change.Path)).ToArray();
            if (largeFiles.Length == 0)
                return string.Empty;

            return "Large file check: " + string.Join(", ", largeFiles) + ". Commit carefully.";
        }

        private bool IsLargeFile(GitSproutChange change)
        {
            if (change.State == GitSproutState.Deleted)
                return false;

            var fullPath = Path.Combine(GitSproutGit.ProjectRoot, change.Path);
            return File.Exists(fullPath) && new FileInfo(fullPath).Length >= LargeFileBytes;
        }

        private void UpdateCommitButton()
        {
            if (commitButton == null)
                return;

            var hasMessage = messageField != null && !string.IsNullOrWhiteSpace(messageField.value);
            var hasFiles = changes.Count > 0;
            var hasConflict = changes.Any(change => change.State == GitSproutState.Conflict);
            commitButton.SetEnabled(hasMessage && hasFiles && !hasConflict);
        }

        private async System.Threading.Tasks.Task CommitAsync()
        {
            commitButton.SetEnabled(false);
            statusLabel.text = "Staging selected files...";
            statusLabel.style.color = LabelColor();

            commitTokenSource?.Cancel();
            commitTokenSource = new CancellationTokenSource();
            var token = commitTokenSource.Token;

            var paths = changes.Select(change => change.Path).Distinct(StringComparer.Ordinal).ToArray();
            var addArguments = new List<string> { "add", "--" };
            addArguments.AddRange(paths);

            var addResult = await GitSproutGit.RunAsync(addArguments, token);
            if (!addResult.Success)
            {
                ShowGitError("Could not stage files", addResult);
                UpdateCommitButton();
                return;
            }

            statusLabel.text = "Committing...";
            var commitResult = await GitSproutGit.RunAsync(new[] { "commit", "-m", messageField.value.Trim() }, token);
            if (!commitResult.Success)
            {
                ShowGitError("Commit failed", commitResult);
                UpdateCommitButton();
                return;
            }

            GitSproutStatusService.MarkPathsClean(paths);
            GitSproutStatusService.QueueRefresh();
            Close();
        }

        private void ShowGitError(string title, GitSproutCommandResult result)
        {
            var error = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            statusLabel.text = title + ": " + error.Trim();
            statusLabel.style.color = new Color(1f, 0.31f, 0.25f);
        }

        private static Rect Centered(float width, float height)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
        }

        private static Color BorderColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.12f)
                : new Color(0f, 0f, 0f, 0.12f);
        }

        private static Color LabelColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.15f, 0.15f, 0.15f);
        }
    }
}
