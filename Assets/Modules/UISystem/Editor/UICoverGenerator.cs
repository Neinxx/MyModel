using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UISystem.Runtime;

namespace UISystem.Editor
{
    public static class UICoverGenerator
    {
        [MenuItem("GameObject/UI System/Cover UI (AAA Standard)", false, 10)]
        public static void GenerateCoverUI(MenuCommand menuCommand)
        {
            // 1. Ensure Canvas and UIManager
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UI_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                
                // Add UIManager if it doesn't exist
                if (Object.FindFirstObjectByType<UIManager>() == null)
                {
                    canvasObj.AddComponent<UIManager>();
                }
            }

            // 2. Ensure EventSystem
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // 3. Create Cover View Container
            GameObject coverObj = new GameObject("View_Cover");
            coverObj.transform.SetParent(canvas.transform, false);
            
            RectTransform coverRect = coverObj.AddComponent<RectTransform>();
            coverRect.anchorMin = Vector2.zero;
            coverRect.anchorMax = Vector2.one;
            coverRect.sizeDelta = Vector2.zero;
            
            // Add UIView components
            var canvasGroup = coverObj.AddComponent<CanvasGroup>();
            var uiView = coverObj.AddComponent<GenericUIView>();
            uiView.viewID = "Cover";
            uiView.hideOnAwake = false; // Cover is usually visible on boot

            // 4. Create Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(coverObj.transform, false);
            
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f); // Sleek dark gray

            // 5. Create Title Text
            GameObject titleObj = new GameObject("Text_Title");
            titleObj.transform.SetParent(coverObj.transform, false);
            
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.sizeDelta = new Vector2(1000, 200);
            
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "PROJECT TITLE";
            titleText.fontSize = 120;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // 6. Create Start Prompt Text
            GameObject promptObj = new GameObject("Text_Prompt");
            promptObj.transform.SetParent(coverObj.transform, false);
            
            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.2f);
            promptRect.anchorMax = new Vector2(0.5f, 0.2f);
            promptRect.sizeDelta = new Vector2(600, 100);
            
            TextMeshProUGUI promptText = promptObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "- PRESS ANY KEY TO START -";
            promptText.fontSize = 40;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(1, 1, 1, 0.6f);

            // 7. Add simple pulse animation to prompt
            var animator = promptObj.AddComponent<Animator>(); // User can add an animation controller later

            // Register Undo
            Undo.RegisterCreatedObjectUndo(coverObj, "Create Cover UI");
            Selection.activeGameObject = coverObj;
            
            Debug.Log("<color=#3FB950><b>[UICoverGenerator]</b></color> Successfully generated AAA Cover UI!");
        }
    }
}
