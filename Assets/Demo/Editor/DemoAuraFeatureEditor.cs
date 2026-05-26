using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ModularDemo.Runtime;
using DecalMini;

namespace ModularDemo.Editor
{
    /// <summary>
    /// 工业级 Demo 光环特性编辑器：与底层 DecalAuraComponentEditor 保持高度一致的 UI 风格与架构
    /// </summary>
    [CustomEditor(typeof(DemoAuraFeature))]
    public class DemoAuraFeatureEditor : DecalEditorBaseMini
    {
        private DemoAuraFeature _target;

        public override VisualElement CreateInspectorGUI()
        {
            _target = (DemoAuraFeature)target;
            _root = new VisualElement();

            // 1. 注入工业级全局样式
            ApplyGlobalStyle(_root);

            // 2. 页眉 (Header)
            CreateHeader(_root);

            // 3. 核心资产卡片 (Gallery & Texture)
            CreateModuleSection(_root);

            // 4. RGBA 层级卡片 (Layer Control)
            CreateLayerSection(_root);

            // 5. 插槽与对齐 (Socket & Snapping)
            CreateSocketSection(_root);
            CreateSnappingSection(_root);

            return _root;
        }

        protected override void OnGridItemClick(Texture2D tex)
        {
            serializedObject.Update();
            var texProp = serializedObject.FindProperty("auraModule.auraTexture");
            if (texProp != null)
            {
                texProp.objectReferenceValue = tex;
                serializedObject.ApplyModifiedProperties();
                RefreshGrid();
            }
        }

        protected override bool IsItemSelected(Texture2D tex)
        {
            return _target != null
                && _target.auraModule != null
                && _target.auraModule.auraTexture == tex;
        }

        private void CreateHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.AddToClassList("decal-header");

            var icon = new VisualElement();
            icon.AddToClassList("decal-header-icon");
            header.Add(icon);

            var title = new Label("Demo Aura Feature (RGBA)");
            title.AddToClassList("decal-header-title");
            header.Add(title);

            root.Add(header);
        }

        private void CreateModuleSection(VisualElement root)
        {
            var card = CreateCard(root, "Core Assets & Gallery");

            // 注入图库
            CreateBaseGUI(card);

            // 基础属性绑定
            card.Add(new PropertyField() { bindingPath = "radius", label = "Aura Radius" });
            card.Add(new PropertyField() { bindingPath = "projectionDepth", label = "Projection Depth" });
            card.Add(new PropertyField() { bindingPath = "auraModule.auraTexture", label = "RGBA Mask Atlas" });

            var settingsRow = new VisualElement();
            settingsRow.AddToClassList("decal-compact-row");
            settingsRow.style.marginTop = 5;
            settingsRow.Add(new PropertyField() { bindingPath = "auraModule.softFade", label = "SOFTNESS" });
            settingsRow.Add(new PropertyField() { bindingPath = "auraModule.pulseIntensity", label = "PULSE INTENSITY" });
            card.Add(settingsRow);
        }

        private void CreateLayerSection(VisualElement root)
        {
            var card = CreateCard(root, "Aura Layers (RGBA Channel Control)");

            AddLayerRow(card, "auraModule.layerR", "R (Red Channel)", new Color(1, 0.4f, 0.4f));
            AddLayerRow(card, "auraModule.layerG", "G (Green Channel)", new Color(0.4f, 1, 0.4f));
            AddLayerRow(card, "auraModule.layerB", "B (Blue Channel)", new Color(0.4f, 0.4f, 1));
            AddLayerRow(card, "auraModule.layerA", "A (Alpha Channel)", new Color(0.9f, 0.9f, 0.9f));
        }

