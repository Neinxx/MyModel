using System;
using UnityEngine;

namespace GitSprout
{
    internal enum GitSproutState
    {
        Clean = 0,
        Ignored = 1,
        Untracked = 2,
        Added = 3,
        Modified = 4,
        Deleted = 5,
        Conflict = 6,
        Error = 7,
        GitMissing = 8,
        NotRepository = 9
    }

    internal readonly struct GitSproutChange
    {
        public readonly string Path;
        public readonly GitSproutState State;
        public readonly string RawStatus;

        public GitSproutChange(string path, GitSproutState state, string rawStatus)
        {
            Path = path;
            State = state;
            RawStatus = rawStatus;
        }
    }

    internal static class GitSproutVisuals
    {
        public static Color ColorFor(GitSproutState state)
        {
            switch (state)
            {
                case GitSproutState.Added:
                    return new Color(0.27f, 0.77f, 0.42f, 1f);
                case GitSproutState.Modified:
                    return new Color(0.31f, 0.64f, 1f, 1f);
                case GitSproutState.Untracked:
                    return new Color(0.95f, 0.61f, 0.22f, 1f);
                case GitSproutState.Deleted:
                    return new Color(0.9f, 0.34f, 0.34f, 1f);
                case GitSproutState.Conflict:
                case GitSproutState.Error:
                    return new Color(1f, 0.23f, 0.19f, 1f);
                case GitSproutState.GitMissing:
                case GitSproutState.NotRepository:
                    return new Color(0.55f, 0.55f, 0.55f, 1f);
                default:
                    return Color.clear;
            }
        }

        public static string LabelFor(GitSproutState state)
        {
            switch (state)
            {
                case GitSproutState.Added:
                    return "Added";
                case GitSproutState.Modified:
                    return "Modified";
                case GitSproutState.Untracked:
                    return "Untracked";
                case GitSproutState.Deleted:
                    return "Deleted";
                case GitSproutState.Conflict:
                    return "Conflict";
                case GitSproutState.Error:
                    return "Git error";
                case GitSproutState.GitMissing:
                    return "Git missing";
                case GitSproutState.NotRepository:
                    return "No Git repository";
                default:
                    return string.Empty;
            }
        }

        public static string GlyphFor(GitSproutState state)
        {
            switch (state)
            {
                case GitSproutState.Added:
                    return "+";
                case GitSproutState.Untracked:
                    return "?";
                case GitSproutState.Deleted:
                    return "-";
                case GitSproutState.Conflict:
                case GitSproutState.Error:
                    return "!";
                default:
                    return string.Empty;
            }
        }

        public static GitSproutState Max(GitSproutState a, GitSproutState b)
        {
            return (GitSproutState)Mathf.Max((int)a, (int)b);
        }
    }
}
