using CharacterPostProcess.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterPostProcess.Editor
{
    [CustomEditor(typeof(CharacterPostProcessFeature))]
    public sealed class CharacterPostProcessFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty _settings;

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var feature = (CharacterPostProcessFeature)target;
            DrawFeatureHeader(feature.FeatureName, feature.FeatureCategory, feature.FeatureDescription);

            DrawSection("Injection Point");
            DrawRelative("renderEvent", "Event");

            DrawSection("Filtering");
            DrawRelative("characterLayer", "Layer Mask");
            DrawRelative("includeSceneView", "Show In Scene View");

            DrawSection("Resources");
            DrawRelative("shader", "Shader");

            DrawSection("Quality");
            DrawRelative("qualityPreset", "Preset");
            DrawRelative("useCustomQuality", "Custom");
            if (FindRelative("useCustomQuality").boolValue)
            {
                EditorGUI.indentLevel++;
                DrawRelative("downsample", "Downsample");
                DrawRelative("blurIterations", "Blur Iterations");
                DrawRelative("blurRadius", "Blur Radius");
                DrawRelative("bloomThreshold", "Bloom Threshold");
                DrawRelative("bloomTint", "Bloom Tint");
                EditorGUI.indentLevel--;
            }
            else
            {
                DrawRelative("bloomThreshold", "Bloom Threshold");
                DrawRelative("bloomTint", "Bloom Tint");
            }

            DrawSection("Composite");
            DrawRelative("bloomIntensity", "Bloom Intensity");
            DrawRelative("characterColorBoost", "Color Boost");
            DrawRelative("edgeGlowIntensity", "Edge Glow");

            DrawSection("Screen Space Outline");
            DrawRelative("outlineIntensity", "Intensity");
            DrawRelative("outlineColor", "Color");
            DrawRelative("outlineThickness", "Thickness");
            DrawRelative("outlineDepthThreshold", "Depth Threshold");
            DrawRelative("outlineNormalThreshold", "Normal Threshold");

            DrawSection("Debug");
            DrawRelative("debugStencil", "Debug Mask");

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
