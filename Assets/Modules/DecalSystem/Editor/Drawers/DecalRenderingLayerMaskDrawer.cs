using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DecalMini
{
    [CustomPropertyDrawer(typeof(DecalRenderingLayerMaskAttribute))]
    public class DecalRenderingLayerMaskDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 尝试获取 URP 资产
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // 获取 URP 定义的图层名称
            string[] names = urpAsset.renderingLayerMaskNames;

            // 绘制原生位掩码选择器
            EditorGUI.BeginProperty(position, label, property);
            
            // 注意：MaskField 使用 int，而 Rendering Layer 使用 uint (32位)
            // 这里我们强转处理，通常 32 位掩码在选择器里是兼容的
            int mask = (int)property.uintValue;
            int newMask = EditorGUI.MaskField(position, label, mask, names);
            
            if (newMask != mask)
            {
                property.uintValue = (uint)newMask;
            }

            EditorGUI.EndProperty();
        }
    }
}
