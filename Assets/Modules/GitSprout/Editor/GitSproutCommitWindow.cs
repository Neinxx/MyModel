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
        private readonly HashSet<string> selectedPaths = new HashSet<string>();
        private string windowTitle = "Commit Selected";
        private Label subtitleLabel;
        private Label filesLabel;
        private Toggle selectAllToggle;
        private VisualElement warningContainer;
        private ScrollView scroll;
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
            selectedPaths.Clear();
            foreach (var change in changes)
            {
                selectedPaths.Add(change.Path);
            }

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

            subtitleLabel = new Label(string.Empty);
            subtitleLabel.style.marginTop = 3;
            subtitleLabel.style.color = MutedTextColor();
            header.Add(subtitleLabel);
            root.Add(header);

            var messageLabel = CreateSectionLabel("MESSAGE");
            root.Add(messageLabel);

            messageField = new TextField { multiline = true };
            messageField.style.marginTop = 6;
            messageField.style.height = 78;
            messageField.style.marginBottom = 10;
            messageField.RegisterValueChangedCallback(_ => UpdateCommitButton());

            var inputElement = messageField.Q("unity-text-input");
            if (inputElement != null)
            {
                inputElement.style.backgroundColor = InputColor();
                inputElement.style.borderTopWidth = 1;
                inputElement.style.borderBottomWidth = 1;
                inputElement.style.borderLeftWidth = 1;
                inputElement.style.borderRightWidth = 1;
                inputElement.style.borderTopColor = BorderColor();
                inputElement.style.borderBottomColor = BorderColor();
                inputElement.style.borderLeftColor = BorderColor();
                inputElement.style.borderRightColor = BorderColor();
                inputElement.style.borderTopLeftRadius = 4;
                inputElement.style.borderTopRightRadius = 4;
                inputElement.style.borderBottomLeftRadius = 4;
                inputElement.style.borderBottomRightRadius = 4;
                inputElement.style.paddingLeft = 6;
                inputElement.style.paddingRight = 6;
                inputElement.style.paddingTop = 6;
                inputElement.style.paddingBottom = 6;
                inputElement.style.flexGrow = 1;
                inputElement.style.whiteSpace = WhiteSpace.Normal;
            }
            root.Add(messageField);

            warningContainer = new VisualElement();
            warningContainer.style.marginBottom = 10;
            warningContainer.style.display = DisplayStyle.None;
            root.Add(warningContainer);

            var filesHeader = new VisualElement();
            filesHeader.style.flexDirection = FlexDirection.Row;
            filesHeader.style.justifyContent = Justify.SpaceBetween;
            filesHeader.style.alignItems = Align.Center;
            filesHeader.style.marginTop = 6;

            filesLabel = CreateSectionLabel("FILES");
            filesLabel.style.marginTop = 0;
            filesHeader.Add(filesLabel);

            selectAllToggle = new Toggle("Select All");
            selectAllToggle.style.fontSize = 10;
            selectAllToggle.style.unityFontStyleAndWeight = FontStyle.Bold;
            selectAllToggle.style.color = MutedTextColor();
            selectAllToggle.style.marginLeft = 0;
            selectAllToggle.style.marginRight = 0;
            selectAllToggle.style.marginTop = 0;
            selectAllToggle.style.marginBottom = 0;
            selectAllToggle.RegisterValueChangedCallback(evt => SetAllSelected(evt.newValue));
            filesHeader.Add(selectAllToggle);

            root.Add(filesHeader);

            scroll = new ScrollView();
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
            scroll.style.borderTopLeftRadius = 4;
            scroll.style.borderTopRightRadius = 4;
            scroll.style.borderBottomLeftRadius = 4;
            scroll.style.borderBottomRightRadius = 4;
            root.Add(scroll);

            statusLabel = new Label(string.Empty);
            statusLabel.style.marginTop = 8;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.display = DisplayStyle.None;
            root.Add(statusLabel);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 12;

            var cancelButton = new Button(Close) { text = "Cancel" };
            StyleButton(cancelButton, false);
            cancelButton.style.marginRight = 8;

            cancelButton.style.backgroundColor = EditorGUIUtility.isProSkin 
                ? new Color(0.22f, 0.22f, 0.22f) 
                : new Color(0.88f, 0.88f, 0.88f);
            cancelButton.style.color = PrimaryTextColor();
            cancelButton.style.borderTopWidth = 1;
            cancelButton.style.borderBottomWidth = 1;
            cancelButton.style.borderLeftWidth = 1;
            cancelButton.style.borderRightWidth = 1;
            cancelButton.style.borderTopColor = BorderColor();
            cancelButton.style.borderBottomColor = BorderColor();
            cancelButton.style.borderLeftColor = BorderColor();
            cancelButton.style.borderRightColor = BorderColor();
            cancelButton.style.borderTopLeftRadius = 4;
            cancelButton.style.borderTopRightRadius = 4;
            cancelButton.style.borderBottomLeftRadius = 4;
            cancelButton.style.borderBottomRightRadius = 4;

            cancelButton.RegisterCallback<MouseOverEvent>(_ => {
                cancelButton.style.backgroundColor = EditorGUIUtility.isProSkin 
                    ? new Color(0.28f, 0.28f, 0.28f) 
                    : new Color(0.82f, 0.82f, 0.82f);
            });
            cancelButton.RegisterCallback<MouseOutEvent>(_ => {
                cancelButton.style.backgroundColor = EditorGUIUtility.isProSkin 
                    ? new Color(0.22f, 0.22f, 0.22f) 
                    : new Color(0.88f, 0.88f, 0.88f);
            });

            buttons.Add(cancelButton);

            commitButton = new Button(() => _ = CommitAsync()) { text = "Commit" };
            StyleButton(commitButton, true);

            commitButton.style.borderTopWidth = 1;
            commitButton.style.borderBottomWidth = 1;
            commitButton.style.borderLeftWidth = 1;
            commitButton.style.borderRightWidth = 1;
            commitButton.style.borderTopColor = Color.clear;
            commitButton.style.borderBottomColor = Color.clear;
            commitButton.style.borderLeftColor = Color.clear;
            commitButton.style.borderRightColor = Color.clear;
            commitButton.style.borderTopLeftRadius = 4;
            commitButton.style.borderTopRightRadius = 4;
            commitButton.style.borderBottomLeftRadius = 4;
            commitButton.style.borderBottomRightRadius = 4;

            commitButton.RegisterCallback<MouseOverEvent>(_ => {
                if (commitButton.enabledSelf)
                {
                    commitButton.style.backgroundColor = new Color(0.55f, 0.60f, 1f);
                }
            });
            commitButton.RegisterCallback<MouseOutEvent>(_ => {
                if (commitButton.enabledSelf)
                {
                    commitButton.style.backgroundColor = AccentColor();
                }
            });

            buttons.Add(commitButton);
            root.Add(buttons);

            RefreshUI();
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
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            row.RegisterCallback<MouseOverEvent>(_ => {
                row.style.backgroundColor = EditorGUIUtility.isProSkin 
                    ? new Color(1f, 1f, 1f, 0.05f) 
                    : new Color(0f, 0f, 0f, 0.05f);
            });
            row.RegisterCallback<MouseOutEvent>(_ => {
                row.style.backgroundColor = Color.clear;
            });

            var toggle = new Toggle();
            toggle.name = "row-toggle";
            toggle.value = selectedPaths.Contains(change.Path);
            toggle.style.marginRight = 6;
            toggle.style.marginLeft = 0;
            toggle.style.marginTop = 0;
            toggle.style.marginBottom = 0;
            toggle.style.alignSelf = Align.Center;
            toggle.RegisterValueChangedCallback(evt => {
                if (evt.newValue)
                    selectedPaths.Add(change.Path);
                else
                    selectedPaths.Remove(change.Path);
                
                UpdateSelectionState();
            });
            row.Add(toggle);

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
            path.style.textOverflow = TextOverflow.Ellipsis;
            path.style.overflow = Overflow.Hidden;
            path.tooltip = change.Path;
            row.Add(path);

            var state = new Label(GitSproutVisuals.LabelFor(change.State));
            state.style.width = 76;
            state.style.marginLeft = 8;
            state.style.unityTextAlign = TextAnchor.MiddleRight;
            state.style.color = MutedTextColor();
            row.Add(state);

            row.AddManipulator(new ContextualMenuManipulator(evt => {
                evt.menu.AppendAction("Open File", action => OpenFile(change.Path));
                evt.menu.AppendAction("Show in Finder", action => RevealFile(change.Path));
                evt.menu.AppendSeparator();
                evt.menu.AppendAction(selectedPaths.Contains(change.Path) ? "Deselect" : "Select", action => {
                    toggle.value = !toggle.value;
                });
                evt.menu.AppendAction("Select All", action => SetAllSelected(true));
                evt.menu.AppendAction("Deselect All", action => SetAllSelected(false));
                if (change.State == GitSproutState.Modified || change.State == GitSproutState.Deleted)
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Revert Changes", action => RevertFileAsync(change));
                }
            }));

            return row;
        }

        private void RefreshUI()
        {
            if (subtitleLabel != null)
                subtitleLabel.text = BuildSubtitle();

            if (filesLabel != null)
                filesLabel.text = changes.Count + " FILES";

            if (warningContainer != null)
            {
                warningContainer.Clear();
                var warning = BuildWarning();
                if (!string.IsNullOrEmpty(warning))
                {
                    var warningBox = new VisualElement();
                    warningBox.style.backgroundColor = new Color(0.95f, 0.62f, 0.22f, 0.1f);
                    warningBox.style.borderTopWidth = 1;
                    warningBox.style.borderBottomWidth = 1;
                    warningBox.style.borderLeftWidth = 1;
                    warningBox.style.borderRightWidth = 1;
                    warningBox.style.borderTopColor = new Color(0.95f, 0.62f, 0.22f, 0.3f);
                    warningBox.style.borderBottomColor = new Color(0.95f, 0.62f, 0.22f, 0.3f);
                    warningBox.style.borderLeftColor = new Color(0.95f, 0.62f, 0.22f, 0.3f);
                    warningBox.style.borderRightColor = new Color(0.95f, 0.62f, 0.22f, 0.3f);
                    warningBox.style.borderTopLeftRadius = 4;
                    warningBox.style.borderTopRightRadius = 4;
                    warningBox.style.borderBottomLeftRadius = 4;
                    warningBox.style.borderBottomRightRadius = 4;
                    warningBox.style.paddingLeft = 10;
                    warningBox.style.paddingRight = 10;
                    warningBox.style.paddingTop = 8;
                    warningBox.style.paddingBottom = 8;

                    var warningLabel = new Label(warning);
                    warningLabel.style.color = new Color(0.95f, 0.62f, 0.22f);
                    warningLabel.style.whiteSpace = WhiteSpace.Normal;
                    warningLabel.style.fontSize = 11;
                    warningBox.Add(warningLabel);
                    warningContainer.Add(warningBox);
                    warningContainer.style.display = DisplayStyle.Flex;
                }
                else
                {
                    warningContainer.style.display = DisplayStyle.None;
                }
            }

            if (scroll != null)
            {
                scroll.Clear();
                if (changes.Count == 0)
                {
                    var emptyContainer = new VisualElement();
                    emptyContainer.style.flexGrow = 1;
                    emptyContainer.style.justifyContent = Justify.Center;
                    emptyContainer.style.alignItems = Align.Center;

                    var emptyLabel = new Label("No files selected.");
                    emptyLabel.style.color = MutedTextColor();
                    emptyLabel.style.fontSize = 12;
                    emptyContainer.Add(emptyLabel);
                    scroll.Add(emptyContainer);
                }
                else
                {
                    foreach (var change in changes)
                        scroll.Add(BuildChangeRow(change));
                }
            }

            if (selectAllToggle != null)
            {
                selectAllToggle.style.display = changes.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                selectAllToggle.SetValueWithoutNotify(changes.Count > 0 && selectedPaths.Count == changes.Count);
            }

            UpdateCommitButton();
        }

        private void SetAllSelected(bool selected)
        {
            if (selected)
            {
                foreach (var change in changes)
                    selectedPaths.Add(change.Path);
            }
            else
            {
                selectedPaths.Clear();
            }

            if (scroll != null)
            {
                scroll.Query<Toggle>("row-toggle").ForEach(t => t.SetValueWithoutNotify(selected));
            }

            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            if (selectAllToggle != null)
            {
                selectAllToggle.SetValueWithoutNotify(changes.Count > 0 && selectedPaths.Count == changes.Count);
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = BuildSubtitle();
            }

            UpdateCommitButton();
        }

        private void OpenFile(string relPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
            else
            {
                var fullPath = Path.Combine(GitSproutGit.ProjectRoot, relPath);
                if (File.Exists(fullPath))
                {
                    System.Diagnostics.Process.Start(fullPath);
                }
            }
        }

        private void RevealFile(string relPath)
        {
            var fullPath = Path.Combine(GitSproutGit.ProjectRoot, relPath);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
        }

        private async void RevertFileAsync(GitSproutChange change)
        {
            if (!EditorUtility.DisplayDialog("Git Sprout - Revert File", $"Are you sure you want to revert changes to {change.Path}?\n\nThis will discard all local modifications to this file.", "Revert", "Cancel"))
                return;

            var arguments = new List<string> { "restore", "--worktree", "--staged", "--", change.Path };
            var result = await GitSproutGit.RunAsync(arguments, CancellationToken.None);
            if (result.Success)
            {
                AssetDatabase.Refresh();
                
                var list = new List<GitSproutChange>(changes);
                list.RemoveAll(c => c.Path == change.Path);
                changes = list;
                selectedPaths.Remove(change.Path);
                
                RefreshUI();
            }
            else
            {
                ShowGitError("Revert failed", result);
            }
        }

        private string BuildSubtitle()
        {
            if (changes.Count == 0)
                return "Nothing changed. Suspiciously peaceful.";

            var selectedCount = selectedPaths.Count;
            var totalCount = changes.Count;
            var baseText = $"{selectedCount} of {totalCount} files selected";

            var metaCount = changes.Count(change => selectedPaths.Contains(change.Path) && change.Path.EndsWith(".meta", StringComparison.Ordinal));
            if (metaCount > 0)
                baseText += ", including " + metaCount + " Unity meta files";

            return baseText;
        }

        private string BuildWarning()
        {
            if (changes.Any(change => selectedPaths.Contains(change.Path) && change.State == GitSproutState.Conflict))
                return "Conflicts detected in selected files. Resolve them before committing.";

            var largeFiles = changes
                .Where(change => selectedPaths.Contains(change.Path) && IsLargeFile(change))
                .Take(3)
                .Select(change => Path.GetFileName(change.Path))
                .ToArray();

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
            var hasFiles = selectedPaths.Count > 0;
            var hasConflict = changes.Any(change => selectedPaths.Contains(change.Path) && change.State == GitSproutState.Conflict);
            var enabled = hasMessage && hasFiles && !hasConflict;

            commitButton.SetEnabled(enabled);

            if (enabled)
            {
                commitButton.style.backgroundColor = AccentColor();
                commitButton.style.color = Color.white;
            }
            else
            {
                commitButton.style.backgroundColor = EditorGUIUtility.isProSkin 
                    ? new Color(0.24f, 0.24f, 0.24f) 
                    : new Color(0.8f, 0.8f, 0.8f);
                commitButton.style.color = MutedTextColor();
            }
        }

        private async System.Threading.Tasks.Task CommitAsync()
        {
            commitButton.SetEnabled(false);
            SetStatus("Staging selected files...", LabelColor());

            commitTokenSource?.Cancel();
            commitTokenSource = new CancellationTokenSource();
            var token = commitTokenSource.Token;

            var paths = changes
                .Select(change => change.Path)
                .Where(path => selectedPaths.Contains(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var addArguments = new List<string> { "add", "-A", "--" };
            addArguments.AddRange(paths);

            var addResult = await GitSproutGit.RunAsync(addArguments, token);
            if (!addResult.Success)
            {
                ShowGitError("Could not stage files", addResult);
                UpdateCommitButton();
                return;
            }

            SetStatus("Committing...", LabelColor());
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
            SetStatus(title + ": " + error.Trim(), new Color(1f, 0.31f, 0.25f));
        }

        private void SetStatus(string text, Color color)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = text;
            statusLabel.style.color = color;
            statusLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
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
