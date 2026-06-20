using UnityEditor;
using UnityEngine;

namespace GitSprout
{
    internal sealed class GitSproutDiagnosticsWindow : EditorWindow
    {
        public static void Open()
        {
            var window = GetWindow<GitSproutDiagnosticsWindow>("Git Sprout");
            window.minSize = new Vector2(420f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Git Sprout Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            DrawReadonly("Project Root", GitSproutGit.ProjectRoot);
            DrawReadonly("Branch", string.IsNullOrEmpty(GitSproutStatusService.BranchSummary) ? "(unknown)" : GitSproutStatusService.BranchSummary);
            DrawReadonly("Last Refresh", GitSproutStatusService.LastRefreshTime == default ? "(not refreshed)" : GitSproutStatusService.LastRefreshTime.ToString("yyyy-MM-dd HH:mm:ss"));
            DrawReadonly("Is Refreshing", GitSproutStatusService.IsRefreshing ? "Yes" : "No");
            DrawReadonly("All Cached Changes", GitSproutStatusService.ChangeCount.ToString());
            DrawReadonly("Project Changes", GitSproutStatusService.ProjectChangeCount.ToString());
            DrawReadonly("Last Command", string.IsNullOrEmpty(GitSproutStatusService.LastCommand) ? "(none)" : GitSproutStatusService.LastCommand);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Last Error", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(GitSproutStatusService.LastError) ? "No Git errors recorded." : GitSproutStatusService.LastError, MessageType.None);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Status"))
                    GitSproutStatusService.RefreshNow();

                if (GUILayout.Button("Status Summary"))
                    GitSproutMenus.ShowStatusSummaryDialog();
            }
        }

        private static void DrawReadonly(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(130f));
                EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }
}
