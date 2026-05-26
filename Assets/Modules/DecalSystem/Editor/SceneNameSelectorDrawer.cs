using System.Linq;
using DecalMini;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor
{
    /// <summary>
    /// 场景选择器绘制器
    /// 自动发现项目中的场景提供者并显示下拉菜单
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneNameSelectorAttribute))]
    public class SceneNameSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [SceneNameSelector] with string.");
                return;
            }

            // 1. 核心发现引擎：尝试从所有外部提供者获取场景列表 (业务优先)
            var optionsList = new System.Collections.Generic.List<string>();
            foreach (var provider in DecalEditorExtensions.SceneProviders)
            {
                var names = provider.GetSceneNames();
                if (names != null) optionsList.AddRange(names);
            }

            string[] options = optionsList.Distinct().ToArray();

            // 2. 如果没有提供者返回结果，则回退到扫描项目中所有的场景资产 (保证独立工作)
            if (options.Length == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
                if (guids.Length == 0)
                {
                    EditorGUI.PropertyField(position, property, label);
                    return;
                }

                var fallbackList = new System.Collections.Generic.List<string>();
                foreach (var guid in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    fallbackList.Add(System.IO.Path.GetFileNameWithoutExtension(p));
                }
                options = fallbackList.ToArray();
            }

            // 3. 显示下拉菜单
            int currentIndex = System.Array.IndexOf(options, property.stringValue);
            if (currentIndex < 0)
                currentIndex = 0;

            currentIndex = EditorGUI.Popup(position, label.text, currentIndex, options);
            property.stringValue = options[currentIndex];
        }
    }
}
