using CharacterPostProcess.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterPostProcess.Editor
{
    [CustomEditor(typeof(AnimePostProcessFeature))]
    public sealed class AnimePostProcessFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty _settings;

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Next-Gen Anime Post Process", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ZZZ / Endfield style multi-layered post-processing for anime characters.", MessageType.Info);

            DrawSection("Injection & Filtering");
            DrawRelative("renderEvent", "Render Event");
            DrawRelative("characterLayer", "Character Layer Mask");
            DrawRelative("includeSceneView", "Include Scene View");
            DrawRelative("shader", "HLSL Shader");

            DrawSection("Dual Kawase Bloom");
            DrawRelative("bloomIterations", "Iterations");
            DrawRelative("bloomRadius", "Radius");
            DrawRelative("bloomThreshold", "Luma Threshold");
            DrawRelative("bloomTint", "Color Tint");

            DrawSection("Screen Space Outline");
            DrawRelative("enableOutline", "Enable Outline");
            if (FindRelative("enableOutline").boolValue)
            {
                EditorGUI.indentLevel++;
                DrawRelative("outlineIntensity", "Intensity");
                DrawRelative("outlineColor", "Line Color");
                DrawRelative("outlineThickness", "Thickness (Px)");
                DrawRelative("depthThreshold", "Depth Threshold");
                DrawRelative("normalThreshold", "Normal Threshold");
                EditorGUI.indentLevel--;
            }

            DrawSection("Cinematic Focus (Ultimate)");
            DrawRelative("enableCinematic", "Enable Focus Mode");
            if (FindRelative("enableCinematic").boolValue)
            {
                EditorGUI.indentLevel++;
                DrawRelative("radialBlurIntensity", "Radial Blur Intensity");
                DrawRelative("radialBlurCenter", "Blur Center (Screen UV)");
                DrawRelative("backgroundDesat", "Background Desaturation");
                EditorGUI.indentLevel--;
            }

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

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
