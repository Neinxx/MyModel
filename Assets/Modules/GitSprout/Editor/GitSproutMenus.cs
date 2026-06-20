using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitSprout
{
    internal static class GitSproutMenus
    {
        [MenuItem("Assets/Git Sprout/Commit Selected...", true)]
        private static bool CanCommitSelected()
        {
            return Selection.assetGUIDs.Length > 0 && GitSproutStatusService.GetChangesForSelection().Count > 0;
        }

        [MenuItem("Assets/Git Sprout/Commit Selected...", false, 2000)]
        private static void CommitSelected()
        {
            GitSproutCommitWindow.Open(GitSproutStatusService.GetChangesForSelection(), "Commit Selected");
        }

        [MenuItem("Assets/Git Sprout/Commit Project Changes...", true)]
        private static bool CanCommitProject()
        {
            return GitSproutStatusService.GetAllProjectChanges().Count > 0;
        }

        [MenuItem("Assets/Git Sprout/Commit Project Changes...", false, 2001)]
        private static void CommitProject()
        {
            GitSproutCommitWindow.Open(GitSproutStatusService.GetAllProjectChanges(), "Commit Project Changes");
        }

        [MenuItem("Assets/Git Sprout/Refresh Status", false, 2020)]
        private static void RefreshStatus()
        {
            GitSproutStatusService.RefreshNow();
            EditorApplication.RepaintProjectWindow();
        }

        [MenuItem("Assets/Git Sprout/Fetch", false, 2030)]
        private static void FetchFromAssets()
        {
            GitSproutOperations.Fetch();
        }

        [MenuItem("Assets/Git Sprout/Pull (Fast-forward Only)", false, 2031)]
        private static void PullFromAssets()
        {
            GitSproutOperations.PullFastForward();
        }

        [MenuItem("Assets/Git Sprout/Push", false, 2032)]
        private static void PushFromAssets()
        {
            GitSproutOperations.Push();
        }

        [MenuItem("Tools/Git Sprout/Commit Project Changes...", false, 2000)]
        private static void CommitProjectFromTools()
        {
            GitSproutCommitWindow.Open(GitSproutStatusService.GetAllProjectChanges(), "Commit Project Changes");
        }

        [MenuItem("Tools/Git Sprout/Refresh Status", false, 2001)]
        private static void RefreshFromTools()
        {
            GitSproutStatusService.RefreshNow();
            EditorApplication.RepaintProjectWindow();
        }

        [MenuItem("Tools/Git Sprout/Fetch", false, 2010)]
        private static void FetchFromTools()
        {
            GitSproutOperations.Fetch();
        }

        [MenuItem("Tools/Git Sprout/Pull (Fast-forward Only)", false, 2011)]
        private static void PullFromTools()
        {
            GitSproutOperations.PullFastForward();
        }

        [MenuItem("Tools/Git Sprout/Push", false, 2012)]
        private static void PushFromTools()
        {
            GitSproutOperations.Push();
        }

        [MenuItem("Tools/Git Sprout/Status Summary", false, 2020)]
        private static void StatusSummary()
        {
            ShowStatusSummaryDialog();
        }

        [MenuItem("Tools/Git Sprout/Diagnostics", false, 2021)]
        private static void Diagnostics()
        {
            GitSproutDiagnosticsWindow.Open();
        }

        internal static void ShowStatusSummaryDialog()
        {
            var changes = GitSproutStatusService.GetAllProjectChanges();
            var title = changes.Count == 0 ? "Git Sprout" : "Git Sprout - " + changes.Count + " changes";
            var branch = string.IsNullOrEmpty(GitSproutStatusService.BranchSummary)
                ? string.Empty
                : "Branch: " + GitSproutStatusService.BranchSummary + "\n\n";
            var message = branch + (changes.Count == 0
                ? "No project changes. Suspiciously peaceful."
                : string.Join("\n", changes.Take(12).Select(change => change.RawStatus + "  " + change.Path)));

            if (changes.Count > 12)
                message += "\n...and " + (changes.Count - 12) + " more";

            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
