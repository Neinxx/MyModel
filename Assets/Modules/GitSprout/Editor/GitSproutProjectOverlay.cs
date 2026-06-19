using UnityEditor;
using UnityEngine;

namespace GitSprout
{
    [InitializeOnLoad]
    internal static class GitSproutProjectOverlay
    {
        private static GUIStyle glyphStyle;
        private static Texture2D circleTexture;
        private static Texture2D circleBorderTexture;

        static GitSproutProjectOverlay()
        {
            EditorApplication.projectWindowItemOnGUI += DrawProjectItem;
        }

        private static void DrawProjectItem(string guid, Rect selectionRect)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return;

            var state = GitSproutStatusService.GetVisualState(path);
            if (state == GitSproutState.Clean || state == GitSproutState.Ignored)
                return;

            var color = GitSproutVisuals.ColorFor(state);
            if (color.a <= 0f)
                return;

            var rect = GetBadgeRect(selectionRect);
            DrawBadge(rect, color, GitSproutVisuals.GlyphFor(state), GitSproutStatusService.GetTooltip(path));
        }

        private static Rect GetBadgeRect(Rect selectionRect)
        {
            var isGrid = selectionRect.height > 22f;
            var size = isGrid ? 10f : 8f;

            if (isGrid)
                return new Rect(selectionRect.xMax - size - 5f, selectionRect.y + 5f, size, size);

            return new Rect(selectionRect.xMax - size - 5f, selectionRect.y + (selectionRect.height - size) * 0.5f, size, size);
        }

        private static void DrawBadge(Rect rect, Color color, string glyph, string tooltip)
        {
            var tooltipRect = rect;
            tooltipRect.x -= 4f;
            tooltipRect.y -= 4f;
            tooltipRect.width += 8f;
            tooltipRect.height += 8f;

            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), GetCircleBorderTexture());
            GUI.color = color;
            GUI.DrawTexture(rect, GetCircleTexture());
            GUI.color = oldColor;

            var style = GetGlyphStyle();
            if (!string.IsNullOrEmpty(glyph) && rect.width >= 9f && style != null)
                GUI.Label(rect, glyph, style);

            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(tooltipRect, new GUIContent(string.Empty, tooltip));
        }

        private static GUIStyle GetGlyphStyle()
        {
            if (glyphStyle != null)
                return glyphStyle;

            if (EditorStyles.boldLabel == null)
                return null;

            glyphStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8
            };
            glyphStyle.normal.textColor = Color.white;
            return glyphStyle;
        }

        private static Texture2D GetCircleTexture()
        {
            if (circleTexture == null)
                circleTexture = CreateCircleTexture(16, Color.white, 0f);
            return circleTexture;
        }

        private static Texture2D GetCircleBorderTexture()
        {
            if (circleBorderTexture == null)
                circleBorderTexture = CreateCircleTexture(18, Color.white, 0f);
            return circleBorderTexture;
        }

        private static Texture2D CreateCircleTexture(int size, Color color, float softness)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = (size - 1) * 0.5f;
            var radius = center;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = distance <= radius - softness ? 1f : Mathf.Clamp01(radius - distance);
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
