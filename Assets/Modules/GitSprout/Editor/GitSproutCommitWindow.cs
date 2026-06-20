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
            root.style.paddingLeft = 18;
            root.style.paddingRight = 18;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 14;
            root.style.backgroundColor = SurfaceColor();

            var header = new VisualElement();
            header.style.borderLeftWidth = 3;
            header.style.borderLeftColor = AccentColor();
            header.style.paddingLeft = 10;
            header.style.marginBottom = 14;

            var title = new Label(windowTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.color = PrimaryTextColor();
            header.Add(title);

            var subtitle = new Label(BuildSubtitle());
            subtitle.style.marginTop = 3;
            subtitle.style.color = MutedTextColor();
            header.Add(subtitle);
            root.Add(header);

            var messageLabel = CreateSectionLabel("MESSAGE");
            root.Add(messageLabel);

            messageField = new TextField { multiline = true };
            messageField.style.marginTop = 6;
            messageField.style.height = 78;
            messageField.style.marginBottom = 10;
            messageField.RegisterValueChangedCallback(_ => UpdateCommitButton());
            root.Add(messageField);

            var warning = BuildWarning();
            if (!string.IsNullOrEmpty(warning))
            {
                var warningLabel = new Label(warning);
                warningLabel.style.color = new Color(0.95f, 0.62f, 0.22f);
                warningLabel.style.whiteSpace = WhiteSpace.Normal;
                warningLabel.style.marginBottom = 8;
                root.Add(warningLabel);
            }

            root.Add(CreateSectionLabel(changes.Count + " FILES"));

            var scroll = new ScrollView();
            scroll.style.marginTop = 6;
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 120;
            scroll.style.backgroundColor = InputColor();
            scroll.style.borderTopWidth = 1;
            scroll.style.borderBottomWidth = 1;
            scroll.style.borderLeftWidth = 1;
            scroll.style.borderRightWidth = 1;
            scroll.style.borderTopColor = BorderColor();
            scroll.style.borderBottomColor = BorderColor();
            scroll.style.borderLeftColor = BorderColor();
            scroll.style.borderRightColor = BorderColor();

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
            StyleButton(cancelButton, false);
            cancelButton.style.marginRight = 8;
            buttons.Add(cancelButton);

            commitButton = new Button(() => _ = CommitAsync()) { text = "Commit" };
            StyleButton(commitButton, true);
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
            row.style.minHeight = 26;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;

            var glyph = new Label(GitSproutVisuals.GlyphFor(change.State));
            glyph.style.width = 18;
            glyph.style.marginRight = 8;
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.color = GitSproutVisuals.ColorFor(change.State);
            row.Add(glyph);

            var path = new Label(change.Path);
            path.style.flexGrow = 1;
            path.style.flexShrink = 1;
            path.style.unityTextAlign = TextAnchor.MiddleLeft;
            path.style.color = PrimaryTextColor();
            row.Add(path);

            var state = new Label(GitSproutVisuals.LabelFor(change.State));
            state.style.width = 76;
            state.style.marginLeft = 8;
            state.style.unityTextAlign = TextAnchor.MiddleRight;
            state.style.color = MutedTextColor();
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
            var addArguments = new List<string> { "add", "-A", "--" };
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
            var shouldPush = EditorUtility.DisplayDialog("Git Sprout", "Committed. Push now?", "Push", "Later");
            Close();
            if (shouldPush)
                GitSproutOperations.PushConfirmed();
        }

        private void OnDisable()
        {
            commitTokenSource?.Cancel();
            commitTokenSource?.Dispose();
            commitTokenSource = null;
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

        private static Label CreateSectionLabel(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 10;
            label.style.color = AccentColor();
            label.style.marginTop = 6;
            return label;
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.style.minWidth = 88;
            button.style.height = 24;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;

            if (!primary)
                return;

            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private static Color SurfaceColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.13f, 0.13f)
                : new Color(0.93f, 0.93f, 0.93f);
        }

        private static Color InputColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.17f, 0.17f, 0.17f)
                : new Color(0.86f, 0.86f, 0.86f);
        }

        private static Color PrimaryTextColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.82f, 0.82f, 0.82f)
                : new Color(0.18f, 0.18f, 0.18f);
        }

        private static Color MutedTextColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.62f, 0.62f, 0.62f)
                : new Color(0.36f, 0.36f, 0.36f);
        }

        private static Color AccentColor()
        {
            return new Color(0.49f, 0.55f, 1f);
        }
    }
}
