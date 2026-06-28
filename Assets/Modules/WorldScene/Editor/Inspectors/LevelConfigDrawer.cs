using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WorldSceneModule.Runtime;

namespace WorldSceneModule.Editor
{
    [CustomPropertyDrawer(typeof(LevelConfig))]
    public class LevelConfigDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
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

            var nameProperty = property.FindPropertyRelative("levelName");
            var sceneProperty = property.FindPropertyRelative("sceneAsset");
            var moduleDataProperty = property.FindPropertyRelative("moduleData");

            var nameField = new PropertyField(nameProperty, "Level Name");
            var sceneField = new PropertyField(sceneProperty, "Scene Asset");
            var moduleDataField = new PropertyField(moduleDataProperty, "Module Data");

            cardContainer.Add(nameField);
            cardContainer.Add(sceneField);
            cardContainer.Add(moduleDataField);

            var embeddedContainer = new VisualElement();
            embeddedContainer.style.marginTop = 6;
            embeddedContainer.style.paddingLeft = 12;
            embeddedContainer.style.borderLeftWidth = 3;
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
                    var serializedModule = new SerializedObject(moduleObj);
                    
                    var subDatasProp = serializedModule.FindProperty("subDatas");
                    if (subDatasProp != null)
                    {
                        var subDatasField = new PropertyField(subDatasProp, "Aggregated Modules (Sub Datas)");
                        embeddedContainer.Add(subDatasField);
                        
                        embeddedContainer.Bind(serializedModule);
                        
                        embeddedContainer.style.display = DisplayStyle.Flex;
                        return;
                    }
                }

                embeddedContainer.style.display = DisplayStyle.None;
            }

            moduleDataField.RegisterValueChangeCallback(evt =>
            {
                UpdateEmbeddedInspector();
            });

            UpdateEmbeddedInspector();

            return cardContainer;
        }
    }
}
