using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini.Editor
{
    [CustomEditor(typeof(DecalAtlasConfigMini))]
    public class DecalAtlasConfigEditorMini : UnityEditor.Editor
    {
        private VisualElement _root;

        public override VisualElement CreateInspectorGUI()
        {
            // 1. 加载 UXML 结构 (动态解析路径)
            var uxml = DecalMini.Editor.DecalSystemPathUtility.LoadUXML("DecalAtlasConfig");

            if (uxml == null)
                return new Label("UXML 丢失");
            _root = uxml.Instantiate();

            var config = (DecalAtlasConfigMini)target;

            // 3. 绑定统计信息
            var statsLabel = _root.Q<Label>("uve_Stats");
            if (statsLabel != null)
                statsLabel.text = $"{config.Count} Slices | {config.textureSize}px";

            // 4. 绑定目录选择器
            BindFolderField("uve_SourcePath", "sourcePath");
            BindFolderField("uve_ExportPath", "exportPath");

            // 5. 绑定按钮逻辑
            var autoBtn = _root.Q<Button>("uve_AutoBakeBtn");
            if (autoBtn != null)
                autoBtn.clicked += () =>
                {
                    DecalAtlasBakingToolMini.AutoCollectAndBake(config);
                    config.RebuildCache();
                    serializedObject.Update();
                };

            var slimBtn = _root.Q<Button>("uve_SlimBakeBtn");
            if (slimBtn != null)
                slimBtn.clicked += () =>
                {
                    DecalAtlasBakingToolMini.BakeWithSlimming(config);
                    config.RebuildCache();
                    serializedObject.Update();
                };

            return _root;
        }

        private void BindFolderField(string elementName, string propertyName)
        {
            var field = _root.Q<ObjectField>(elementName);
            if (field == null)
                return;

            var prop = serializedObject.FindProperty(propertyName);
            field.objectType = typeof(DefaultAsset);

            if (!string.IsNullOrEmpty(prop.stringValue))
                field.value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(prop.stringValue);

            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null)
                {
                    string path = AssetDatabase.GetAssetPath(evt.newValue);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        prop.stringValue = path;
                        serializedObject.ApplyModifiedProperties();
                    }
                    else
                        field.value = evt.previousValue;
                }
                else
                {
                    prop.stringValue = "";
                    serializedObject.ApplyModifiedProperties();
                }
            });
        }
    }
}
