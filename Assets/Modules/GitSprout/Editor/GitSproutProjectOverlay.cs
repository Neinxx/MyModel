using UnityEditor;
using UnityEngine;

namespace GitSprout
{
    [InitializeOnLoad]
    internal static class GitSproutProjectOverlay
    {
        private static GUIStyle statusLabelStyle;

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
                DrawStatusLabel(GetIconStatusLabelRect(selectionRect), state, color, tooltip);
                return;
            }

            DrawStatusLabel(GetStatusLabelRect(selectionRect), state, color, tooltip);
        }

        private static bool IsGridItem(Rect selectionRect)
        {
            return selectionRect.height > 22f;
        }

        private static Rect GetIconStatusLabelRect(Rect selectionRect)
        {
            const float width = 20f;
            const float height = 18f;
            const float labelReserve = 24f;
            const float inset = 6f;

            var iconTop = selectionRect.y + inset;
            var iconBottom = selectionRect.yMax - labelReserve;
            var labelHeight = Mathf.Min(height, Mathf.Max(12f, iconBottom - iconTop));
            return new Rect(selectionRect.xMax - width - inset, iconTop, width, labelHeight);
        }

        private static Rect GetStatusLabelRect(Rect selectionRect)
        {
            const float width = 22f;
            const float rightInset = 6f;
            return new Rect(selectionRect.xMax - width - rightInset, selectionRect.y, width, selectionRect.height);
        }

        private static void DrawStatusLabel(Rect rect, GitSproutState state, Color color, string tooltip)
        {
            var glyph = GitSproutVisuals.GlyphFor(state);
            if (string.IsNullOrEmpty(glyph))
                return;

            var content = new GUIContent(glyph, tooltip);
            var style = GetStatusLabelStyle();
            var oldColor = GUI.color;
            var textColor = color;
            textColor.a = 0.95f;
            GUI.color = textColor;
            GUI.Label(rect, content, style);
            GUI.color = oldColor;
        }

        private static GUIStyle GetStatusLabelStyle()
        {
            if (statusLabelStyle != null)
                return statusLabelStyle;

            statusLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 1)
            };
            return statusLabelStyle;
        }

    }
}
