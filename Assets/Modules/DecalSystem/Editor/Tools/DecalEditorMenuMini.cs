using UnityEditor;
using UnityEngine;

namespace DecalMini
{
    // 编辑器快捷菜单工具
    public static class DecalEditorMenuMini
    {
        [MenuItem("GameObject/Effects/Decal Projector Mini", false, 10)]
        private static void CreateDecalProjector(MenuCommand menuCommand)
        {
            var config = DecalSystemMini.CurrentConfig;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            GameObject go = new("Decal_Projector_Mini");
            var projector = go.AddComponent<DecalProjectorMini>();

            // 1. 设置默认参数
            if (config != null)
            {
                // 从 LayerMask 提取第一个有效的层级索引
                int maskValue = config.defaultLayer.value;
                int layerIndex = 0;
                for (int i = 0; i < 32; i++)
                {
                    if ((maskValue & (1 << i)) != 0) { layerIndex = i; break; }
                }
                
                go.layer = layerIndex;
                go.transform.localScale = config.defaultSize;
            }

            // 2. 射线检测定位 (屏幕中心)
            Ray ray = sceneView.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            LayerMask mask = config != null ? config.creationRaycastLayer : -1;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
            {
                go.transform.position = hit.point + hit.normal * 0.1f;
                // 贴花通常需要 Z 轴指向表面法线
                go.transform.rotation = Quaternion.LookRotation(-hit.normal);
            }
            else
            {
                // 没检测到则放在相机前方 5 米处
                go.transform.position = ray.GetPoint(5f);
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
            }

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Decal Mini");
            Selection.activeObject = go;
        }
    }
}