        private void AddLayerRow(VisualElement container, string path, string label, Color themeColor)
        {
            // 1. 容器定义 (Professional Strip Card)
            var strip = new VisualElement();
            strip.AddToClassList("aura-channel-strip");

            // 2. 标题行 (Header Strip)
            var header = new VisualElement();
            header.AddToClassList("aura-channel-header");

            // 通道彩色标识 (Tag)
            var tag = new VisualElement();
            tag.AddToClassList("aura-channel-tag");
            tag.style.backgroundColor = themeColor;
            var tagLabel = new Label(label.Substring(0, 1)); // 只取 R/G/B/A
            tagLabel.AddToClassList("aura-channel-tag-label");
            tag.Add(tagLabel);

            // 颜色预览条 & 拾色器
            var colorField = new PropertyField()
            {
                bindingPath = path + ".color",
                label = "",
                style = { width = 120, marginRight = 4 }
            };
            var colorBar = new VisualElement();
            colorBar.AddToClassList("aura-channel-color-preview");

            // 开关
            var activeToggle = new PropertyField() { bindingPath = path + ".active", label = "" };

            header.Add(tag);
            header.Add(new Label(label)
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = themeColor
                }
            });
            header.Add(colorBar);
            header.Add(colorField);
            header.Add(activeToggle);
            strip.Add(header);

            // 3. 内容区 (Expanded Controls)
            var content = new VisualElement();
            content.AddToClassList("aura-channel-content");

            VisualElement CreateField(string subPath, string subLabel)
            {
                var f = new PropertyField() { bindingPath = path + subPath, label = subLabel };
                f.AddToClassList("aura-compact-field");
                return f;
            }

            content.Add(CreateField(".scale", "SIZE"));
            content.Add(CreateField(".rotationSpeed", "SPIN"));
            content.Add(CreateField(".pulseSpeed", "PULSE"));
            strip.Add(content);

            // 4. 交互逻辑：点击标题折叠
            bool isExpanded = false;
            content.style.display = DisplayStyle.None;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                isExpanded = !isExpanded;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                header.style.backgroundColor = isExpanded
                    ? new Color(1, 1, 1, 0.08f)
                    : new Color(0, 0, 0, 0f);
            });

            // 5. 智能联动：状态同步
            strip.TrackSerializedObjectValue(serializedObject, (so) =>
            {
                var active = so.FindProperty(path + ".active").boolValue;

                // 保持与主组件编辑器完全一致的反馈样式
                strip.style.opacity = active ? 1f : 0.5f;

                tag.SetEnabled(active);
                colorField.SetEnabled(active);
                content.SetEnabled(active);

                tag.style.backgroundColor = active ? themeColor : new Color(0.3f, 0.3f, 0.3f);
                colorBar.style.backgroundColor = active ? themeColor : new Color(0.2f, 0.2f, 0.2f);
            });

            container.Add(strip);
        }

        private void CreateSocketSection(VisualElement root)
        {
            var card = CreateCard(root, "Character Socket");
            var typeField = new PropertyField() { bindingPath = "socketId" };
            var autoSnapField = new PropertyField() { bindingPath = "autoSnapToSocket" };
            card.Add(typeField);
            card.Add(autoSnapField);

            // [智能联动]：仅在 AutoSnap 开启时显示 Socket 类型选择
            root.TrackSerializedObjectValue(serializedObject, (so) =>
            {
                var snap = so.FindProperty("autoSnapToSocket").boolValue;
                typeField.style.display = snap ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }

        private void CreateSnappingSection(VisualElement root)
        {
            var card = CreateCard(root, "Orientation & Snapping");
            var rotField = new PropertyField() { bindingPath = "lockRotation" };
            var stickField = new PropertyField() { bindingPath = "stickToGround" };
            var groundLayerField = new PropertyField() { bindingPath = "groundLayer" };
            var distField = new PropertyField() { bindingPath = "raycastDistance" };
            var offsetField = new PropertyField() { bindingPath = "heightOffset" };

            card.Add(rotField);
            card.Add(stickField);
            card.Add(groundLayerField);
            card.Add(distField);
            card.Add(offsetField);

            // [智能联动]：当 StickToGround 关闭时，隐藏物理相关参数
            root.TrackSerializedObjectValue(serializedObject, (so) =>
            {
                var stick = so.FindProperty("stickToGround").boolValue;
                var display = stick ? DisplayStyle.Flex : DisplayStyle.None;
                groundLayerField.style.display = display;
                distField.style.display = display;
                offsetField.style.display = display;
            });
        }

        private VisualElement CreateCard(VisualElement root, string title)
        {
            var card = new VisualElement();
            card.AddToClassList("decal-card");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("decal-card-title");
            card.Add(titleLabel);

            root.Add(card);
            return card;
        }
    }
}
