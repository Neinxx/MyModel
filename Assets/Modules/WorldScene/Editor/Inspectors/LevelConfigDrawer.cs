using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WorldSceneModule.Runtime;

namespace WorldSceneModule.Editor
{
    /// <summary>
    /// 关卡配置 (LevelConfig) 的专属属性绘制器。
    /// 🚀 采用了极具黑科技感的“嵌入式子级属性面板 (Inline Sub-Inspector)”设计！
    /// 自动在当前关卡元素下方，直接内嵌展示其关联的 LevelModuleData 聚合容器面板，极大地优化了关卡策划的编辑流体验。
    /// </summary>
    [CustomPropertyDrawer(typeof(LevelConfig))]
    public class LevelConfigDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // 1. 创建外层卡片容器，赋予现代简约的卡片式美学背景与微调圆角
            var cardContainer = new VisualElement();
            cardContainer.style.marginTop = 6;
            cardContainer.style.marginBottom = 6;
            cardContainer.style.paddingLeft = 8;
            cardContainer.style.paddingRight = 8;
            cardContainer.style.paddingTop = 8;
            cardContainer.style.paddingBottom = 8;
            cardContainer.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.35f);
            cardContainer.style.borderTopWidth = 1;
            cardContainer.style.borderBottomWidth = 1;
            cardContainer.style.borderLeftWidth = 1;
            cardContainer.style.borderRightWidth = 1;

            var borderColorVal = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            cardContainer.style.borderTopColor = borderColorVal;
            cardContainer.style.borderBottomColor = borderColorVal;
            cardContainer.style.borderLeftColor = borderColorVal;
            cardContainer.style.borderRightColor = borderColorVal;

            cardContainer.style.borderTopLeftRadius = 6;
            cardContainer.style.borderTopRightRadius = 6;
            cardContainer.style.borderBottomLeftRadius = 6;
            cardContainer.style.borderBottomRightRadius = 6;

            // 2. 检索并解析 LevelConfig 的各个成员属性
            var nameProperty = property.FindPropertyRelative("levelName");
            var sceneProperty = property.FindPropertyRelative("sceneAsset");
            var moduleDataProperty = property.FindPropertyRelative("moduleData");

            // 3. 构建高精度的属性字段控件
            var nameField = new PropertyField(nameProperty, "Level Name");
            var sceneField = new PropertyField(sceneProperty, "Scene Asset");
            var moduleDataField = new PropertyField(moduleDataProperty, "Module Data");

            cardContainer.Add(nameField);
            cardContainer.Add(sceneField);
            cardContainer.Add(moduleDataField);

            // 4. 🚀 嵌入式属性面板容器 (Embedded Sub-Inspector Container)
            var embeddedContainer = new VisualElement();
            embeddedContainer.style.marginTop = 6;
            embeddedContainer.style.paddingLeft = 12;
            embeddedContainer.style.borderLeftWidth = 3;
            // 采用科技感十足的传送门蓝色 (Portal Blue) 左边栏，作为高内聚模块数据的边界视觉指引
            embeddedContainer.style.borderLeftColor = new Color(0.486f, 0.549f, 1.0f, 0.85f);
            embeddedContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.25f);
            embeddedContainer.style.paddingTop = 4;
            embeddedContainer.style.paddingBottom = 4;
            embeddedContainer.style.borderTopLeftRadius = 2;
            embeddedContainer.style.borderTopRightRadius = 2;
            embeddedContainer.style.borderBottomLeftRadius = 2;
            embeddedContainer.style.borderBottomRightRadius = 2;

            cardContainer.Add(embeddedContainer);

            void UpdateEmbeddedInspector()
            {
                embeddedContainer.Clear();

                var moduleObj = moduleDataProperty.objectReferenceValue as LevelModuleData;
                if (moduleObj != null)
                {
                    // 🚀 核心修复：创建独立的 SerializedObject 以彻底隔离上下文
                    var serializedModule = new SerializedObject(moduleObj);
                    
                    // 获取 subDatas 属性并构建独立的 PropertyField
                    var subDatasProp = serializedModule.FindProperty("subDatas");
                    if (subDatasProp != null)
                    {
                        var subDatasField = new PropertyField(subDatasProp, "Aggregated Modules (Sub Datas)");
                        embeddedContainer.Add(subDatasField);
                        
                        // 🚀 核心关键：使用 UI Toolkit 官方的 Bind 机制显式绑定子级 SerializedObject
                        embeddedContainer.Bind(serializedModule);
                        
                        embeddedContainer.style.display = DisplayStyle.Flex;
                        return;
                    }
                }

                // 无数据资产绑定时隐藏占位
                embeddedContainer.style.display = DisplayStyle.None;
            }

            // 6. 绑定值变更监听，使嵌入面板能在拖入/清除资产时瞬间重绘自愈
            moduleDataField.RegisterValueChangeCallback(evt =>
            {
                UpdateEmbeddedInspector();
            });

            // 7. 首次初载重绘渲染
            UpdateEmbeddedInspector();

            return cardContainer;
        }
    }
}
