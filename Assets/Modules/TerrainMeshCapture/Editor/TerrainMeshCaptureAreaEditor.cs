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
        private static GUIStyle sceneLabelStyle;

        private UnityEditor.Editor profileEditor;

        public override void OnInspectorGUI()
        {
            var area = (TerrainMeshCaptureArea)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("drawGizmo"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoWireColor"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawResolvedTerrain(area);
            DrawProfileEditor(area);
            DrawBakeEstimate(area);
            DrawPreviewButtons(area);
            DrawBakeButton(area);
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
            float blockSize = Mathf.Max(1f, Mathf.Round(area.Profile.SquareBlockSize));
            float handleSize = GetModernHandleSize(area.transform.position, 0.16f);
            Vector3 left = SampleWorld(terrain, terrainData, rect.xMin, rect.center.y);
            Vector3 right = SampleWorld(terrain, terrainData, rect.xMax, rect.center.y);
            Vector3 bottom = SampleWorld(terrain, terrainData, rect.center.x, rect.yMin);
            Vector3 top = SampleWorld(terrain, terrainData, rect.center.x, rect.yMax);

            Vector3 terrainRight = terrain.transform.TransformDirection(Vector3.right);
            Vector3 terrainForward = terrain.transform.TransformDirection(Vector3.forward);

            Handles.color = AreaGuideColor;
            DrawSceneLabel(right + Vector3.up * handleSize * 1.5f, $"Width {Mathf.RoundToInt(rect.width)} ({Mathf.RoundToInt(rect.width / blockSize)} blocks)", AreaHandleColor);
            DrawSceneLabel(top + Vector3.up * handleSize * 1.5f, $"Depth {Mathf.RoundToInt(rect.height)} ({Mathf.RoundToInt(rect.height / blockSize)} blocks)", AreaHandleColor);

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
            Rect snappedRect = SnapDraggedRectToBlockGrid(
                rect,
                terrainData.size,
                blockSize,
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

        private static Rect SnapDraggedRectToBlockGrid(
            Rect rect,
            Vector3 terrainSize,
            float blockSize,
            float draggedLeft,
            float draggedRight,
            float draggedBottom,
            float draggedTop)
        {
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
                int blocks = GetSnappedBlockCount(xMax - Mathf.Clamp(draggedLeft, 0f, xMax - blockSize), blockSize, xMax / blockSize);
                xMin = xMax - blocks * blockSize;
            }
            else if (Mathf.Approximately(maxDelta, rightDelta))
            {
                int blocks = GetSnappedBlockCount(Mathf.Clamp(draggedRight, xMin + blockSize, terrainSize.x) - xMin, blockSize, (terrainSize.x - xMin) / blockSize);
                xMax = xMin + blocks * blockSize;
            }
            else if (Mathf.Approximately(maxDelta, bottomDelta))
            {
                int blocks = GetSnappedBlockCount(zMax - Mathf.Clamp(draggedBottom, 0f, zMax - blockSize), blockSize, zMax / blockSize);
                zMin = zMax - blocks * blockSize;
            }
            else
            {
                int blocks = GetSnappedBlockCount(Mathf.Clamp(draggedTop, zMin + blockSize, terrainSize.z) - zMin, blockSize, (terrainSize.z - zMin) / blockSize);
                zMax = zMin + blocks * blockSize;
            }

            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        private static int GetSnappedBlockCount(float length, float blockSize, float maxBlocks)
        {
            int upperBound = Mathf.Max(1, Mathf.FloorToInt(maxBlocks));
            int blocks = Mathf.RoundToInt(length / Mathf.Max(1f, blockSize));
            return Mathf.Clamp(blocks, 1, upperBound);
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
            float blockSize = Mathf.Max(1f, Mathf.Round(area.Profile.SquareBlockSize));
            serializedProfile.FindProperty("areaSize").vector2Value = TerrainMeshCaptureProfile.SnapAreaSizeToBlockGrid(size, blockSize);
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

        private void DrawBakeEstimate(TerrainMeshCaptureArea area)
        {
            EditorGUILayout.Space(8);
            if (area.Profile == null)
            {
                return;
            }

            if (!TerrainMeshCaptureAssetWriter.TryBuildBakePlan(area, area.Profile, out TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan, out List<string> issues))
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    EditorGUILayout.HelpBox(issues[i], MessageType.Error);
                }

                return;
            }

            EditorGUILayout.LabelField("Grid", $"{plan.Columns} x {plan.Rows}");
            EditorGUILayout.LabelField("Chunks", plan.ChunkCount.ToString());
            EditorGUILayout.LabelField("Block Size", $"{area.Profile.SquareBlockSize:0} square");
            EditorGUILayout.LabelField("Area Size", $"{area.Profile.AreaSize.x:0} x {area.Profile.AreaSize.y:0}");
            EditorGUILayout.LabelField("Texture", $"{area.Profile.TextureResolution} x {area.Profile.TextureResolution}");
        }

        private void DrawBakeButton(TerrainMeshCaptureArea area)
        {
            EditorGUILayout.Space(8);
            bool canBake = area.Profile != null
                && TerrainMeshCaptureAssetWriter.TryBuildBakePlan(area, area.Profile, out _, out _);

            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("Bake Terrain Mesh Assets", GUILayout.Height(34)))
                {
                    TerrainMeshCaptureAssetWriter.BakeProfile(area);
                }
            }
        }

        private void DrawPreviewButtons(TerrainMeshCaptureArea area)
        {
            EditorGUILayout.Space(8);
            bool canPreview = area.Profile != null
                && TerrainMeshCaptureAssetWriter.TryBuildBakePlan(area, area.Profile, out _, out _);

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
