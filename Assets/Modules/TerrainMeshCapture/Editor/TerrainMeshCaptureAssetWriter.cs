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
            string textureFolder = profile.TextureBakeMode == TerrainTextureBakeMode.None ? string.Empty : EnsureOutputFolder(profile.TextureOutputFolder);
            string materialFolder = profile.CreateMaterials ? EnsureOutputFolder(profile.MaterialOutputFolder) : string.Empty;
            string prefabFolder = profile.CreatePrefab ? EnsureOutputFolder(profile.PrefabOutputFolder) : string.Empty;
            List<ChunkBakeItem> chunks = BuildChunks(plan.AreaRect, profile);

            var texturePaths = new List<string>(chunks.Count);
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
                            BakeChunkAssets(plan.Terrain, profile, meshFolder, textureFolder, item, meshPaths, texturePaths);
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
                            string materialPath = CreateMaterial(profile, materialFolder, chunks[i], texturePaths, i);
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

            if (profile.TextureBakeMode != TerrainTextureBakeMode.None && !IsProjectFolder(profile.TextureOutputFolder))
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

            areaRect = TerrainMeshCaptureBaker.ResolveRect(areaRect, terrain.terrainData.size, profile.BoundsMode, out bool inside);
            if (!inside)
            {
                issues.Add($"Profile area is outside terrain: {areaRect}.");
            }

            if (profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize && !IsRectDivisibleByBlock(areaRect, profile.SquareBlockSize))
            {
                issues.Add("Capture area size must be divisible by Block Size. Move the area inside terrain or adjust Blocks X/Z.");
            }

            ResolveSplitGrid(areaRect, profile, out int columns, out int rows);
            long chunkCount = (long)columns * rows;
            if (chunkCount > 10000)
            {
                issues.Add($"Refusing to bake {chunkCount} chunks. Increase block size or reduce area.");
            }

            if (profile.TextureResolution >= 4096 && chunkCount > 256)
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
            List<string> meshPaths,
            List<string> texturePaths)
        {
            var chunkSettings = TerrainMeshCaptureAreaUtility.BuildSettings(profile);
            if (!TerrainMeshCaptureBaker.TryBuildMesh(terrain, item.Rect, chunkSettings, out TerrainMeshCaptureResult result, out string meshError))
            {
                throw new InvalidOperationException($"Chunk {item.Name} mesh failed: {meshError}");
            }

            result.Mesh.name = $"{item.Name}_Mesh";
            string meshPath = BuildAssetPath(meshFolder, $"{item.Name}_Mesh", "asset", profile.WriteMode);
            SaveMesh(result.Mesh, meshPath);
            meshPaths.Add(meshPath);

            if (profile.TextureBakeMode == TerrainTextureBakeMode.None)
            {
                texturePaths.Add(string.Empty);
                return;
            }

            if (!TerrainMeshCaptureBaker.TryBakeTexture(terrain, result.TerrainLocalRect, chunkSettings, out Texture2D texture, out string textureError))
            {
                throw new InvalidOperationException($"Chunk {item.Name} texture failed: {textureError}");
            }

            string texturePath = BuildAssetPath(textureFolder, $"{item.Name}_{profile.TextureBakeMode}", "png", profile.WriteMode);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            File.WriteAllBytes(texturePath, png);
            texturePaths.Add(texturePath);
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
                float zMax = z == rows - 1 ? areaRect.yMax : zMin + blockSize;
                for (int x = 0; x < columns; x++)
                {
                    float xMin = areaRect.xMin + blockSize * x;
                    float xMax = x == columns - 1 ? areaRect.xMax : xMin + blockSize;
                    Rect chunkRect = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
                    string name = $"{Sanitize(profile.AssetName)}_X{x:00}_Z{z:00}";
                    chunks.Add(new ChunkBakeItem(chunkRect, name));
                }
            }

            return chunks;
        }

        internal static void ResolveSplitGrid(Rect areaRect, TerrainMeshCaptureProfile profile, out int columns, out int rows)
        {
            float blockSize = GetSquareBlockSize(profile);
            columns = Mathf.Max(1, Mathf.RoundToInt(areaRect.width / blockSize));
            rows = Mathf.Max(1, Mathf.RoundToInt(areaRect.height / blockSize));
        }

        private static float GetSquareBlockSize(TerrainMeshCaptureProfile profile)
        {
            return profile != null ? profile.SquareBlockSize : 1f;
        }

        private static bool IsRectDivisibleByBlock(Rect areaRect, float blockSize)
        {
            blockSize = Mathf.Max(1f, blockSize);
            float columns = areaRect.width / blockSize;
            float rows = areaRect.height / blockSize;
            return Mathf.Abs(columns - Mathf.Round(columns)) <= 0.001f
                && Mathf.Abs(rows - Mathf.Round(rows)) <= 0.001f;
        }

        private static void ConfigureImportedTextures(List<string> texturePaths, bool mipMaps)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < texturePaths.Count; i++)
                {
                    string path = texturePaths[i];
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    importer.textureType = TextureImporterType.Default;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = mipMaps;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static string CreateMaterial(
            TerrainMeshCaptureProfile profile,
            string materialFolder,
            ChunkBakeItem item,
            List<string> texturePaths,
            int index)
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

            if (index < texturePaths.Count && !string.IsNullOrEmpty(texturePaths[index]))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePaths[index]);
                if (texture != null)
                {
                    if (material.HasProperty("_BaseMap"))
                    {
                        material.SetTexture("_BaseMap", texture);
                    }

                    if (material.HasProperty("_MainTex"))
                    {
                        material.SetTexture("_MainTex", texture);
                    }
                }
            }

            string path = BuildAssetPath(materialFolder, $"{item.Name}_Mat", "mat", profile.WriteMode);
            SaveMaterial(material, path);
            return path;
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
            if (!folder.StartsWith("Assets", StringComparison.Ordinal))
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
            return !string.IsNullOrWhiteSpace(folder)
                && folder.Replace("\\", "/").StartsWith("Assets", StringComparison.Ordinal);
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

            public TerrainLayerReadableScope(Terrain terrain)
            {
                TerrainLayer[] layers = terrain != null && terrain.terrainData != null ? terrain.terrainData.terrainLayers : null;
                if (layers == null)
                {
                    return;
                }

                for (int i = 0; i < layers.Length; i++)
                {
                    Texture2D texture = layers[i] != null ? layers[i].diffuseTexture : null;
                    if (texture == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(texture);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    importers.Add(importer);
                    originalReadable.Add(importer.isReadable);
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < importers.Count; i++)
                {
                    TextureImporter importer = importers[i];
                    if (importer == null || importer.isReadable == originalReadable[i])
                    {
                        continue;
                    }

                    importer.isReadable = originalReadable[i];
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
