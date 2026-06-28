using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UISystem.Runtime;
using ModularDemo.Runtime;

namespace ModularDemo.Editor
{
    /// <summary>
    /// 一键运行时贴花调试 UI 生成器 (Modular Decal DebugUI Generator)
    /// 纯 C# 代码驱动像素级构建 UGUI 界面体系，完美对接 UISystem 生命周期，零 Prefab 依赖，即插即用。
    /// </summary>
    public static class DecalDebugTool
    {
        private const string MENU_PATH = "Tools/Decal System/Create Decal Debug UI";

        [MenuItem(MENU_PATH)]
        public static void CreateDecalDebugUI()
        {
            // 1. 确保场景中具备 EventSystem 环境
            var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (eventSystem == null)
            {
                var esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                
                if (inputModuleType != null)
                {
                    esObj.AddComponent(inputModuleType);
                    Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 已自动布设包含 InputSystemUIInputModule 的 EventSystem (新输入系统环境)");
                }
                else
                {
                    esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 已自动布设包含 StandaloneInputModule 的 EventSystem (旧输入系统环境)");
                }
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }
            else
            {
                // 如果已存在 EventSystem，检查是否缺少 InputModule
                var inputModule = eventSystem.GetComponent<UnityEngine.EventSystems.BaseInputModule>();
                if (inputModule == null)
                {
                    if (inputModuleType != null)
                    {
                        eventSystem.gameObject.AddComponent(inputModuleType);
                        Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 场景已存在 EventSystem 但缺少 InputModule，已自适应补齐 InputSystemUIInputModule");
                    }
                    else
                    {
                        eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                        Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 场景已存在 EventSystem 但缺少 InputModule，已自适应补齐 StandaloneInputModule");
                    }
                }
                else if (inputModuleType != null && inputModule.GetType().Name == "StandaloneInputModule")
                {
                    // 如果存在老的 StandaloneInputModule，在新输入系统环境下可能导致 UI 无法点击交互
                    // 我们利用 Undo 和 Destroy 将其安全、动态替换为 InputSystemUIInputModule，确保 100% 交互成功
                    Undo.DestroyObjectImmediate(inputModule);
                    var newModule = eventSystem.gameObject.AddComponent(inputModuleType);
                    Undo.RegisterCreatedObjectUndo(newModule, "Replace StandaloneInputModule with InputSystemUIInputModule");
                    Debug.Log("<color=#BC8CFF><b>[DecalDebugTool]</b></color> 检测到新输入系统环境，已自动将老旧 StandaloneInputModule 替换为 InputSystemUIInputModule");
                }
            }

            // 2. 确保场景中具备 CameraUI/UICamera 专属摄像机
            var uiCameraObj = GameObject.Find("CameraUI") ?? GameObject.Find("UICamera");
            Camera uiCamera = null;
            if (uiCameraObj == null)
            {
                uiCameraObj = new GameObject("CameraUI", typeof(Camera));
                uiCamera = uiCameraObj.GetComponent<Camera>();

                // 移除多余的 AudioListener 避免报警
                var listener = uiCameraObj.GetComponent<AudioListener>();
                if (listener != null) Undo.DestroyObjectImmediate(listener);

                // 基本正交设置
                uiCamera.clearFlags = CameraClearFlags.Depth;
                uiCamera.orthographic = true;
                uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");

                Undo.RegisterCreatedObjectUndo(uiCameraObj, "Create CameraUI");
                Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 已自动部署专属 UI 渲染摄像机 CameraUI");
            }
            else
            {
                uiCamera = uiCameraObj.GetComponent<Camera>();
            }

            // 🌟 核心优雅发现机制：给 UI 摄像机挂载 UICameraRegisterHook 触发自发现与堆叠，无反射、高耦合隔离
            var registerHook = uiCameraObj.GetComponent<CameraSystem.Runtime.UICameraRegisterHook>();
            if (registerHook == null)
            {
                registerHook = uiCameraObj.AddComponent<CameraSystem.Runtime.UICameraRegisterHook>();
                Undo.RegisterCreatedObjectUndo(registerHook, "Add UICameraRegisterHook");
            }

            // 3. 确保场景中具备 UIManager (Canvas)
            var uiManager = Object.FindAnyObjectByType<UIManager>();
            Canvas canvas = null;
            if (uiManager == null)
            {
                var canvasObj = new GameObject("DecalDebugCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 2.0f;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                uiManager = canvasObj.AddComponent<UIManager>();

                Undo.RegisterCreatedObjectUndo(canvasObj, "Create UIManager Canvas");
                Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 已自动部署全局 UIManager & Canvas (Camera Space)");
            }
            else
            {
                canvas = uiManager.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = uiManager.gameObject.AddComponent<Canvas>();
                }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 2.0f;
            }

            // Ensure the always-active hotkey manager is present on the Canvas/UIManager object
            var hotkeyManager = canvas.GetComponent<DecalDebugHotkeyManager>();
            if (hotkeyManager == null)
            {
                hotkeyManager = canvas.gameObject.AddComponent<DecalDebugHotkeyManager>();
                Undo.RegisterCreatedObjectUndo(hotkeyManager, "Create DecalDebugHotkeyManager");
            }

            // 3. 防重入保护：如果已存在，安全销毁旧界面以重新构建最纯净、最新的绑定
            var existingView = canvas.GetComponentInChildren<DecalDebugView>(true);
            if (existingView != null)
            {
                Undo.DestroyObjectImmediate(existingView.gameObject);
                Debug.Log("<color=#BC8CFF><b>[DecalDebugTool]</b></color> 检测到已存在 DecalDebugView，已将其安全替换重建。");
            }

            // 4. 像素级纯代码拼装 UGUI 场景树
            var rootObj = new GameObject("DecalDebugView", typeof(RectTransform));
            rootObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(rootObj, "Create Decal Debug View");

            // UIView 必须拥有 CanvasGroup
            var canvasGroup = rootObj.AddComponent<CanvasGroup>();
            var debugView = rootObj.AddComponent<DecalDebugView>();
            debugView.SetViewId("DecalDebugView");
            debugView.SetHideOnAwake(true);
            debugView.SetFadeDuration(0.25f);

            // 全屏拉伸 rootObj
            SetRectOffsets(rootObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // A. Frost Glass Dark Panel 背景
            var panelObj = CreateUIObject("Panel", rootObj.transform);
            AddImage(panelObj, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            var outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.35f, 0.45f, 0.25f);
            outline.effectDistance = new Vector2(1, -1);
            // 靠右侧摆放面板 (宽 450, 高 850)
            SetRect(panelObj, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-25f, 0f), new Vector2(450f, 850f));

            // B. Header 标题区
            var headerObj = CreateUIObject("Header", panelObj.transform);
            SetRectOffsets(headerObj, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(20f, -70f), new Vector2(-20f, -15f));
            
            var titleTextObj = CreateUIObject("TitleText", headerObj.transform);
            SetRectOffsets(titleTextObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(titleTextObj, "DECAL SYSTEM TELEMETRY", 18, new Color(0.85f, 0.9f, 1.0f, 1.0f), TextAnchor.MiddleLeft, true);

            var dividerObj = CreateUIObject("Divider", headerObj.transform);
            SetRectOffsets(dividerObj, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f));
            AddImage(dividerObj, new Color(0.3f, 0.35f, 0.45f, 0.4f));

            // C. Telemetry Metrics 遥测指标卡
            var metricsObj = CreateUIObject("TelemetryMetrics", panelObj.transform);
            SetRectOffsets(metricsObj, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(20f, -210f), new Vector2(-20f, -80f));
            AddImage(metricsObj, new Color(0.04f, 0.05f, 0.07f, 0.85f));
            var metricsOutline = metricsObj.AddComponent<Outline>();
            metricsOutline.effectColor = new Color(0.3f, 0.35f, 0.45f, 0.15f);
            metricsOutline.effectDistance = new Vector2(1, -1);

            var metricsLayout = metricsObj.AddComponent<VerticalLayoutGroup>();
            metricsLayout.padding = new RectOffset(16, 16, 12, 12);
            metricsLayout.spacing = 8;
            metricsLayout.childControlHeight = true;
            metricsLayout.childForceExpandHeight = false;
            metricsLayout.childControlWidth = true;
            metricsLayout.childForceExpandWidth = true;

            var totalCountText = AddMetricText("TotalCountText", metricsObj.transform, "Total Decals: 0");
            var activeProjectorsText = AddMetricText("ActiveProjectorsText", metricsObj.transform, "Active Projectors: 0");
            var gridCellsText = AddMetricText("GridCellsText", metricsObj.transform, "Spatial Cells (Active): 0");

            // D. Scroll List (贴花列表)
            var scrollObj = CreateUIObject("ScrollView", panelObj.transform);
            // 位于遥测区与底部操作区之间
            SetRectOffsets(scrollObj, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(20f, 140f), new Vector2(-20f, -220f));
            AddImage(scrollObj, new Color(0.04f, 0.05f, 0.07f, 0.5f));
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25f;

            var viewportObj = CreateUIObject("Viewport", scrollObj.transform);
            SetRectOffsets(viewportObj, Vector2.zero, Vector2.one, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;
            AddImage(viewportObj, Color.white); // Mask 需要有 Image 图形才能生效

            var contentObj = CreateUIObject("Content", viewportObj.transform);
            var contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(6, 6, 6, 6);
            contentLayout.spacing = 6;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportObj.GetComponent<RectTransform>();
            scrollRect.content = contentRect;

            // E. Item Template (滚动元素模板)
            var itemTemplate = CreateUIObject("ItemTemplate", contentObj.transform);
            SetRect(itemTemplate, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 65f));
            AddImage(itemTemplate, new Color(0.12f, 0.14f, 0.18f, 0.9f));
            var itemOutline = itemTemplate.AddComponent<Outline>();
            itemOutline.effectColor = new Color(0.3f, 0.35f, 0.45f, 0.15f);
            itemOutline.effectDistance = new Vector2(1, -1);

            // Item 子物件: Label
            var labelObj = CreateUIObject("Label", itemTemplate.transform);
            SetRectOffsets(labelObj, new Vector2(0f, 0.5f), new Vector2(0.65f, 1f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(0f, -4f));
            AddText(labelObj, "#01 - DecalProjector", 13, new Color(0.9f, 0.93f, 0.98f, 1.0f), TextAnchor.MiddleLeft, true);

            // Item 子物件: PosLabel
            var posLabelObj = CreateUIObject("PosLabel", itemTemplate.transform);
            SetRectOffsets(posLabelObj, new Vector2(0f, 0f), new Vector2(0.65f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 4f), new Vector2(0f, 0f));
            AddText(posLabelObj, "Pos: (0.0, 0.0, 0.0)", 11, new Color(0.6f, 0.65f, 0.75f, 1.0f), TextAnchor.MiddleLeft, false);

            // Item 交互按钮：TP (Teleport)
            var tpBtnObj = CreateUIObject("TeleportBtn", itemTemplate.transform);
            SetRectOffsets(tpBtnObj, new Vector2(0.66f, 0.15f), new Vector2(0.76f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddButton(tpBtnObj, new Color(0.22f, 0.25f, 0.35f, 1.0f));
            var tpBtnOutline = tpBtnObj.AddComponent<Outline>();
            tpBtnOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            tpBtnOutline.effectDistance = new Vector2(1, -1);
            var tpLabel = CreateUIObject("Text", tpBtnObj.transform);
            SetRectOffsets(tpLabel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(tpLabel, "TP", 11, Color.white, TextAnchor.MiddleCenter, true);

            // Item 交互按钮：Flash
            var flashBtnObj = CreateUIObject("FlashBtn", itemTemplate.transform);
            SetRectOffsets(flashBtnObj, new Vector2(0.77f, 0.15f), new Vector2(0.87f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddButton(flashBtnObj, new Color(0.35f, 0.25f, 0.45f, 1.0f));
            var flashBtnOutline = flashBtnObj.AddComponent<Outline>();
            flashBtnOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            flashBtnOutline.effectDistance = new Vector2(1, -1);
            var flashLabel = CreateUIObject("Text", flashBtnObj.transform);
            SetRectOffsets(flashLabel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(flashLabel, "FL", 11, Color.white, TextAnchor.MiddleCenter, true);

            // Item 交互按钮：Destroy
            var destroyBtnObj = CreateUIObject("DestroyBtn", itemTemplate.transform);
            SetRectOffsets(destroyBtnObj, new Vector2(0.88f, 0.15f), new Vector2(0.98f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddButton(destroyBtnObj, new Color(0.55f, 0.18f, 0.2f, 1.0f));
            var destroyBtnOutline = destroyBtnObj.AddComponent<Outline>();
            destroyBtnOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            destroyBtnOutline.effectDistance = new Vector2(1, -1);
            var destroyLabel = CreateUIObject("Text", destroyBtnObj.transform);
            SetRectOffsets(destroyLabel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(destroyLabel, "X", 11, Color.white, TextAnchor.MiddleCenter, true);

            // F. Footer Operations Panel (底部操作栏)
            var footerObj = CreateUIObject("Footer", panelObj.transform);
            SetRectOffsets(footerObj, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(20f, 15f), new Vector2(-20f, 125f));
            AddImage(footerObj, new Color(0.04f, 0.05f, 0.07f, 0.85f));
            var footerOutline = footerObj.AddComponent<Outline>();
            footerOutline.effectColor = new Color(0.3f, 0.35f, 0.45f, 0.15f);
            footerOutline.effectDistance = new Vector2(1, -1);

            var footerLayout = footerObj.AddComponent<HorizontalLayoutGroup>();
            footerLayout.padding = new RectOffset(8, 8, 10, 10);
            footerLayout.spacing = 8;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandHeight = true;
            footerLayout.childControlWidth = true;
            footerLayout.childForceExpandWidth = true;

            // Spawn Btn
            var spawnBtnObj = CreateUIObject("SpawnBtn", footerObj.transform);
            AddButton(spawnBtnObj, new Color(0.18f, 0.45f, 0.25f, 1.0f));
            var spawnBtnOutline = spawnBtnObj.AddComponent<Outline>();
            spawnBtnOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            var spawnLabelObj = CreateUIObject("Text", spawnBtnObj.transform);
            SetRectOffsets(spawnLabelObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(spawnLabelObj, "Spawn Decal", 12, Color.white, TextAnchor.MiddleCenter, true);

            // Clear Btn
            var clearBtnObj = CreateUIObject("ClearBtn", footerObj.transform);
            AddButton(clearBtnObj, new Color(0.5f, 0.2f, 0.2f, 1.0f));
            var clearBtnOutline = clearBtnObj.AddComponent<Outline>();
            clearBtnOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            var clearLabelObj = CreateUIObject("Text", clearBtnObj.transform);
            SetRectOffsets(clearLabelObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(clearLabelObj, "Clear Dynamic", 12, Color.white, TextAnchor.MiddleCenter, true);

            // Clean Shader Injections Btn
            var cleanBtnObj = CreateUIObject("CleanInjectionsBtn", footerObj.transform);
            AddButton(cleanBtnObj, new Color(0.35f, 0.22f, 0.5f, 1.0f));
            var cleanBtnOutline = cleanBtnObj.AddComponent<Outline>();
            cleanBtnOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
            var cleanLabelObj = CreateUIObject("Text", cleanBtnObj.transform);
            SetRectOffsets(cleanLabelObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(cleanLabelObj, "Clean Shader", 12, Color.white, TextAnchor.MiddleCenter, true);

            // 5. 绑定序列化引用给 DecalDebugView 组件
            debugView.totalCountText = totalCountText;
            debugView.activeProjectorsText = activeProjectorsText;
            debugView.gridCellsText = gridCellsText;
            debugView.scrollRect = scrollRect;
            debugView.listContent = contentRect;
            debugView.itemTemplate = itemTemplate;

            // 6. 标记修改并同步视图保存
            EditorUtility.SetDirty(debugView);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = rootObj;
            Debug.Log("<color=#3FB950><b>[DecalDebugTool]</b></color> 成功在当前场景中创建并自适应装载了 DecalDebugView，全部引用字段已自动完成高鲁棒性绑定！");
        }

        #region Helpers
        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text AddText(GameObject go, string content, int fontSize, Color color, TextAnchor alignment, bool bold)
        {
            var text = go.AddComponent<Text>();
            text.text = content;
            
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                var loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
                if (loadedFonts != null && loadedFonts.Length > 0)
                {
                    font = loadedFonts[0];
                }
            }
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            if (bold)
            {
                text.fontStyle = FontStyle.Bold;
            }
            return text;
        }

        private static Text AddMetricText(string name, Transform parent, string placeholder)
        {
            var go = CreateUIObject(name, parent);
            return AddText(go, placeholder, 14, new Color(0.35f, 0.85f, 0.45f, 1.0f), TextAnchor.MiddleLeft, false);
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Button AddButton(GameObject go, Color normalColor)
        {
            var btn = go.AddComponent<Button>();
            var img = go.GetComponent<Image>();
            if (img == null)
            {
                img = go.AddComponent<Image>();
            }
            img.color = normalColor;

            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = normalColor * 1.25f;
            colors.pressedColor = normalColor * 0.75f;
            colors.selectedColor = normalColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            btn.colors = colors;

            return btn;
        }

        private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
            {
                rt = go.AddComponent<RectTransform>();
            }
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        private static void SetRectOffsets(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
            {
                rt = go.AddComponent<RectTransform>();
            }
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
        #endregion
    }
}
