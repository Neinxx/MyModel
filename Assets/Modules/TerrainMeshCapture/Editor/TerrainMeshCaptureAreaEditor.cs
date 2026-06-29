using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    [CustomEditor(typeof(TerrainMeshCaptureArea))]
    public sealed class TerrainMeshCaptureAreaEditor : UnityEditor.Editor
    {
        private static readonly Color AreaHandleColor = new Color(0.1f, 0.85f, 1f, 1f);
        private static readonly Color AreaGuideColor = new Color(0.1f, 0.85f, 1f, 0.65f);
        private static bool showGizmoSettings;
        private static GUIStyle sceneLabelStyle;

        private UnityEditor.Editor profileEditor;
        private bool hasComplexityAnalysis;
        private TerrainComplexityAnalysis complexityAnalysis;
        private string complexityAnalysisError;

        public override void OnInspectorGUI()
        {
            var area = (TerrainMeshCaptureArea)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("drawGizmo"));
            if (serializedObject.FindProperty("drawGizmo").boolValue)
            {
                showGizmoSettings = EditorGUILayout.Foldout(showGizmoSettings, "Gizmo Settings", true);
                if (showGizmoSettings)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoColor"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoWireColor"));
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawResolvedTerrain(area);
            DrawProfileEditor(area);

            TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan = default;
            List<string> issues = new List<string>();
            bool hasPlan = area.Profile != null
                && TerrainMeshCaptureAssetWriter.TryBuildBakePlan(area, area.Profile, out plan, out issues);

            DrawBakeEstimate(area, hasPlan, plan, issues);
            DrawAutoConfiguration(area, hasPlan, plan);
            DrawPreviewButtons(area, hasPlan);
            DrawBakeButton(area, hasPlan);
        }

        private void OnSceneGUI()
        {
            var area = (TerrainMeshCaptureArea)target;
            if (area.Profile == null)
            {
                return;
            }

            Terrain terrain = TerrainMeshCaptureAreaUtility.ResolveTerrain(area);
            if (terrain == null || terrain.terrainData == null)
            {
                return;
            }

            TerrainMeshCaptureSettings settings = TerrainMeshCaptureAreaUtility.BuildSettings(area.Profile);
            TerrainData terrainData = terrain.terrainData;
            Vector3 centerLocal = terrain.transform.InverseTransformPoint(area.transform.position);
            Rect rect = TerrainMeshCaptureBaker.BuildRect(centerLocal, settings.Size);
            rect = TerrainMeshCaptureBaker.ResolveRect(rect, terrainData.size, settings.BoundsMode, out bool inside);
            if (!inside)
            {
                return;
            }

            DrawSceneLabel(area.transform.position, "Capture Area", AreaHandleColor);
            Handles.color = AreaHandleColor;
            EditorGUI.BeginChangeCheck();
            Vector3 newCenterWorld = Handles.PositionHandle(area.transform.position, terrain.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area.transform, "Move Terrain Capture Area");
                area.transform.position = newCenterWorld;
                SceneView.RepaintAll();
            }

            DrawAreaSizeHandles(area, terrain, terrainData, rect);
        }

        private static void DrawAreaSizeHandles(TerrainMeshCaptureArea area, Terrain terrain, TerrainData terrainData, Rect rect)
        {
            bool splitByBlock = area.Profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize;
            float snapSize = splitByBlock ? Mathf.Max(1f, Mathf.Round(area.Profile.SquareBlockSize)) : 1f;
            float handleSize = GetModernHandleSize(area.transform.position, 0.16f);
            Vector3 left = SampleWorld(terrain, terrainData, rect.xMin, rect.center.y);
            Vector3 right = SampleWorld(terrain, terrainData, rect.xMax, rect.center.y);
            Vector3 bottom = SampleWorld(terrain, terrainData, rect.center.x, rect.yMin);
            Vector3 top = SampleWorld(terrain, terrainData, rect.center.x, rect.yMax);

            Vector3 terrainRight = terrain.transform.TransformDirection(Vector3.right);
            Vector3 terrainForward = terrain.transform.TransformDirection(Vector3.forward);

            Handles.color = AreaGuideColor;
            DrawSceneLabel(right + Vector3.up * handleSize * 1.5f, BuildSizeLabel("Width", rect.width, snapSize, splitByBlock), AreaHandleColor);
            DrawSceneLabel(top + Vector3.up * handleSize * 1.5f, BuildSizeLabel("Depth", rect.height, snapSize, splitByBlock), AreaHandleColor);

            Handles.color = AreaHandleColor;
            EditorGUI.BeginChangeCheck();
            Vector3 newLeft = Handles.Slider(left, -terrainRight, handleSize, XzSolidDiscHandleCap, 0f);
            Vector3 newRight = Handles.Slider(right, terrainRight, handleSize, XzSolidDiscHandleCap, 0f);
            Vector3 newBottom = Handles.Slider(bottom, -terrainForward, handleSize, XzSolidDiscHandleCap, 0f);
            Vector3 newTop = Handles.Slider(top, terrainForward, handleSize, XzSolidDiscHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Vector3 leftLocal = terrain.transform.InverseTransformPoint(newLeft);
            Vector3 rightLocal = terrain.transform.InverseTransformPoint(newRight);
            Vector3 bottomLocal = terrain.transform.InverseTransformPoint(newBottom);
            Vector3 topLocal = terrain.transform.InverseTransformPoint(newTop);
            Rect snappedRect = SnapDraggedRectToGrid(
                rect,
                terrainData.size,
                snapSize,
                leftLocal.x,
                rightLocal.x,
                bottomLocal.z,
                topLocal.z);
            if (snappedRect.width <= 0.01f || snappedRect.height <= 0.01f)
            {
                return;
            }

            Undo.RecordObject(area.transform, "Resize Terrain Capture Area");
            SetAreaSize(area, new Vector2(snappedRect.width, snappedRect.height));
            Vector3 centerLocal = new Vector3(snappedRect.center.x, 0f, snappedRect.center.y);
            area.transform.position = terrain.transform.TransformPoint(centerLocal);
            EditorUtility.SetDirty(area);
            SceneView.RepaintAll();
        }

        private static string BuildSizeLabel(string axis, float length, float snapSize, bool splitByBlock)
        {
            int roundedLength = Mathf.RoundToInt(length);
            if (!splitByBlock)
            {
                return $"{axis} {roundedLength}";
            }

            int blocks = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(1f, snapSize)));
            return $"{axis} {roundedLength} ({blocks} blocks)";
        }

        private static Rect SnapDraggedRectToGrid(
            Rect rect,
            Vector3 terrainSize,
            float snapSize,
            float draggedLeft,
            float draggedRight,
            float draggedBottom,
            float draggedTop)
        {
            snapSize = Mathf.Max(1f, Mathf.Round(snapSize));
            float leftDelta = Mathf.Abs(draggedLeft - rect.xMin);
            float rightDelta = Mathf.Abs(draggedRight - rect.xMax);
            float bottomDelta = Mathf.Abs(draggedBottom - rect.yMin);
            float topDelta = Mathf.Abs(draggedTop - rect.yMax);
            float maxDelta = Mathf.Max(leftDelta, rightDelta, bottomDelta, topDelta);
            float xMin = rect.xMin;
            float xMax = rect.xMax;
            float zMin = rect.yMin;
            float zMax = rect.yMax;

            if (Mathf.Approximately(maxDelta, leftDelta))
            {
                int steps = GetSnappedStepCount(xMax - Mathf.Clamp(draggedLeft, 0f, xMax - snapSize), snapSize, xMax / snapSize);
                xMin = xMax - steps * snapSize;
            }
            else if (Mathf.Approximately(maxDelta, rightDelta))
            {
                int steps = GetSnappedStepCount(Mathf.Clamp(draggedRight, xMin + snapSize, terrainSize.x) - xMin, snapSize, (terrainSize.x - xMin) / snapSize);
                xMax = xMin + steps * snapSize;
            }
            else if (Mathf.Approximately(maxDelta, bottomDelta))
            {
                int steps = GetSnappedStepCount(zMax - Mathf.Clamp(draggedBottom, 0f, zMax - snapSize), snapSize, zMax / snapSize);
                zMin = zMax - steps * snapSize;
            }
            else
            {
                int steps = GetSnappedStepCount(Mathf.Clamp(draggedTop, zMin + snapSize, terrainSize.z) - zMin, snapSize, (terrainSize.z - zMin) / snapSize);
                zMax = zMin + steps * snapSize;
            }

            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        private static int GetSnappedStepCount(float length, float snapSize, float maxSteps)
        {
            int upperBound = Mathf.Max(1, Mathf.FloorToInt(maxSteps));
            int steps = Mathf.RoundToInt(length / Mathf.Max(1f, snapSize));
            return Mathf.Clamp(steps, 1, upperBound);
        }

        private static float GetModernHandleSize(Vector3 position, float scale)
        {
            return Mathf.Clamp(HandleUtility.GetHandleSize(position) * scale, 0.25f, 8f);
        }

        private static void XzSolidDiscHandleCap(int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            const float RadiusScale = 0.55f;
            float radius = size * RadiusScale;
            switch (eventType)
            {
                case EventType.Layout:
                    HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(position, radius));
                    break;
                case EventType.Repaint:
                    Handles.DrawSolidDisc(position, Vector3.up, radius);
                    Handles.DrawWireDisc(position, Vector3.up, radius);
                    break;
            }
        }

        private static void DrawSceneLabel(Vector3 position, string text, Color color)
        {
            sceneLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            sceneLabelStyle.normal.textColor = color;
            Handles.Label(position, text, sceneLabelStyle);
        }

        private static Vector3 SampleWorld(Terrain terrain, TerrainData terrainData, float localX, float localZ)
        {
            float normalizedX = terrainData.size.x > 0f ? Mathf.Clamp01(localX / terrainData.size.x) : 0f;
            float normalizedZ = terrainData.size.z > 0f ? Mathf.Clamp01(localZ / terrainData.size.z) : 0f;
            float height = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            return terrain.transform.TransformPoint(new Vector3(localX, height, localZ));
        }

        private static void SetAreaSize(TerrainMeshCaptureArea area, Vector2 size)
        {
            var serializedProfile = new SerializedObject(area.Profile);
            SerializedProperty areaSize = serializedProfile.FindProperty("areaSize");
            if (area.Profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize)
            {
                float blockSize = Mathf.Max(1f, Mathf.Round(area.Profile.SquareBlockSize));
                areaSize.vector2Value = TerrainMeshCaptureProfile.SnapAreaSizeToBlockGrid(size, blockSize);
            }
            else
            {
                areaSize.vector2Value = new Vector2(
                    Mathf.Max(1f, Mathf.Round(size.x)),
                    Mathf.Max(1f, Mathf.Round(size.y)));
            }

            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(area.Profile);
        }

        private void DrawResolvedTerrain(TerrainMeshCaptureArea area)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Resolved Terrain", TerrainMeshCaptureAreaUtility.ResolveTerrain(area), typeof(Terrain), true);
            }
        }

        private void DrawProfileEditor(TerrainMeshCaptureArea area)
        {
            if (area.Profile == null)
            {
                if (GUILayout.Button("Create Bake Profile", GUILayout.Height(28)))
                {
                    CreateProfile(area);
                }

                EditorGUILayout.HelpBox("Assign or create a Bake Profile to edit area size, block size, output paths, texture resolution, and shader.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            if (profileEditor == null || profileEditor.target != area.Profile)
            {
                if (profileEditor != null)
                {
                    DestroyImmediate(profileEditor);
                }

                profileEditor = CreateEditor(area.Profile);
            }

            profileEditor.OnInspectorGUI();
        }

        private void DrawBakeEstimate(
            TerrainMeshCaptureArea area,
            bool hasPlan,
            TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan,
            List<string> issues)
        {
            EditorGUILayout.Space(8);
            if (area.Profile == null)
            {
                return;
            }

            if (!hasPlan)
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    EditorGUILayout.HelpBox(issues[i], MessageType.Error);
                }

                return;
            }

            EditorGUILayout.LabelField("Grid", $"{plan.Columns} x {plan.Rows}");
            EditorGUILayout.LabelField("Chunks", plan.ChunkCount.ToString());
            if (area.Profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize)
            {
                EditorGUILayout.LabelField("Block Size", $"{area.Profile.SquareBlockSize:0} square");
            }

            EditorGUILayout.LabelField("Area Size", $"{plan.AreaRect.width:0} x {plan.AreaRect.height:0}");
            if (area.Profile.HasTextureOutputs)
            {
                Vector2Int textureSize = area.Profile.ResolveTextureSize(GetEstimateTextureRect(plan));
                EditorGUILayout.LabelField("Texture Outputs", area.Profile.TextureBakeOutputs.ToString());
                EditorGUILayout.LabelField("Texture Size", $"{textureSize.x} x {textureSize.y}");
            }
        }

        private static Rect GetEstimateTextureRect(TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan)
        {
            if (plan.Columns <= 1 && plan.Rows <= 1)
            {
                return plan.AreaRect;
            }

            float width = plan.AreaRect.width / Mathf.Max(1, plan.Columns);
            float height = plan.AreaRect.height / Mathf.Max(1, plan.Rows);
            return new Rect(0f, 0f, width, height);
        }

        private void DrawAutoConfiguration(
            TerrainMeshCaptureArea area,
            bool hasPlan,
            TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan)
        {
            if (area.Profile == null)
            {
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Auto Configuration", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!hasPlan))
            {
                if (GUILayout.Button("Analyze Terrain Complexity", GUILayout.Height(28)))
                {
                    hasComplexityAnalysis = TerrainMeshCaptureComplexityAnalyzer.TryAnalyze(
                        plan,
                        area.Profile,
                        out complexityAnalysis,
                        out complexityAnalysisError);
                }
            }

            if (!hasPlan)
            {
                EditorGUILayout.HelpBox("Fix bake plan issues before analyzing complexity.", MessageType.Info);
                return;
            }

            if (!hasComplexityAnalysis)
            {
                if (!string.IsNullOrEmpty(complexityAnalysisError))
                {
                    EditorGUILayout.HelpBox(complexityAnalysisError, MessageType.Warning);
                }

                return;
            }

            EditorGUILayout.LabelField("Complexity", $"{complexityAnalysis.Complexity:P0}");
            EditorGUILayout.LabelField("Height Range", $"{complexityAnalysis.HeightRange:0.###}");
            EditorGUILayout.LabelField("Average Slope", $"{complexityAnalysis.AverageSlope:0.###}");
            EditorGUILayout.LabelField("Roughness", $"{complexityAnalysis.Roughness:0.###}");
            EditorGUILayout.LabelField("Recommended Samples", $"{complexityAnalysis.RecommendedSamplesX} x {complexityAnalysis.RecommendedSamplesZ}");
            EditorGUILayout.LabelField("Recommended Error", $"{complexityAnalysis.RecommendedMaxHeightError:0.###}");
            EditorGUILayout.LabelField("Recommended Triangles", complexityAnalysis.RecommendedMaxTriangles.ToString());
            EditorGUILayout.LabelField("Recommended Texture Long Side", complexityAnalysis.RecommendedTextureResolution.ToString());

            if (GUILayout.Button("Apply Recommended Settings", GUILayout.Height(28)))
            {
                TerrainMeshCaptureComplexityAnalyzer.ApplyToProfile(area.Profile, complexityAnalysis);
                hasComplexityAnalysis = false;
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawBakeButton(TerrainMeshCaptureArea area, bool canBake)
        {
            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("Bake Terrain Mesh Assets", GUILayout.Height(34)))
                {
                    TerrainMeshCaptureAssetWriter.BakeProfile(area);
                }
            }
        }

        private void DrawPreviewButtons(TerrainMeshCaptureArea area, bool canPreview)
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canPreview))
                {
                    if (GUILayout.Button("Preview Baked Assets", GUILayout.Height(28)))
                    {
                        if (!TerrainMeshCapturePreviewUtility.ShowPreview(area, out string error))
                        {
                            Debug.LogError($"<color=#ff4d4d><b>[TerrainMeshCapture]</b></color> Preview failed: {error}", area);
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(!TerrainMeshCapturePreviewUtility.HasPreview(area)))
                {
                    if (GUILayout.Button("Clear Preview", GUILayout.Height(28)))
                    {
                        TerrainMeshCapturePreviewUtility.ClearPreview(area);
                    }
                }
            }
        }

        private void CreateProfile(TerrainMeshCaptureArea area)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Terrain Mesh Capture Profile",
                "TerrainMeshCaptureProfile",
                "asset",
                "Choose where to save the bake profile.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var profile = CreateInstance<TerrainMeshCaptureProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            SerializedObject serializedArea = new SerializedObject(area);
            serializedArea.FindProperty("profile").objectReferenceValue = profile;
            serializedArea.ApplyModifiedProperties();
            EditorUtility.SetDirty(area);
            Selection.activeObject = area;
        }

        private void OnDisable()
        {
            if (profileEditor != null)
            {
                DestroyImmediate(profileEditor);
                profileEditor = null;
            }
        }
    }
}
