using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DecalMini.Editor
{
    /// <summary>
    /// 全局编辑器模拟器：确保在不运行的情况下，拖动任何父物体也能产生脚印
    /// </summary>
    [InitializeOnLoad]
    public static class DecalFootprintEditorSimulator
    {
        private static List<DecalFootprintComponent> _activeComponents = new List<DecalFootprintComponent>();
        private static double _lastSearchTime;

        static DecalFootprintEditorSimulator()
        {
            // 注册全局更新回调
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            // 运行模式下交给脚本自己的 Update
            if (Application.isPlaying) return;

            // 为了性能，每隔 1 秒重新扫描一次场景中的脚印组件
            if (EditorApplication.timeSinceStartup - _lastSearchTime > 1.0f)
            {
                RefreshComponents();
                _lastSearchTime = EditorApplication.timeSinceStartup;
            }

            // 驱动所有活跃组件
            for (int i = _activeComponents.Count - 1; i >= 0; i--)
            {
                var comp = _activeComponents[i];
                if (comp == null)
                {
                    _activeComponents.RemoveAt(i);
                    continue;
                }

                // 如果物体在层级中处于开启状态，则执行步进检测
                if (comp.gameObject.activeInHierarchy && comp.enabled)
                {
                    if (comp.ManualUpdateEditor())
                    {
                        // 强制刷新场景视图，显示纯数据贴花
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private static void RefreshComponents()
        {
            _activeComponents.Clear();
            var found = Object.FindObjectsOfType<DecalFootprintComponent>();
            if (found != null)
            {
                _activeComponents.AddRange(found);
            }
        }
    }
}
