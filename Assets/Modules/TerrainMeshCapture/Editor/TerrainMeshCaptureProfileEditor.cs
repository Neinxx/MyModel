using UnityEditor;
using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    [CustomEditor(typeof(TerrainMeshCaptureProfile))]
    public sealed class TerrainMeshCaptureProfileEditor : UnityEditor.Editor
    {
        private static bool showOutputFolders;
        private static bool showAdvancedMesh;
        private static bool showAdvancedTexture;

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
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("assetName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("writeMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("createMaterials"));
            if (serializedObject.FindProperty("createMaterials").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shader"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("createPrefab"));

            showOutputFolders = EditorGUILayout.Foldout(showOutputFolders, "Output Folders", true);
            if (!showOutputFolders)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("meshOutputFolder"));
            if ((TerrainTextureBakeMode)serializedObject.FindProperty("textureBakeMode").intValue != TerrainTextureBakeMode.None)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("textureOutputFolder"));
            }

            if (serializedObject.FindProperty("createMaterials").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("materialOutputFolder"));
            }

            if (serializedObject.FindProperty("createPrefab").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabOutputFolder"));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAreaSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
            SerializedProperty bakeScope = serializedObject.FindProperty("bakeScope");
            EditorGUILayout.PropertyField(bakeScope);

            SerializedProperty areaSize = serializedObject.FindProperty("areaSize");
            SerializedProperty blockSize = serializedObject.FindProperty("blockSize");
            if ((TerrainCaptureBakeScope)bakeScope.intValue == TerrainCaptureBakeScope.SingleArea)
            {
                Vector2Int singleSize = new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(areaSize.vector2Value.x)),
                    Mathf.Max(1, Mathf.RoundToInt(areaSize.vector2Value.y)));
                EditorGUI.BeginChangeCheck();
                singleSize = EditorGUILayout.Vector2IntField("Area Size", singleSize);
                if (EditorGUI.EndChangeCheck())
                {
                    areaSize.vector2Value = new Vector2(
                        Mathf.Max(1, singleSize.x),
                        Mathf.Max(1, singleSize.y));
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty("boundsMode"));
                DrawIntField("Height Offset", serializedObject.FindProperty("heightOffset"), int.MinValue);
                return;
            }

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
            SerializedProperty meshGenerationMode = serializedObject.FindProperty("meshGenerationMode");
            EditorGUILayout.PropertyField(meshGenerationMode);
            DrawSamplesField();
            bool adaptiveTin = (TerrainMeshGenerationMode)meshGenerationMode.intValue == TerrainMeshGenerationMode.AdaptiveHeightTin;
            if (adaptiveTin)
            {
                DrawIntField("Max Triangles", serializedObject.FindProperty("adaptiveMaxTriangles"), 32);
                DrawFloatField("Curvature Threshold", serializedObject.FindProperty("adaptiveCurvatureThreshold"), 0f);
            }

            showAdvancedMesh = EditorGUILayout.Foldout(showAdvancedMesh, "Advanced Mesh", true);
            if (!showAdvancedMesh)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("heightSamplingMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateNormals"), new GUIContent("Normals"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateTangents"), new GUIContent("Tangents"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateUv2"), new GUIContent("UV2"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateSkirts"));
            if (serializedObject.FindProperty("generateSkirts").boolValue)
            {
                DrawIntField("Skirt Depth", serializedObject.FindProperty("skirtDepth"), 0);
            }

            if (adaptiveTin)
            {
                DrawFloatField("Height Error", serializedObject.FindProperty("adaptiveMaxHeightError"), 0f);
                DrawFloatField("Curvature Penalty", serializedObject.FindProperty("adaptiveCurvaturePenalty"), 0f);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawTextureSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
            SerializedProperty textureBakeMode = serializedObject.FindProperty("textureBakeMode");
            EditorGUILayout.PropertyField(textureBakeMode);
            if ((TerrainTextureBakeMode)textureBakeMode.intValue == TerrainTextureBakeMode.None)
            {
                return;
            }

            SerializedProperty textureSizeMode = serializedObject.FindProperty("textureSizeMode");
            EditorGUILayout.PropertyField(textureSizeMode, new GUIContent("Texture Size"));
            string resolutionLabel = (TerrainTextureSizeMode)textureSizeMode.intValue == TerrainTextureSizeMode.MatchAreaAspect
                ? "Long Side"
                : "Resolution";
            DrawIntField(resolutionLabel, serializedObject.FindProperty("textureResolution"), 4);

            showAdvancedTexture = EditorGUILayout.Foldout(showAdvancedTexture, "Advanced Texture", true);
            if (!showAdvancedTexture)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("textureMipMaps"));
            if ((TerrainTextureBakeMode)textureBakeMode.intValue == TerrainTextureBakeMode.Albedo)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackAlbedo"));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawSamplesField()
        {
            SerializedProperty samplesX = serializedObject.FindProperty("samplesX");
            SerializedProperty samplesZ = serializedObject.FindProperty("samplesZ");
            var samples = new Vector2Int(
                Mathf.Max(2, samplesX.intValue),
                Mathf.Max(2, samplesZ.intValue));

            EditorGUI.BeginChangeCheck();
            samples = EditorGUILayout.Vector2IntField("Source Samples X/Z", samples);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            samplesX.intValue = Mathf.Clamp(samples.x, 2, 4097);
            samplesZ.intValue = Mathf.Clamp(samples.y, 2, 4097);
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

        private static void DrawFloatField(string label, SerializedProperty property, float minValue)
        {
            float value = property.floatValue;
            EditorGUI.BeginChangeCheck();
            value = EditorGUILayout.FloatField(label, value);
            if (EditorGUI.EndChangeCheck())
            {
                property.floatValue = Mathf.Max(minValue, value);
            }
        }
    }
}
