using UnityEditor;
using UnityEngine;

namespace GitSprout
{
    [InitializeOnLoad]
    internal static class GitSproutProjectOverlay
    {
        static GitSproutProjectOverlay()
        {
            EditorApplication.projectWindowItemOnGUI += DrawProjectItem;
            EditorApplication.delayCall += GitSproutStatusService.RefreshNow;
        }

        private static void DrawProjectItem(string guid, Rect selectionRect)
        {
            if (!GitSproutStatusService.TryGetVisualStateForGuid(guid, out var state))
                return;

            var color = GitSproutVisuals.ColorFor(state);
            if (color.a <= 0f)
                return;

            var tooltip = GitSproutStatusService.GetTooltipForGuid(guid);
            if (IsGridItem(selectionRect))
            {
                DrawStatusCorner(GetStatusCornerRect(selectionRect), color, tooltip);
                return;
            }

            DrawStatusLine(GetStatusLineRect(selectionRect), color, tooltip);
        }

        private static bool IsGridItem(Rect selectionRect)
        {
            return selectionRect.height > 22f;
        }

        private static Rect GetStatusCornerRect(Rect selectionRect)
        {
            const float size = 12f;
            const float labelReserve = 24f;
            const float inset = 6f;

            var iconBottom = selectionRect.yMax - labelReserve;
            return new Rect(selectionRect.x + inset, iconBottom - size - inset, size, size);
        }

        private static Rect GetStatusLineRect(Rect selectionRect)
        {
            return new Rect(selectionRect.x + 1f, selectionRect.y + 3f, 3f, Mathf.Max(4f, selectionRect.height - 6f));
        }

        private static void DrawStatusLine(Rect rect, Color color, string tooltip)
        {
            var lineColor = color;
            lineColor.a = 0.9f;
            EditorGUI.DrawRect(rect, lineColor);

            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 8f, rect.height + 6f), new GUIContent(string.Empty, tooltip));
        }

        private static void DrawStatusCorner(Rect rect, Color color, string tooltip)
        {
            var lineColor = color;
            lineColor.a = 0.92f;

            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), lineColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), lineColor);

            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f), new GUIContent(string.Empty, tooltip));
        }
    }
}
