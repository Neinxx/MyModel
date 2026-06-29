using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    public static class TerrainMeshCaptureAssetWriter
    {
        private const string DefaultOutputFolder = "Assets/TerrainMeshCaptures";

        [MenuItem("GameObject/Terrain Mesh Capture/Create Capture Area", false, 10)]
        public static void CreateCaptureArea()
        {
            var go = new GameObject("Terrain Mesh Capture Area");
            Undo.RegisterCreatedObjectUndo(go, "Create Terrain Mesh Capture Area");

            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                Vector3 terrainCenter = activeTerrain.terrainData.size * 0.5f;
                terrainCenter.y = 0f;
                go.transform.position = activeTerrain.transform.TransformPoint(terrainCenter);
            }

            go.AddComponent<TerrainMeshCaptureArea>();
            Selection.activeGameObject = go;
        }

        public static void BakeProfile(TerrainMeshCaptureArea area)
        {
            if (area == null || area.Profile == null)
            {
                Debug.LogError("<color=#ff4d4d><b>[TerrainMeshCapture]</b></color> Bake profile is missing.", area);
                return;
            }

            TerrainMeshCaptureProfile profile = area.Profile;
            profile.Sanitize();

            if (!TryBuildBakePlan(area, profile, out TerrainMeshCaptureBakePlan plan, out List<string> issues))
            {
                LogIssues(area, issues);
                return;
            }

            string meshFolder = EnsureOutputFolder(profile.MeshOutputFolder);
            string textureFolder = profile.HasTextureOutputs ? EnsureOutputFolder(profile.TextureOutputFolder) : string.Empty;
            string materialFolder = profile.CreateMaterials ? EnsureOutputFolder(profile.MaterialOutputFolder) : string.Empty;
            string prefabFolder = profile.CreatePrefab ? EnsureOutputFolder(profile.PrefabOutputFolder) : string.Empty;
            List<ChunkBakeItem> chunks = BuildChunks(plan.AreaRect, profile);
            TerrainMeshAdaptiveEdgeConstraints[] edgePlans = BuildChunkEdgePlans(plan.Terrain, profile, chunks, plan.Columns, plan.Rows);

            var texturePaths = new List<ChunkTexturePaths>(chunks.Count);
            var materialPaths = new List<string>(chunks.Count);
            var meshPaths = new List<string>(chunks.Count);
            var prefabPaths = new List<string>(1);

            try
            {
                using (new TerrainLayerReadableScope(plan.Terrain))
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            ChunkBakeItem item = chunks[i];
                            EditorUtility.DisplayProgressBar("Terrain Mesh Capture", $"Baking chunk {i + 1}/{chunks.Count}", (float)i / chunks.Count);
                            BakeChunkAssets(plan.Terrain, profile, meshFolder, textureFolder, item, edgePlans[i], meshPaths, texturePaths);
                        }
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ConfigureImportedTextures(texturePaths, profile.TextureMipMaps);

                if (profile.CreateMaterials)
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            string materialPath = CreateMaterial(profile, materialFolder, chunks[i], texturePaths[i]);
                            materialPaths.Add(materialPath);
                        }
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                if (profile.CreatePrefab)
                {
                    string prefabPath = CreatePrefab(profile, prefabFolder, chunks, meshPaths, materialPaths, plan.AreaRect);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        prefabPaths.Add(prefabPath);
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                TerrainMeshCapturePreviewUtility.RecordLastBake(profile, plan.AreaRect, chunks, meshPaths, materialPaths, prefabPaths);

                List<string> selectionPaths = prefabPaths.Count > 0 ? prefabPaths : meshPaths;
                Selection.objects = selectionPaths.ConvertAll(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)).ToArray();

                Debug.Log($"<color=#3FB950><b>[TerrainMeshCapture]</b></color> Profile bake completed: {chunks.Count} chunk(s).", area);
            }
            catch (Exception exception)
            {
                Debug.LogError($"<color=#ff4d4d><b>[TerrainMeshCapture]</b></color> Profile bake failed: {exception.Message}\n{exception.StackTrace}", area);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static bool TryBuildBakePlan(
            TerrainMeshCaptureArea area,
            TerrainMeshCaptureProfile profile,
            out TerrainMeshCaptureBakePlan plan,
            out List<string> issues)
        {
            plan = default;
            issues = new List<string>();
            if (area == null)
            {
                issues.Add("Capture area is missing.");
                return false;
            }

            if (profile == null)
            {
                issues.Add("Bake profile is missing.");
                return false;
            }

            profile.Sanitize();
            if (!TerrainMeshCaptureAreaUtility.TryGetAreaRect(area, profile, out Terrain terrain, out Rect areaRect))
            {
                issues.Add("Capture area has no valid source terrain.");
                return false;
            }

            if (!IsProjectFolder(profile.MeshOutputFolder))
            {
                issues.Add("Mesh output folder must be inside Assets.");
            }

            if (profile.HasTextureOutputs && !IsProjectFolder(profile.TextureOutputFolder))
            {
                issues.Add("Texture output folder must be inside Assets.");
            }

            if (profile.CreateMaterials && !IsProjectFolder(profile.MaterialOutputFolder))
            {
                issues.Add("Material output folder must be inside Assets.");
            }

            if (profile.CreatePrefab && !IsProjectFolder(profile.PrefabOutputFolder))
            {
                issues.Add("Prefab output folder must be inside Assets.");
            }

            areaRect = ResolveBakeAreaRect(areaRect, terrain.terrainData.size, profile, out bool inside);
            if (!inside)
            {
                issues.Add($"Profile area is outside terrain: {areaRect}.");
            }

            if (profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize && !CanFitSplitAreaInTerrain(profile, terrain.terrainData.size))
            {
                issues.Add("Split area is larger than the terrain. Reduce Blocks X/Z or Block Size.");
            }

            ResolveSplitGrid(areaRect, profile, out int columns, out int rows);
            long chunkCount = (long)columns * rows;
            if (chunkCount > 10000)
            {
                issues.Add($"Refusing to bake {chunkCount} chunks. Increase block size or reduce area.");
            }

            Vector2Int estimatedTextureSize = profile.ResolveTextureSize(GetEstimateTextureRect(areaRect, columns, rows));
            if (profile.HasTextureOutputs && Mathf.Max(estimatedTextureSize.x, estimatedTextureSize.y) >= 4096 && chunkCount > 256)
            {
                issues.Add("Texture resolution and chunk count are both high. Reduce resolution or increase block size.");
            }

            plan = new TerrainMeshCaptureBakePlan(terrain, areaRect, columns, rows, (int)Mathf.Min(chunkCount, int.MaxValue));
            return issues.Count == 0;
        }

        private static void BakeChunkAssets(
            Terrain terrain,
            TerrainMeshCaptureProfile profile,
            string meshFolder,
            string textureFolder,
            ChunkBakeItem item,
            TerrainMeshAdaptiveEdgeConstraints edgeConstraints,
            List<string> meshPaths,
            List<ChunkTexturePaths> texturePaths)
        {
            var chunkSettings = TerrainMeshCaptureAreaUtility.BuildSettings(profile);
            chunkSettings.SetAdaptiveEdgeConstraints(edgeConstraints);
            if (!TerrainMeshCaptureBaker.TryBuildMesh(terrain, item.Rect, chunkSettings, out TerrainMeshCaptureResult result, out string meshError))
            {
                throw new InvalidOperationException($"Chunk {item.Name} mesh failed: {meshError}");
            }

            result.Mesh.name = $"{item.Name}_Mesh";
            string meshPath = BuildAssetPath(meshFolder, $"{item.Name}_Mesh", "asset", profile.WriteMode);
            SaveMesh(result.Mesh, meshPath);
            meshPaths.Add(meshPath);

            if (!profile.HasTextureOutputs)
            {
                texturePaths.Add(default);
                return;
            }

            ChunkTexturePaths chunkTexturePaths = default;
            TerrainTextureBakeMode[] bakeModes = GetTextureBakeModes(profile.TextureBakeOutputs);
            for (int i = 0; i < bakeModes.Length; i++)
            {
                TerrainTextureBakeMode bakeMode = bakeModes[i];
                chunkSettings.SetTextureBakeMode(bakeMode);
                if (!TerrainMeshCaptureBaker.TryBakeTexture(terrain, result.TerrainLocalRect, chunkSettings, out Texture2D texture, out string textureError))
                {
                    throw new InvalidOperationException($"Chunk {item.Name} {bakeMode} texture failed: {textureError}");
                }

                string texturePath = BuildAssetPath(textureFolder, $"{item.Name}_{bakeMode}", "png", profile.WriteMode);
                byte[] png = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                File.WriteAllBytes(texturePath, png);
                chunkTexturePaths.Set(bakeMode, texturePath);
            }

            texturePaths.Add(chunkTexturePaths);
        }

        internal static List<ChunkBakeItem> BuildChunks(Rect areaRect, TerrainMeshCaptureProfile profile)
        {
            var chunks = new List<ChunkBakeItem>();
            if (profile.BakeScope == TerrainCaptureBakeScope.SingleArea)
            {
                chunks.Add(new ChunkBakeItem(areaRect, $"{Sanitize(profile.AssetName)}_X00_Z00"));
                return chunks;
            }

            ResolveSplitGrid(areaRect, profile, out int columns, out int rows);
            float blockSize = GetSquareBlockSize(profile);
            for (int z = 0; z < rows; z++)
            {
                float zMin = areaRect.yMin + blockSize * z;
                float zMax = zMin + blockSize;
                for (int x = 0; x < columns; x++)
                {
                    float xMin = areaRect.xMin + blockSize * x;
                    float xMax = xMin + blockSize;
                    Rect chunkRect = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
                    string name = $"{Sanitize(profile.AssetName)}_X{x:00}_Z{z:00}";
                    chunks.Add(new ChunkBakeItem(chunkRect, name));
                }
            }

            return chunks;
        }

        private static TerrainMeshAdaptiveEdgeConstraints[] BuildChunkEdgePlans(
            Terrain terrain,
            TerrainMeshCaptureProfile profile,
            List<ChunkBakeItem> chunks,
            int columns,
            int rows)
        {
            var plans = new TerrainMeshAdaptiveEdgeConstraints[chunks.Count];
            for (int i = 0; i < plans.Length; i++)
            {
                plans[i] = new TerrainMeshAdaptiveEdgeConstraints();
            }

            if (terrain == null
                || terrain.terrainData == null
                || profile == null
                || profile.MeshGenerationMode != TerrainMeshGenerationMode.AdaptiveHeightTin
                || profile.BakeScope != TerrainCaptureBakeScope.SplitByBlockSize
                || columns <= 1 && rows <= 1)
            {
                return plans;
            }

            TerrainData terrainData = terrain.terrainData;
            float[] chunkComplexities = new float[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                chunkComplexities[i] = EstimateChunkComplexity(terrainData, chunks[i].Rect);
            }

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns - 1; column++)
                {
                    int leftIndex = GetChunkIndex(column, row, columns);
                    int rightIndex = GetChunkIndex(column + 1, row, columns);
                    Rect leftRect = chunks[leftIndex].Rect;
                    float complexity = Mathf.Max(chunkComplexities[leftIndex], chunkComplexities[rightIndex]);
                    int[] points = BuildSharedVerticalEdgePoints(
                        terrainData,
                        leftRect.xMax,
                        leftRect.yMin,
                        leftRect.yMax,
                        profile.SamplesZ,
                        profile.AdaptiveMaxHeightError,
                        complexity);
                    plans[leftIndex].RightZ = points;
                    plans[leftIndex].LockRight = true;
                    plans[rightIndex].LeftZ = points;
                    plans[rightIndex].LockLeft = true;
                }
            }

            for (int row = 0; row < rows - 1; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int bottomIndex = GetChunkIndex(column, row, columns);
                    int topIndex = GetChunkIndex(column, row + 1, columns);
                    Rect bottomRect = chunks[bottomIndex].Rect;
                    float complexity = Mathf.Max(chunkComplexities[bottomIndex], chunkComplexities[topIndex]);
                    int[] points = BuildSharedHorizontalEdgePoints(
                        terrainData,
                        bottomRect.yMax,
                        bottomRect.xMin,
                        bottomRect.xMax,
                        profile.SamplesX,
                        profile.AdaptiveMaxHeightError,
                        complexity);
                    plans[bottomIndex].TopX = points;
                    plans[bottomIndex].LockTop = true;
                    plans[topIndex].BottomX = points;
                    plans[topIndex].LockBottom = true;
                }
            }

            return plans;
        }

        private static int GetChunkIndex(int column, int row, int columns)
        {
            return row * columns + column;
        }

        internal static void ResolveSplitGrid(Rect areaRect, TerrainMeshCaptureProfile profile, out int columns, out int rows)
        {
            if (profile == null || profile.BakeScope == TerrainCaptureBakeScope.SingleArea)
            {
                columns = 1;
                rows = 1;
                return;
            }

            columns = profile.BlockColumns;
            rows = profile.BlockRows;
        }

        private static float GetSquareBlockSize(TerrainMeshCaptureProfile profile)
        {
            return profile != null ? profile.SquareBlockSize : 1f;
        }

        private static Rect GetEstimateTextureRect(Rect areaRect, int columns, int rows)
        {
            if (columns <= 1 && rows <= 1)
            {
                return areaRect;
            }

            float width = areaRect.width / Mathf.Max(1, columns);
            float height = areaRect.height / Mathf.Max(1, rows);
            return new Rect(0f, 0f, width, height);
        }

        private static Rect ResolveBakeAreaRect(
            Rect areaRect,
            Vector3 terrainSize,
            TerrainMeshCaptureProfile profile,
            out bool inside)
        {
            if (profile == null || profile.BakeScope == TerrainCaptureBakeScope.SingleArea)
            {
                return TerrainMeshCaptureBaker.ResolveRect(areaRect, terrainSize, profile != null ? profile.BoundsMode : TerrainCaptureBoundsMode.RejectOutOfBounds, out inside);
            }

            float blockSize = profile.SquareBlockSize;
            Vector2 splitSize = new Vector2(profile.BlockColumns * blockSize, profile.BlockRows * blockSize);
            Rect splitRect = TerrainMeshCaptureBaker.BuildRect(new Vector3(areaRect.center.x, 0f, areaRect.center.y), splitSize);
            return TerrainMeshCaptureBaker.ResolveRect(splitRect, terrainSize, profile.BoundsMode, out inside);
        }

        private static bool CanFitSplitAreaInTerrain(TerrainMeshCaptureProfile profile, Vector3 terrainSize)
        {
            if (profile == null || profile.BakeScope == TerrainCaptureBakeScope.SingleArea)
            {
                return true;
            }

            float blockSize = profile.SquareBlockSize;
            float width = profile.BlockColumns * blockSize;
            float height = profile.BlockRows * blockSize;
            return width <= terrainSize.x + 0.001f && height <= terrainSize.z + 0.001f;
        }

        private static int[] BuildSharedHorizontalEdgePoints(
            TerrainData terrainData,
            float localZ,
            float xMin,
            float xMax,
            int sampleCount,
            float maxHeightError,
            float complexity)
        {
            return BuildSharedEdgePoints(
                sampleCount,
                maxHeightError,
                complexity,
                index =>
                {
                    float t = sampleCount > 1 ? (float)index / (sampleCount - 1) : 0f;
                    float localX = Mathf.Lerp(xMin, xMax, t);
                    return SampleTerrainHeight(terrainData, localX, localZ);
                });
        }

        private static int[] BuildSharedVerticalEdgePoints(
            TerrainData terrainData,
            float localX,
            float zMin,
            float zMax,
            int sampleCount,
            float maxHeightError,
            float complexity)
        {
            return BuildSharedEdgePoints(
                sampleCount,
                maxHeightError,
                complexity,
                index =>
                {
                    float t = sampleCount > 1 ? (float)index / (sampleCount - 1) : 0f;
                    float localZ = Mathf.Lerp(zMin, zMax, t);
                    return SampleTerrainHeight(terrainData, localX, localZ);
                });
        }

        private static int[] BuildSharedEdgePoints(
            int sampleCount,
            float maxHeightError,
            float complexity,
            Func<int, float> sampleHeight)
        {
            int last = Mathf.Max(1, sampleCount - 1);
            var points = new HashSet<int> { 0, last };
            SimplifySharedEdge(points, sampleHeight, 0, last, Mathf.Max(0.01f, maxHeightError));

            int maxSegments = Mathf.Max(1, last);
            int targetSegments = Mathf.Clamp(NextPowerOfTwo(Mathf.RoundToInt(Mathf.Lerp(2f, maxSegments, Mathf.Clamp01(complexity)))), 1, maxSegments);
            for (int i = 1; i < targetSegments; i++)
            {
                points.Add(Mathf.Clamp(Mathf.RoundToInt((float)last * i / targetSegments), 0, last));
            }

            var sorted = new List<int>(points);
            sorted.Sort();
            return sorted.ToArray();
        }

        private static void SimplifySharedEdge(HashSet<int> points, Func<int, float> sampleHeight, int start, int end, float maxHeightError)
        {
            if (end - start <= 1)
            {
                return;
            }

            float startHeight = sampleHeight(start);
            float endHeight = sampleHeight(end);
            float maxError = 0f;
            int split = -1;
            for (int i = start + 1; i < end; i++)
            {
                float t = (float)(i - start) / (end - start);
                float projected = Mathf.Lerp(startHeight, endHeight, t);
                float error = Mathf.Abs(sampleHeight(i) - projected);
                if (error <= maxError)
                {
                    continue;
                }

                maxError = error;
                split = i;
            }

            if (split < 0 || maxError <= maxHeightError)
            {
                return;
            }

            points.Add(split);
            SimplifySharedEdge(points, sampleHeight, start, split, maxHeightError);
            SimplifySharedEdge(points, sampleHeight, split, end, maxHeightError);
        }

        private static float EstimateChunkComplexity(TerrainData terrainData, Rect rect)
        {
            const int Samples = 33;
            float[,] heights = new float[Samples, Samples];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int z = 0; z < Samples; z++)
            {
                float v = (float)z / (Samples - 1);
                float localZ = Mathf.Lerp(rect.yMin, rect.yMax, v);
                for (int x = 0; x < Samples; x++)
                {
                    float u = (float)x / (Samples - 1);
                    float localX = Mathf.Lerp(rect.xMin, rect.xMax, u);
                    float height = SampleTerrainHeight(terrainData, localX, localZ);
                    heights[z, x] = height;
                    minHeight = Mathf.Min(minHeight, height);
                    maxHeight = Mathf.Max(maxHeight, height);
                }
            }

            float roughnessSum = 0f;
            int roughnessCount = 0;
            for (int z = 1; z < Samples - 1; z++)
            {
                for (int x = 1; x < Samples - 1; x++)
                {
                    float neighborAverage = (heights[z, x - 1] + heights[z, x + 1] + heights[z - 1, x] + heights[z + 1, x]) * 0.25f;
                    roughnessSum += Mathf.Abs(heights[z, x] - neighborAverage);
                    roughnessCount++;
                }
            }

            float heightRange = Mathf.Max(0f, maxHeight - minHeight);
            float roughness = roughnessCount > 0 ? roughnessSum / roughnessCount : 0f;
            float normalizedRange = Mathf.Clamp01(heightRange / Mathf.Max(1f, terrainData.size.y * 0.2f));
            float normalizedRoughness = Mathf.Clamp01(roughness / Mathf.Max(0.025f, terrainData.size.y * 0.001f));
            return Mathf.Clamp01(normalizedRange * 0.35f + normalizedRoughness * 0.65f);
        }

        private static float SampleTerrainHeight(TerrainData terrainData, float localX, float localZ)
        {
            float normalizedX = terrainData.size.x > 0f ? Mathf.Clamp01(localX / terrainData.size.x) : 0f;
            float normalizedZ = terrainData.size.z > 0f ? Mathf.Clamp01(localZ / terrainData.size.z) : 0f;
            return terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value)
            {
                result <<= 1;
            }

            return result;
        }

        private static void ConfigureImportedTextures(List<ChunkTexturePaths> texturePaths, bool mipMaps)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < texturePaths.Count; i++)
                {
                    ConfigureImportedTexture(texturePaths[i].AlbedoPath, TerrainTextureBakeMode.Albedo, mipMaps);
                    ConfigureImportedTexture(texturePaths[i].NormalMapPath, TerrainTextureBakeMode.NormalMap, mipMaps);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void ConfigureImportedTexture(string path, TerrainTextureBakeMode bakeMode, bool mipMaps)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = bakeMode == TerrainTextureBakeMode.NormalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = mipMaps;
            importer.sRGBTexture = bakeMode == TerrainTextureBakeMode.Albedo;
            if (bakeMode == TerrainTextureBakeMode.NormalMap)
            {
                importer.convertToNormalmap = false;
            }

            importer.SaveAndReimport();
        }

        private static string CreateMaterial(
            TerrainMeshCaptureProfile profile,
            string materialFolder,
            ChunkBakeItem item,
            ChunkTexturePaths texturePaths)
        {
            Shader shader = profile.Shader != null
                ? profile.Shader
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return string.Empty;
            }

            var material = new Material(shader)
            {
                name = $"{item.Name}_Mat"
            };

            BindMaterialTextures(material, texturePaths);

            string path = BuildAssetPath(materialFolder, $"{item.Name}_Mat", "mat", profile.WriteMode);
            SaveMaterial(material, path);
            return path;
        }

        private static void BindMaterialTextures(Material material, ChunkTexturePaths texturePaths)
        {
            Texture2D albedo = LoadTexture(texturePaths.AlbedoPath);
            if (albedo != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", albedo);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", albedo);
                }
            }

            Texture2D normal = LoadTexture(texturePaths.NormalMapPath);
            if (normal != null)
            {
                if (material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normal);
                }

                if (material.HasProperty("_NormalMap"))
                {
                    material.SetTexture("_NormalMap", normal);
                }

                material.EnableKeyword("_NORMALMAP");
            }

        }

        private static Texture2D LoadTexture(string path)
        {
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static TerrainTextureBakeMode[] GetTextureBakeModes(TerrainTextureBakeOutputs outputs)
        {
            var modes = new List<TerrainTextureBakeMode>(3);
            if ((outputs & TerrainTextureBakeOutputs.Albedo) != 0)
            {
                modes.Add(TerrainTextureBakeMode.Albedo);
            }

            if ((outputs & TerrainTextureBakeOutputs.NormalMap) != 0)
            {
                modes.Add(TerrainTextureBakeMode.NormalMap);
            }

            return modes.ToArray();
        }

        private static string CreatePrefab(
            TerrainMeshCaptureProfile profile,
            string prefabFolder,
            List<ChunkBakeItem> chunks,
            List<string> meshPaths,
            List<string> materialPaths,
            Rect areaRect)
        {
            if (string.IsNullOrEmpty(prefabFolder))
            {
                return string.Empty;
            }

            var root = new GameObject(Sanitize(profile.AssetName));
            try
            {
                Vector3 areaPivot = new Vector3(areaRect.center.x, 0f, areaRect.center.y);
                for (int i = 0; i < chunks.Count; i++)
                {
                    Mesh mesh = i < meshPaths.Count ? AssetDatabase.LoadAssetAtPath<Mesh>(meshPaths[i]) : null;
                    if (mesh == null)
                    {
                        continue;
                    }

                    ChunkBakeItem chunk = chunks[i];
                    var child = new GameObject(chunk.Name);
                    child.transform.SetParent(root.transform, false);
                    Vector3 chunkPivot = new Vector3(chunk.Rect.center.x, 0f, chunk.Rect.center.y);
                    child.transform.localPosition = chunkPivot - areaPivot;

                    MeshFilter meshFilter = child.AddComponent<MeshFilter>();
                    MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
                    meshFilter.sharedMesh = mesh;

                    Material material = i < materialPaths.Count && !string.IsNullOrEmpty(materialPaths[i])
                        ? AssetDatabase.LoadAssetAtPath<Material>(materialPaths[i])
                        : null;
                    if (material != null)
                    {
                        meshRenderer.sharedMaterial = material;
                    }
                }

                string prefabPath = BuildAssetPath(prefabFolder, $"{Sanitize(profile.AssetName)}_Prefab", "prefab", profile.WriteMode);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string EnsureOutputFolder(string folder)
        {
            folder = string.IsNullOrWhiteSpace(folder) ? DefaultOutputFolder : folder.Replace("\\", "/").TrimEnd('/');
            if (!IsProjectFolder(folder))
            {
                folder = DefaultOutputFolder;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            return folder;
        }

        private static string BuildAssetPath(string folder, string assetName, string extension, TerrainCaptureAssetWriteMode writeMode)
        {
            string path = $"{folder}/{Sanitize(assetName)}.{extension}";
            return writeMode == TerrainCaptureAssetWriteMode.GenerateUnique
                ? AssetDatabase.GenerateUniqueAssetPath(path)
                : path;
        }

        private static void SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return;
            }

            AssetDatabase.CreateAsset(mesh, path);
        }

        private static void SaveMaterial(Material material, string path)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                UnityEngine.Object.DestroyImmediate(material);
                EditorUtility.SetDirty(existing);
                return;
            }

            AssetDatabase.CreateAsset(material, path);
        }

        private static bool IsProjectFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return false;
            }

            folder = folder.Replace("\\", "/").TrimEnd('/');
            return folder == "Assets"
                || folder.StartsWith("Assets/", StringComparison.Ordinal) && !HasParentDirectorySegment(folder);
        }

        private static bool HasParentDirectorySegment(string folder)
        {
            return folder == ".."
                || folder.StartsWith("../", StringComparison.Ordinal)
                || folder.EndsWith("/..", StringComparison.Ordinal)
                || folder.Contains("/../");
        }

        private static void LogIssues(UnityEngine.Object context, List<string> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogError($"<color=#ff4d4d><b>[TerrainMeshCapture]</b></color> {issues[i]}", context);
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "TerrainCapture";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        internal readonly struct ChunkBakeItem
        {
            public ChunkBakeItem(Rect rect, string name)
            {
                Rect = rect;
                Name = name;
            }

            public Rect Rect { get; }
            public string Name { get; }
        }

        internal struct ChunkTexturePaths
        {
            public string AlbedoPath { get; private set; }
            public string NormalMapPath { get; private set; }
            public string SplatWeightsPath { get; private set; }

            public void Set(TerrainTextureBakeMode mode, string path)
            {
                switch (mode)
                {
                    case TerrainTextureBakeMode.Albedo:
                        AlbedoPath = path;
                        break;
                    case TerrainTextureBakeMode.NormalMap:
                        NormalMapPath = path;
                        break;
                    case TerrainTextureBakeMode.SplatWeights:
                        SplatWeightsPath = path;
                        break;
                }
            }
        }

        public readonly struct TerrainMeshCaptureBakePlan
        {
            public TerrainMeshCaptureBakePlan(Terrain terrain, Rect areaRect, int columns, int rows, int chunkCount)
            {
                Terrain = terrain;
                AreaRect = areaRect;
                Columns = columns;
                Rows = rows;
                ChunkCount = chunkCount;
            }

            public Terrain Terrain { get; }
            public Rect AreaRect { get; }
            public int Columns { get; }
            public int Rows { get; }
            public int ChunkCount { get; }
        }

        private sealed class TerrainLayerReadableScope : IDisposable
        {
            private readonly List<TextureImporter> importers = new List<TextureImporter>();
            private readonly List<bool> originalReadable = new List<bool>();
            private readonly List<bool> originalCrunched = new List<bool>();
            private readonly List<TextureImporterCompression> originalCompression = new List<TextureImporterCompression>();

            public TerrainLayerReadableScope(Terrain terrain)
            {
                TerrainLayer[] layers = terrain != null && terrain.terrainData != null ? terrain.terrainData.terrainLayers : null;
                if (layers == null)
                {
                    return;
                }

                for (int i = 0; i < layers.Length; i++)
                {
                    AddReadableTexture(layers[i] != null ? layers[i].diffuseTexture : null);
                    AddReadableTexture(layers[i] != null ? layers[i].normalMapTexture : null);
                }
            }

            private void AddReadableTexture(Texture2D texture)
            {
                if (texture == null)
                {
                    return;
                }

                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importers.Contains(importer))
                {
                    return;
                }

                importers.Add(importer);
                originalReadable.Add(importer.isReadable);
                originalCrunched.Add(importer.crunchedCompression);
                originalCompression.Add(importer.textureCompression);
                bool changed = false;
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    changed = true;
                }

                if (importer.crunchedCompression)
                {
                    importer.crunchedCompression = false;
                    changed = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < importers.Count; i++)
                {
                    TextureImporter importer = importers[i];
                    if (importer == null)
                    {
                        continue;
                    }

                    bool changed = importer.isReadable != originalReadable[i]
                        || importer.crunchedCompression != originalCrunched[i]
                        || importer.textureCompression != originalCompression[i];
                    importer.isReadable = originalReadable[i];
                    importer.crunchedCompression = originalCrunched[i];
                    importer.textureCompression = originalCompression[i];
                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }
    }
}
