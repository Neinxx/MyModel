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

        public bool Success
        {
            get { return ExitCode == 0; }
        }
    }

    internal static class GitSproutGit
    {
        public static string ProjectRoot
        {
            get { return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length); }
        }

        public static async Task<GitSproutCommandResult> RunAsync(IEnumerable<string> arguments, CancellationToken token)
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

            startInfo.Arguments = JoinArguments(arguments);

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    while (!process.HasExited)
                    {
                        if (token.IsCancellationRequested)
                        {
                            TryKill(process);
                            token.ThrowIfCancellationRequested();
                        }

                        await Task.Delay(50, token);
                    }

                    return new GitSproutCommandResult
                    {
                        ExitCode = process.ExitCode,
                        Output = await outputTask,
                        Error = await errorTask
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
                    Error = exception.Message
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

        private static string JoinArguments(IEnumerable<string> arguments)
        {
            var builder = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(QuoteArgument(argument));
            }

            return builder.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            var needsQuotes = false;
            for (var i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument[i]) || argument[i] == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
                return argument;

            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}
