using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// Lightweight Gizmo helper kept in runtime without editor assembly dependencies.
    /// </summary>
    public static class DecalGizmoUtility
    {
        public static readonly Color ChampagneGold = new Color(0.78f, 0.64f, 0.37f, 1.0f);
        public static readonly Color FillColor = new Color(0.78f, 0.64f, 0.37f, 0.05f);

        public static void DrawModernGizmo(Matrix4x4 matrix, Vector3 iconPos, string iconName)
        {
            DrawProjectionVolume(matrix, new Vector3(0, 0, 0.5f), Vector3.one);
            DrawIcon(iconPos, iconName);
        }

        public static void DrawIcon(Vector3 pos, string iconName)
        {
            Gizmos.DrawIcon(pos + Vector3.up * 0.8f, iconName, true);
        }

        private static void DrawProjectionVolume(Matrix4x4 matrix, Vector3 center, Vector3 size)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = matrix;
            Gizmos.color = FillColor;
            Gizmos.DrawCube(center, size);

            Gizmos.color = ChampagneGold;
            Gizmos.DrawWireCube(center, size);
            DrawProjectionAxis(center, size);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawProjectionAxis(Vector3 center, Vector3 size)
        {
            float depthStart = center.z - size.z * 0.5f + size.z * 0.12f;
            float depthEnd = center.z + size.z * 0.5f - size.z * 0.12f;
            Vector3 from = new Vector3(center.x, center.y, depthStart);
            Vector3 to = new Vector3(center.x, center.y, depthEnd);

            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(to, 0.035f);
        }
    }
}
