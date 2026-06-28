using UnityEditor;
using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    [CustomEditor(typeof(TerrainMeshCaptureProfile))]
    public sealed class TerrainMeshCaptureProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawAssetSection();
            DrawAreaSection();
            DrawMeshSection();
            DrawTextureSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetSection()
        {
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("assetName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("writeMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("meshOutputFolder"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("textureOutputFolder"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialOutputFolder"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabOutputFolder"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shader"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("createMaterials"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("createPrefab"));
        }

        private void DrawAreaSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bakeScope"));

            SerializedProperty areaSize = serializedObject.FindProperty("areaSize");
            SerializedProperty blockSize = serializedObject.FindProperty("blockSize");
            int squareSize = Mathf.Max(1, Mathf.RoundToInt(blockSize.vector2Value.x));
            Vector2Int blockCounts = new Vector2Int(
                TerrainMeshCaptureProfile.GetBlockCount(areaSize.vector2Value.x, squareSize),
                TerrainMeshCaptureProfile.GetBlockCount(areaSize.vector2Value.y, squareSize));

            EditorGUI.BeginChangeCheck();
            squareSize = EditorGUILayout.IntField("Block Size", squareSize);
            if (EditorGUI.EndChangeCheck())
            {
                squareSize = Mathf.Max(1, squareSize);
                blockSize.vector2Value = new Vector2(squareSize, squareSize);
                areaSize.vector2Value = new Vector2(blockCounts.x * squareSize, blockCounts.y * squareSize);
            }

            EditorGUI.BeginChangeCheck();
            blockCounts = EditorGUILayout.Vector2IntField("Blocks X/Z", blockCounts);
            if (EditorGUI.EndChangeCheck())
            {
                blockCounts.x = Mathf.Max(1, blockCounts.x);
                blockCounts.y = Mathf.Max(1, blockCounts.y);
                areaSize.vector2Value = new Vector2(blockCounts.x * squareSize, blockCounts.y * squareSize);
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2IntField(
                    "Area Size",
                    new Vector2Int(
                        Mathf.RoundToInt(areaSize.vector2Value.x),
                        Mathf.RoundToInt(areaSize.vector2Value.y)));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("boundsMode"));
            DrawIntField("Height Offset", serializedObject.FindProperty("heightOffset"), int.MinValue);
        }

        private void DrawMeshSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("samplesX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("samplesZ"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("heightSamplingMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateNormals"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateTangents"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateUv2"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateSkirts"));
            if (serializedObject.FindProperty("generateSkirts").boolValue)
            {
                DrawIntField("Skirt Depth", serializedObject.FindProperty("skirtDepth"), 0);
            }
        }

        private void DrawTextureSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("textureBakeMode"));
            DrawIntField("Texture Resolution", serializedObject.FindProperty("textureResolution"), 4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("textureMipMaps"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackAlbedo"));
        }

        private static void DrawIntField(string label, SerializedProperty property, int minValue)
        {
            int value = property.propertyType == SerializedPropertyType.Integer
                ? property.intValue
                : Mathf.RoundToInt(property.floatValue);
            EditorGUI.BeginChangeCheck();
            value = EditorGUILayout.IntField(label, value);
            if (EditorGUI.EndChangeCheck())
            {
                value = Mathf.Max(minValue, value);
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    property.intValue = value;
                    return;
                }

                property.floatValue = value;
            }
        }
    }
}
