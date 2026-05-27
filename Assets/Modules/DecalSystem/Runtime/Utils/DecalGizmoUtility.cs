using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
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
        private static readonly Color OccludedLineColor = new Color(0.78f, 0.64f, 0.37f, 0.18f);
        private static readonly Color ReceiverFaceColor = new Color(0.35f, 0.85f, 1.00f, 0.10f);
        private static readonly Color ReceiverOutlineColor = new Color(0.35f, 0.85f, 1.00f, 0.90f);
        private static readonly Color DirectionColor = new Color(1.00f, 0.82f, 0.36f, 0.95f);

        /// <summary>
        /// 核心绘制接口：仅在选中时调用 (框架 + 填充 + 图标)
        /// </summary>
        public static void DrawModernGizmo(Matrix4x4 matrix, Vector3 iconPos, string iconName)
        {
#if UNITY_EDITOR
            DrawProjectionVolume(matrix, new Vector3(0, 0, 0.5f), Vector3.one);
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

        private static void DrawProjectionVolume(Matrix4x4 matrix, Vector3 center, Vector3 size)
        {
#if UNITY_EDITOR
            Matrix4x4 previousGizmoMatrix = Gizmos.matrix;
            Color previousGizmoColor = Gizmos.color;
            Matrix4x4 previousHandleMatrix = Handles.matrix;
            Color previousHandleColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;

            Gizmos.matrix = matrix;
            Gizmos.color = FillColor;
            Gizmos.DrawCube(center, size);

            Handles.matrix = matrix;
            Handles.zTest = CompareFunction.Always;
            Handles.color = OccludedLineColor;
            DrawFullWireBox(center, size, 1.0f);

            Handles.zTest = CompareFunction.LessEqual;
            DrawReceiverFace(center, size);
            DrawCornerBrackets(center, size, 0.08f);
            DrawProjectionAxis(center, size);

            Gizmos.matrix = previousGizmoMatrix;
            Gizmos.color = previousGizmoColor;
            Handles.matrix = previousHandleMatrix;
            Handles.color = previousHandleColor;
            Handles.zTest = previousZTest;
#endif
        }

        /// <summary>
        /// 绘制简约角架线框
        /// </summary>
        private static void DrawCornerBrackets(Vector3 center, Vector3 size, float bracketLength)
        {
#if UNITY_EDITOR
            Handles.color = ChampagneGold;

            Vector3 h = size * 0.5f;
            float l = bracketLength;

            Vector3[] v =
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

            DrawPlanarCorner(v[0], center, l);
            DrawPlanarCorner(v[1], center, l);
            DrawPlanarCorner(v[2], center, l);
            DrawPlanarCorner(v[3], center, l);
            DrawPlanarCorner(v[4], center, l);
            DrawPlanarCorner(v[5], center, l);
            DrawPlanarCorner(v[6], center, l);
            DrawPlanarCorner(v[7], center, l);
#endif
        }

        private static void DrawPlanarCorner(Vector3 p, Vector3 center, float length)
        {
#if UNITY_EDITOR
            float dx = p.x < center.x ? length : -length;
            float dy = p.y < center.y ? length : -length;

            Handles.DrawAAPolyLine(1.6f, p, p + new Vector3(dx, 0, 0));
            Handles.DrawAAPolyLine(1.6f, p, p + new Vector3(0, dy, 0));
#endif
        }

        private static void DrawProjectionAxis(Vector3 center, Vector3 size)
        {
#if UNITY_EDITOR
            float depthStart = center.z - size.z * 0.5f + size.z * 0.12f;
            float depthEnd = center.z + size.z * 0.5f - size.z * 0.12f;
            Vector3 from = new Vector3(center.x, center.y, depthStart);
            Vector3 to = new Vector3(center.x, center.y, depthEnd);

            Handles.color = DirectionColor;
            Handles.DrawAAPolyLine(4f, from, to);
            Handles.ConeHandleCap(
                0,
                to,
                Quaternion.LookRotation(Vector3.forward),
                0.12f,
                EventType.Repaint
            );
#endif
        }

        private static void DrawReceiverFace(Vector3 center, Vector3 size)
        {
#if UNITY_EDITOR
            Vector3 h = size * 0.5f;
            float z = center.z + h.z;
            Vector3[] corners =
            {
                center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z),
                center + new Vector3(-h.x, h.y, h.z)
            };

            Handles.DrawSolidRectangleWithOutline(
                corners,
                ReceiverFaceColor,
                ReceiverOutlineColor
            );

            Vector3 crossX0 = new Vector3(center.x - h.x, center.y, z);
            Vector3 crossX1 = new Vector3(center.x + h.x, center.y, z);
            Vector3 crossY0 = new Vector3(center.x, center.y - h.y, z);
            Vector3 crossY1 = new Vector3(center.x, center.y + h.y, z);
            Handles.color = ReceiverOutlineColor;
            Handles.DrawAAPolyLine(2f, crossX0, crossX1);
            Handles.DrawAAPolyLine(2f, crossY0, crossY1);
#endif
        }

        private static void DrawFullWireBox(Vector3 center, Vector3 size, float thickness)
        {
#if UNITY_EDITOR
            Vector3 h = size * 0.5f;
            Vector3[] v =
            {
                center + new Vector3(-h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z),
                center + new Vector3(-h.x, h.y, h.z)
            };

            DrawEdge(v[0], v[1], thickness);
            DrawEdge(v[1], v[2], thickness);
            DrawEdge(v[2], v[3], thickness);
            DrawEdge(v[3], v[0], thickness);
            DrawEdge(v[4], v[5], thickness);
            DrawEdge(v[5], v[6], thickness);
            DrawEdge(v[6], v[7], thickness);
            DrawEdge(v[7], v[4], thickness);
            DrawEdge(v[0], v[4], thickness);
            DrawEdge(v[1], v[5], thickness);
            DrawEdge(v[2], v[6], thickness);
            DrawEdge(v[3], v[7], thickness);
#endif
        }

        private static void DrawEdge(Vector3 a, Vector3 b, float thickness)
        {
#if UNITY_EDITOR
            Handles.DrawLine(a, b, thickness);
#endif
        }
    }
}
