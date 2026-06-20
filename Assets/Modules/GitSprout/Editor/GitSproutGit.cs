using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GitSprout
{
    internal sealed class GitSproutCommandResult
    {
        public int ExitCode;
        public string Output;
        public string Error;
        public string Command;

        public bool Success
        {
            get { return ExitCode == 0; }
        }
    }

    internal static class GitSproutGit
    {
        private const int DefaultTimeoutMilliseconds = 30000;

        public static string ProjectRoot
        {
            get { return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length); }
        }

        public static async Task<GitSproutCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken token, int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    var startedAt = Environment.TickCount;

                    while (!process.HasExited)
                    {
                        if (token.IsCancellationRequested || HasTimedOut(startedAt, timeoutMilliseconds))
                        {
                            TryKill(process);
                            token.ThrowIfCancellationRequested();
                            return new GitSproutCommandResult
                            {
                                ExitCode = -2,
                                Output = string.Empty,
                                Error = "Git command timed out.",
                                Command = BuildCommandLabel(arguments)
                            };
                        }

                        await Task.Delay(50, token);
                    }

                    return new GitSproutCommandResult
                    {
                        ExitCode = process.ExitCode,
                        Output = await outputTask,
                        Error = await errorTask,
                        Command = BuildCommandLabel(arguments)
                    };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new GitSproutCommandResult
                {
                    ExitCode = -1,
                    Output = string.Empty,
                    Error = exception.Message,
                    Command = BuildCommandLabel(arguments)
                };
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
                // Best effort only. A stale git status process is less helpful than silence.
            }
        }

        private static bool HasTimedOut(int startedAt, int timeoutMilliseconds)
        {
            return timeoutMilliseconds > 0 && Environment.TickCount - startedAt >= timeoutMilliseconds;
        }

        private static string BuildCommandLabel(IReadOnlyList<string> arguments)
        {
            return "git " + string.Join(" ", arguments);
        }
    }
}
