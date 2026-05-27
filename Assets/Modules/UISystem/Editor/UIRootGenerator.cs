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
        public const string DefaultPrefabPath = "Assets/Demo/Art/Prefabs/UIRoot.prefab";

        [MenuItem("GameObject/UI System/UIRoot (AAA Standard)", false, 11)]
        public static void GenerateUIRoot(MenuCommand menuCommand)
        {
            RemoveSceneEventSystems();
            GameObject rootObj = CreateUIRoot("UIRoot", true);

            Undo.RegisterCreatedObjectUndo(rootObj, "Create AAA UIRoot");
            Selection.activeGameObject = rootObj;
            
            Debug.Log("<color=#3FB950><b>[UIRootGenerator]</b></color> Successfully generated AAA Standard UIRoot scene instance!");
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

            GameObject rootObj = CreateUIRoot(Path.GetFileNameWithoutExtension(prefabPath), false);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObj, prefabPath);
                SetSerializedStringIfAssetExists(
                    "Assets/Modules/Mainboard/Data/UIRootFeature.asset",
                    "uiRootKey",
                    prefabPath
                );

                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log($"<color=#3FB950><b>[UIRootGenerator]</b></color> Successfully generated UIRoot prefab: {prefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(rootObj);
            }
        }

        private static GameObject CreateUIRoot(string rootName, bool includeUICamera)
        {
            // 1. Create Root Object
            GameObject rootObj = new GameObject(string.IsNullOrWhiteSpace(rootName) ? "UIRoot" : rootName);
            var uiManager = rootObj.AddComponent<UIManager>();

            // 2. Optional embedded UICamera for scene-only quick setup.
            Camera uiCamera = includeUICamera ? CreateUICamera(rootObj.transform) : null;

            // 3. Create Canvas
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

            // 4. Create Layers
            uiManager.backgroundLayer = CreateLayer("1_BackgroundLayer", canvasObj.transform);
            uiManager.hudLayer = CreateLayer("2_HUDLayer", canvasObj.transform);
            uiManager.mainPanelLayer = CreateLayer("3_MainPanelLayer", canvasObj.transform);
            uiManager.popupLayer = CreateLayer("4_PopupLayer", canvasObj.transform);
            uiManager.toastLayer = CreateLayer("5_ToastLayer", canvasObj.transform);
            uiManager.systemLayer = CreateLayer("6_SystemLayer", canvasObj.transform);
            CreateEventSystem(rootObj.transform);

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

            camObj.AddComponent<CameraSystem.Runtime.UICameraRegisterHook>();
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

        private static GameObject CreateEventSystem(Transform parent)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.transform.SetParent(parent, false);

            eventSystemObj.AddComponent<EventSystem>();
            var inputModule = eventSystemObj.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            return eventSystemObj;
        }

        private static void RemoveSceneEventSystems()
        {
            if (!Application.isEditor || Application.isPlaying)
                return;

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem == null || !eventSystem.gameObject.scene.IsValid())
                    continue;

                Undo.DestroyObjectImmediate(eventSystem.gameObject);
            }
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

        private static void SetSerializedStringIfAssetExists(
            string assetPath,
            string propertyName,
            string value
        )
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
                return;

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return;

            property.stringValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
