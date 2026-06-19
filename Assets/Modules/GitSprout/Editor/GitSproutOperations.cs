using System;
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

            RunOperation("Push", new[] { "push" });
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

        private static string BuildMessage(GitSproutCommandResult result)
        {
            var output = string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output;
            output = string.IsNullOrWhiteSpace(output) ? "Done." : output.Trim();

            if (result.Success)
                return output;

            return "Git failed:\n\n" + output;
        }
    }
}
