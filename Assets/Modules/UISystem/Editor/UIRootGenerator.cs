using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UISystem.Runtime;

namespace UISystem.Editor
{
    public static class UIRootGenerator
    {
        [MenuItem("GameObject/UI System/UIRoot (AAA Standard)", false, 11)]
        public static void GenerateUIRoot(MenuCommand menuCommand)
        {
            // 1. Create Root Object
            GameObject rootObj = new GameObject("UIRoot");
            var uiManager = rootObj.AddComponent<UIManager>();

            // 2. Create UICamera
            GameObject camObj = new GameObject("UICamera");
            camObj.transform.SetParent(rootObj.transform, false);
            
            Camera uiCamera = camObj.AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 5;
            uiCamera.nearClipPlane = -100;
            uiCamera.farClipPlane = 100;

            // Add hook to auto-register with CameraManager and URP Stack Linker
            camObj.AddComponent<CameraSystem.Runtime.UICameraRegisterHook>();

            // 3. Create Canvas
            GameObject canvasObj = new GameObject("UI_Canvas");
            canvasObj.layer = LayerMask.NameToLayer("UI");
            canvasObj.transform.SetParent(rootObj.transform, false);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
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

            // Register Undo
            Undo.RegisterCreatedObjectUndo(rootObj, "Create AAA UIRoot");
            Selection.activeGameObject = rootObj;
            
            Debug.Log("<color=#3FB950><b>[UIRootGenerator]</b></color> Successfully generated AAA Standard UIRoot!");
        }

        private static Transform CreateLayer(string name, Transform parent)
        {
            GameObject layerObj = new GameObject(name);
            layerObj.layer = LayerMask.NameToLayer("UI");
            layerObj.transform.SetParent(parent, false);
            
            RectTransform rect = layerObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            return rect;
        }
    }
}
