using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UISystem.Runtime;
using System.IO;

namespace UISystem.Editor
{
    public static class UIRootGenerator
    {
        public const string DefaultPrefabPath = "Assets/Modules/UISystem/Prefabs/UIRoot.prefab";

        [MenuItem("GameObject/UI System/UIRoot", false, 11)]
        public static void GenerateUIRoot(MenuCommand menuCommand)
        {
            GameObject rootObj = CreateUIRoot("UIRoot", true, true);

            Undo.RegisterCreatedObjectUndo(rootObj, "Create UIRoot");
            Selection.activeGameObject = rootObj;
            
            Debug.Log("[UIRootGenerator] Created UIRoot scene instance.");
        }

        [MenuItem("Tools/UI System/Generate UIRoot Prefab", false, 20)]
        public static void GenerateDefaultUIRootPrefab()
        {
            GenerateUIRootPrefab(DefaultPrefabPath);
        }

        public static GameObject GenerateUIRootPrefab(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                EditorUtility.DisplayDialog("UIRoot Generator", "Prefab path is empty.", "OK");
                return null;
            }

            EnsureAssetFolder(prefabPath);

            GameObject rootObj = CreateUIRoot(Path.GetFileNameWithoutExtension(prefabPath), false, false);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObj, prefabPath);

                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log($"[UIRootGenerator] Generated UIRoot prefab: {prefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(rootObj);
            }
        }

        private static GameObject CreateUIRoot(string rootName, bool includeUICamera, bool skipEventSystemIfSceneHasOne)
        {
            GameObject rootObj = new GameObject(string.IsNullOrWhiteSpace(rootName) ? "UIRoot" : rootName);
            var uiManager = rootObj.AddComponent<UIManager>();

            Camera uiCamera = includeUICamera ? CreateUICamera(rootObj.transform) : null;

            GameObject canvasObj = new GameObject("UI_Canvas");
            canvasObj.layer = GetUILayer();
            canvasObj.transform.SetParent(rootObj.transform, false);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = includeUICamera ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 10;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            uiManager.SetLayer(UILayerType.Background, CreateLayer("1_BackgroundLayer", canvasObj.transform));
            uiManager.SetLayer(UILayerType.HUD, CreateLayer("2_HUDLayer", canvasObj.transform));
            uiManager.SetLayer(UILayerType.MainPanel, CreateLayer("3_MainPanelLayer", canvasObj.transform));
            uiManager.SetLayer(UILayerType.Popup, CreateLayer("4_PopupLayer", canvasObj.transform));
            uiManager.SetLayer(UILayerType.Toast, CreateLayer("5_ToastLayer", canvasObj.transform));
            uiManager.SetLayer(UILayerType.System, CreateLayer("6_SystemLayer", canvasObj.transform));
            CreateEventSystem(rootObj.transform, skipEventSystemIfSceneHasOne);

            return rootObj;
        }

        private static Camera CreateUICamera(Transform parent)
        {
            GameObject camObj = new GameObject("UICamera");
            camObj.transform.SetParent(parent, false);
            
            Camera uiCamera = camObj.AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = GetUILayerMask();
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 5;
            uiCamera.nearClipPlane = -100;
            uiCamera.farClipPlane = 100;
            uiCamera.allowHDR = false;
            uiCamera.allowMSAA = false;
            return uiCamera;
        }

        private static Transform CreateLayer(string name, Transform parent)
        {
            GameObject layerObj = new GameObject(name);
            layerObj.layer = GetUILayer();
            layerObj.transform.SetParent(parent, false);
            
            RectTransform rect = layerObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            return rect;
        }

        private static GameObject CreateEventSystem(Transform parent, bool skipIfSceneHasOne)
        {
            if (skipIfSceneHasOne && Object.FindFirstObjectByType<EventSystem>() != null)
                return null;

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.transform.SetParent(parent, false);

            eventSystemObj.AddComponent<EventSystem>();
            var inputModule = eventSystemObj.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            return eventSystemObj;
        }

        private static int GetUILayer()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 ? uiLayer : 0;
        }

        private static int GetUILayerMask()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 ? 1 << uiLayer : 0;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

    }
}
