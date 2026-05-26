using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini
{
    [CustomEditor(typeof(DecalFootprintComponent))]
    public class DecalFootprintComponentEditor : DecalEditorBaseMini
    {
        private int _selectedSlot = 0; // 0: Left, 1: Right
        private VisualElement _uve_LeftPreview;
        private VisualElement _uve_RightPreview;

        private void OnEnable()
        {
            var comp = target as DecalFootprintComponent;
            if (comp != null && comp.decalPrefab != null)
            {
                // 确保编辑器模式下也能使用对象池
                DecalPoolMini.Init(comp.decalPrefab);
            }
        }

        private void OnSceneGUI()
        {
            var comp = target as DecalFootprintComponent;
            if (comp == null || Application.isPlaying)
                return;

            // 驱动步进模拟
            comp.ManualUpdateEditor();
        }

        private VisualElement _uve_StepContainer;
        private VisualElement _uve_TrackContainer;
        private VisualElement _uve_TrackPreview;

        public override VisualElement CreateInspectorGUI()
        {
            var comp = target as DecalFootprintComponent;
            
            _root = new VisualElement();
            _root.AddToClassList("decal-inspector-root");
            _root.style.paddingTop = 10;

            // 1. 加载样式 (动态解析路径)
            StyleSheet style = DecalMini.Editor.DecalSystemPathUtility.LoadUSS("DecalSystemStyle");
            if (style != null) _root.styleSheets.Add(style);

            StyleSheet editorStyle = DecalMini.Editor.DecalSystemPathUtility.LoadUSS("DecalSystemEditor");
            if (editorStyle != null) _root.styleSheets.Add(editorStyle);

            // 2. 模式选择头部 (卡片式)
            var modeHeader = new VisualElement();
            modeHeader.AddToClassList("inspector-section");
            modeHeader.style.flexDirection = FlexDirection.Row;
            modeHeader.style.justifyContent = Justify.SpaceBetween;
            modeHeader.style.alignItems = Align.Center;
            modeHeader.style.paddingBottom = 8;
            modeHeader.style.borderBottomWidth = 1;
            modeHeader.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            var modeLabel = new Label("Footprint System Mode");
            modeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            modeLabel.style.fontSize = 14;
            modeHeader.Add(modeLabel);

            var modeField = new EnumField(comp.footprintModule.mode);
            modeField.style.width = 120;
            modeField.RegisterValueChangedCallback(evt => {
                serializedObject.FindProperty("footprintModule.mode").enumValueIndex = (int)(FootprintMode)evt.newValue;
                serializedObject.ApplyModifiedProperties();
                UpdateModeVisibility();
            });
            modeHeader.Add(modeField);
            _root.Add(modeHeader);

            // 3. 通用参数区 (Raycast & Layers)
            var commonBox = new VisualElement();
            commonBox.AddToClassList("inspector-section");
            commonBox.style.marginTop = 10;
            commonBox.Add(new PropertyField(serializedObject.FindProperty("triggerMode")));
            commonBox.Add(new PropertyField(serializedObject.FindProperty("groundLayer")));
            _root.Add(commonBox);

            // 4. 共享外观参数 (颜色、透明度、生命周期)
            var appearanceBox = new VisualElement();
            appearanceBox.AddToClassList("inspector-section");
            appearanceBox.style.marginTop = 10;
            
            var visualLabel = new Label(" VISUAL APPEARANCE");
            visualLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            visualLabel.style.marginBottom = 5;
            visualLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            appearanceBox.Add(visualLabel);

            appearanceBox.Add(new PropertyField(serializedObject.FindProperty("footprintModule.tintColor")));
            appearanceBox.Add(new PropertyField(serializedObject.FindProperty("footprintModule.softFade")));
            appearanceBox.Add(new PropertyField(serializedObject.FindProperty("footprintModule.lifeTime")));
            _root.Add(appearanceBox);

            // 5. 核心功能容器
            _uve_StepContainer = new VisualElement();
            _uve_TrackContainer = new VisualElement();
            _root.Add(_uve_StepContainer);
            _root.Add(_uve_TrackContainer);

            // 5. 步进模式 UI 构建
            var stepSlots = new VisualElement();
            stepSlots.AddToClassList("decal-slot-row");
            _uve_LeftPreview = CreateSlotPreview("Left Foot (R)", 0);
            _uve_RightPreview = CreateSlotPreview("Right Foot (G)", 1);
            stepSlots.Add(_uve_LeftPreview);
            stepSlots.Add(_uve_RightPreview);
            _uve_StepContainer.Add(stepSlots);

            var stepParams = new VisualElement();
            stepParams.AddToClassList("inspector-section");
            stepParams.Add(new PropertyField(serializedObject.FindProperty("minStepDistance")));
            stepParams.Add(new PropertyField(serializedObject.FindProperty("footprintModule.stepSideOffset")));
            _uve_StepContainer.Add(stepParams);

            // 6. 车辙模式 UI 构建
            CreateTrackSection(_uve_TrackContainer);

            // 7. 图集网格 (底部共享)
            var gridContainer = new VisualElement();
            gridContainer.style.marginTop = 15;
            gridContainer.name = "uve_GridContainer";
            CreateBaseGUI(gridContainer);
            _root.Add(gridContainer);

            UpdateModeVisibility();
            return _root;
        }

        private void UpdateModeVisibility()
        {
            var comp = target as DecalFootprintComponent;
            if (comp == null || comp.footprintModule == null) return;

            bool isStep = comp.footprintModule.mode == FootprintMode.Step;
            
            // 响应式布局切换
            _uve_StepContainer.style.display = isStep ? DisplayStyle.Flex : DisplayStyle.None;
            _uve_TrackContainer.style.display = isStep ? DisplayStyle.None : DisplayStyle.Flex;

            // 自动修正选中的槽位
            if (!isStep && _selectedSlot < 2) _selectedSlot = 2;
            if (isStep && _selectedSlot >= 2) _selectedSlot = 0;
            
            UpdateSlotVisuals();
            RefreshGrid();
        }

        private void CreateTrackSection(VisualElement container)
        {
            var comp = target as DecalFootprintComponent;
            
            // 轨迹模式头部 (高饱和度蓝色强调)
            var header = new Label(" TRACK MODE SETTINGS");
            header.style.backgroundColor = new Color(0.1f, 0.4f, 0.8f, 0.2f);
            header.style.borderLeftColor = new Color(0.1f, 0.4f, 0.8f, 0.8f);
            header.style.borderLeftWidth = 4;
            header.style.paddingLeft = 5;
            header.style.marginTop = 10;
            header.style.height = 24;
            header.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(header);

            // 车辙槽位预览
            _uve_TrackPreview = CreateSlotPreview("Main Track Texture (B)", 2);
            container.Add(_uve_TrackPreview);

            // 专属参数卡片
            var settings = new VisualElement();
            settings.AddToClassList("inspector-section");
            settings.style.marginTop = 10;
            
            settings.Add(new PropertyField(serializedObject.FindProperty("footprintModule.trackWidth")));
            settings.Add(new PropertyField(serializedObject.FindProperty("footprintModule.trackLength")));
            settings.Add(new PropertyField(serializedObject.FindProperty("footprintModule.sampleInterval")));
            settings.Add(new PropertyField(serializedObject.FindProperty("footprintModule.stretchingWithSpeed")));
            
            // 新增轮迹配置
            settings.Add(new PropertyField(serializedObject.FindProperty("footprintModule.wheelCount")));
            settings.Add(new PropertyField(serializedObject.FindProperty("footprintModule.wheelSpacing")));
            
            container.Add(settings);
        }

        private VisualElement CreateSlotPreview(string headerText, int index)
        {
            var comp = target as DecalFootprintComponent;
            
            var uve_Box = new VisualElement();
            uve_Box.name = $"uve_Slot_{index}";
            uve_Box.AddToClassList("decal-slot-item");

            var uve_Label = new Label(headerText);
            uve_Label.name = "uve_HeaderLabel";
            uve_Label.AddToClassList("decal-slot-label");
            uve_Box.Add(uve_Label);

            var uve_Preview = new VisualElement();
            uve_Preview.name = "uve_PreviewImage";
            uve_Preview.AddToClassList("decal-slot-preview");
            uve_Box.Add(uve_Preview);

            if (index < 2)
            {
                var uve_ParticleField = new ObjectField("")
                {
                    name = "uve_ParticleField",
                    objectType = typeof(ParticleSystem),
                    value = index == 0 ? comp.footprintModule.leftFootParticle : comp.footprintModule.rightFootParticle,
                };
                uve_ParticleField.style.marginTop = 5;
                
                uve_ParticleField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var moduleProp = serializedObject.FindProperty("footprintModule");
                    var psProp = moduleProp.FindPropertyRelative(index == 0 ? "leftFootParticle" : "rightFootParticle");
                    psProp.objectReferenceValue = evt.newValue as Object;
                    serializedObject.ApplyModifiedProperties();
                });
                uve_Box.Add(uve_ParticleField);
            }

            uve_Box.RegisterCallback<MouseDownEvent>(evt =>
            {
                _selectedSlot = index;
                UpdateSlotVisuals();
                RefreshGrid();
            });

            return uve_Box;
        }

        private void UpdateSlotVisuals()
        {
            var comp = target as DecalFootprintComponent;
            if (comp == null || comp.footprintModule == null) return;

            SetSlotState(_uve_LeftPreview, _selectedSlot == 0, comp.footprintModule.leftFootTex);
            SetSlotState(_uve_RightPreview, _selectedSlot == 1, comp.footprintModule.rightFootTex);
            SetSlotState(_uve_TrackPreview, _selectedSlot == 2, comp.footprintModule.trackTexture);
        }

        private void SetSlotState(VisualElement box, bool active, Texture2D tex)
        {
            if (box == null) return;
            box.EnableInClassList("decal-item-btn--selected", active);

            var preview = box.Q<VisualElement>("uve_PreviewImage");
            if (preview != null) preview.style.backgroundImage = tex;
        }

        protected override void OnGridItemClick(Texture2D tex)
        {
            serializedObject.Update();
            var moduleProp = serializedObject.FindProperty("footprintModule");
            string propName = _selectedSlot == 0 ? "leftFootTex" : (_selectedSlot == 1 ? "rightFootTex" : "trackTexture");
            moduleProp.FindPropertyRelative(propName).objectReferenceValue = tex;
            serializedObject.ApplyModifiedProperties();
            UpdateSlotVisuals();
        }

        protected override bool IsItemSelected(Texture2D tex)
        {
            var comp = target as DecalFootprintComponent;
            if (comp.footprintModule == null) return false;
            
            Texture2D currentTex = null;
            if (_selectedSlot == 0) currentTex = comp.footprintModule.leftFootTex;
            else if (_selectedSlot == 1) currentTex = comp.footprintModule.rightFootTex;
            else if (_selectedSlot == 2) currentTex = comp.footprintModule.trackTexture;

            return currentTex == tex;
        }
    }
}
