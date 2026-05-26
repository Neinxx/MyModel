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
        static DecalSystemDebugDrawerMini()
        {
            // 注册 Scene 视图刷新回调
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // 仅在非播放模式下绘制，避免影响运行体验（可选）
            DecalSystemMini.DrawDebugGrid();
        }
    }
}
