using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini
{
    /// <summary>
    /// 工业级投影器编辑器：支持 UXML 模板注入与零延迟属性可见性追踪
    /// </summary>
    [CustomEditor(typeof(DecalProjectorMini))]
    [CanEditMultipleObjects]
    public class DecalProjectorEditorMini : DecalEditorBaseMini
    {
        public override VisualElement CreateInspectorGUI()
        {
            // 1. 加载并初始化布局
            var uxml = DecalMini.Editor.DecalSystemPathUtility.LoadUXML("DecalProjector");
            if (uxml == null) return new Label("UXML Template Missing (DecalProjector.uxml)");

            _root = uxml.Instantiate();
            
            // 2. 注入工业级全局样式 (USS)
            ApplyGlobalStyle(_root);

            // 3. 注入图库网格 (Gallery Grid)
            var gridSlot = _root.Q<VisualElement>("uve_GridContainer");
            if (gridSlot != null) CreateBaseGUI(gridSlot);

            // 4. 动态 UI 追踪逻辑 (Industrial Practice: TrackSerializedObjectValue)
            SetupDynamicVisibility();

            // 5. 禁用手动输入贴图（强制通过图库选择以保证图集性能）
            var texField = _root.Q<PropertyField>("uve_TextureField");
            if (texField != null) texField.SetEnabled(false);

            return _root;
        }

        private void SetupDynamicVisibility()
        {
            // 追踪脉冲特效开关
            var pulseProp = serializedObject.FindProperty("pulseEffect");
            if (pulseProp != null)
            {
                _root.TrackSerializedObjectValue(serializedObject, _ => UpdateVisibility());
            }

            // 追踪径向遮罩开关
            var maskProp = serializedObject.FindProperty("useRadialMask");
            if (maskProp != null)
            {
                _root.TrackSerializedObjectValue(serializedObject, _ => UpdateVisibility());
            }

            // 初始执行一次同步
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var proj = target as DecalProjectorMini;
            if (proj == null || _root == null) return;

            // 使用 .style.display 切换模块可见性，不销毁元素，保持 UI 性能
            var shapeSettings = _root.Q<VisualElement>("uve_ShapeSettings");
            if (shapeSettings != null)
            {
                shapeSettings.style.display = proj.useRadialMask ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var pulseSettings = _root.Q<VisualElement>("uve_PulseSettings");
            if (pulseSettings != null)
            {
                pulseSettings.style.display = proj.pulseEffect ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected override void OnGridItemClick(Texture2D tex)
        {
            serializedObject.Update();
            var texProp = serializedObject.FindProperty("decalTexture");
            if (texProp != null)
            {
                texProp.objectReferenceValue = tex;
                serializedObject.ApplyModifiedProperties();
                
                // 工业级补丁：修改贴图后强制刷新整个网格的选中状态
                RefreshGrid();
            }
        }

        protected override bool IsItemSelected(Texture2D tex)
        {
            var proj = target as DecalProjectorMini;
            return proj != null && proj.decalTexture == tex;
        }
    }
}
