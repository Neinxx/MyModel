using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 全局调试绘制器：将空间哈希网格注入到 Scene 视图
    /// </summary>
    [InitializeOnLoad]
    public static class DecalSystemDebugDrawerMini
    {
        private static readonly List<Vector3> _gridCenters = new();

        static DecalSystemDebugDrawerMini()
        {
            // 注册 Scene 视图刷新回调
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var config = DecalSystemMini.CurrentConfig;
            if (config == null || !config.showDebugGrid)
                return;

            DecalSystemMini.CopyDebugGridCenters(_gridCenters);
            Handles.color = new Color(0.77f, 0.54f, 0.98f, 0.3f);

            float size = config.spatialGridSize;
            for (int i = 0; i < _gridCenters.Count; i++)
                Handles.DrawWireCube(_gridCenters[i], new Vector3(size, 100f, size));
        }
    }
}
