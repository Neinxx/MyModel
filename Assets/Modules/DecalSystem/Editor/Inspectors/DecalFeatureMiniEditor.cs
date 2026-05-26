using DecalMini;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor
{
    [CustomEditor(typeof(DecalFeatureMini))]
    public sealed class DecalFeatureMiniEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var feature = (DecalFeatureMini)target;
            DrawFeatureHeader(feature.FeatureName, feature.FeatureCategory, feature.FeatureDescription);

            DrawSection("Injection Point");
            Draw("renderEvent", "Event");

            DrawSection("Filtering");
            Draw("decalLayer", "Decal Layer");
            Draw("includeSceneView", "Show In Scene View");

            DrawSection("Resources");
            Draw("atlasConfig", "Atlas");
            Draw("decalShader", "Decal Shader");
            Draw("stencilShader", "Stencil Shader");

            DrawSection("Exclusion");
            Draw("exclusionLayerMask", "Rendering Layer Mask");

            DrawSection("Limits");
            Draw("maxDecals", "Max Decals");
            Draw("maxDrawDistance", "Max Draw Distance");
            Draw("fadeRange", "Fade Range");

            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName, string label)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(propertyName),
                new GUIContent(label)
            );
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
