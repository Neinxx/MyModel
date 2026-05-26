using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DecalMini
{
    /// <summary>
    /// 现代简约级 Gizmo 绘制工具：极简角点框架
    /// </summary>
    public static class DecalGizmoUtility
    {
        public static readonly Color ChampagneGold = new Color(0.78f, 0.64f, 0.37f, 1.0f);
        public static readonly Color FillColor = new Color(0.78f, 0.64f, 0.37f, 0.05f);

        /// <summary>
        /// 核心绘制接口：仅在选中时调用 (框架 + 填充 + 图标)
        /// </summary>
        public static void DrawModernGizmo(Matrix4x4 matrix, Vector3 iconPos, string iconName)
        {
#if UNITY_EDITOR
            // 1. 绘制极淡的填充感
            Gizmos.matrix = matrix;
            Gizmos.color = FillColor;
            Gizmos.DrawCube(new Vector3(0, 0, 0.5f), Vector3.one);

            // 2. 绘制角点框架 (现代简约)
            DrawCornerBrackets(matrix, new Vector3(0, 0, 0.5f), Vector3.one, 0.15f);

            // 3. 绘制图标
            DrawIcon(iconPos, iconName);
#endif
        }

        /// <summary>
        /// 仅绘制图标：用于非选中状态，并向上偏移 0.8 米以防埋地，方便用户点击选中
        /// </summary>
        public static void DrawIcon(Vector3 pos, string iconName)
        {
#if UNITY_EDITOR
            Gizmos.DrawIcon(pos + Vector3.up * 0.8f, iconName, true);
#endif
        }

        /// <summary>
        /// 绘制简约角架线框
        /// </summary>
        private static void DrawCornerBrackets(
            Matrix4x4 matrix,
            Vector3 center,
            Vector3 size,
            float bracketLength
        )
        {
#if UNITY_EDITOR
            Handles.matrix = matrix;
            Handles.color = ChampagneGold;

            Vector3 h = size * 0.5f;
            float l = bracketLength;

            // 8个顶点的坐标
            Vector3[] v = new Vector3[]
            {
                center + new Vector3(-h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z),
                center + new Vector3(-h.x, h.y, h.z),
            };

            // 为每个顶点绘制三条短线形成 L 型角架
            foreach (var p in v)
            {
                float dx = (p.x < center.x) ? l : -l;
                float dy = (p.y < center.y) ? l : -l;
                float dz = (p.z < center.z) ? l : -l;

                Handles.DrawLine(p, p + new Vector3(dx, 0, 0), 2f);
                Handles.DrawLine(p, p + new Vector3(0, dy, 0), 2f);
                Handles.DrawLine(p, p + new Vector3(0, 0, dz), 2f);
            }

            // 绘制中心投影指示
            Handles.DrawLine(center, center + Vector3.forward * 0.6f, 1.5f);
#endif
        }
    }
}
