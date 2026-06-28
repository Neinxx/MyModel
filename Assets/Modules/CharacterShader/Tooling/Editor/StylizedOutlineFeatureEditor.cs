using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    [CustomEditor(typeof(StylizedOutlineFeature))]
    public sealed class StylizedOutlineFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty _settings;

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var feature = (StylizedOutlineFeature)target;
            DrawFeatureHeader(feature.FeatureName, feature.FeatureCategory, feature.FeatureDescription);

            DrawSection("Injection Point");
            DrawRelative("renderPassEvent", "Event");

            DrawSection("Filtering");
            DrawRelative("layerMask", "Layer Mask");
            DrawRelative("includeSceneView", "Show In Scene View");

            serializedObject.ApplyModifiedProperties();
        }

        private SerializedProperty FindRelative(string name)
        {
            return _settings.FindPropertyRelative(name);
        }

        private void DrawRelative(string name, string label)
        {
            EditorGUILayout.PropertyField(FindRelative(name), new GUIContent(label));
        }

        private static void DrawFeatureHeader(string name, string category, string description)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(category, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(description, MessageType.None);
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
