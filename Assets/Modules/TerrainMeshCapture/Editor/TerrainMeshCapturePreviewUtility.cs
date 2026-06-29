using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    internal static class TerrainMeshCapturePreviewUtility
    {
        private const string PreviewObjectName = "__TerrainMeshCapturePreview";
        private static readonly Dictionary<int, LastBakeRecord> LastBakeRecords = new();

        public static bool ShowPreview(TerrainMeshCaptureArea area, out string error)
        {
            error = string.Empty;
            if (area == null || area.Profile == null)
            {
                error = "Capture area or profile is missing.";
                return false;
            }

            if (!TerrainMeshCaptureAssetWriter.TryBuildBakePlan(area, area.Profile, out TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan, out List<string> issues))
            {
                error = issues.Count > 0 ? string.Join("\n", issues) : "Failed to build preview plan.";
                return false;
            }

            ClearPreview(area);
            bool hasLastBake = TryGetLastBake(area.Profile, out LastBakeRecord lastBake);
            Rect previewRect = hasLastBake ? lastBake.AreaRect : plan.AreaRect;
            var preview = new GameObject(PreviewObjectName)
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };

            Vector3 areaPivot = new Vector3(previewRect.center.x, 0f, previewRect.center.y);
            preview.transform.SetPositionAndRotation(
                plan.Terrain.transform.TransformPoint(areaPivot),
                plan.Terrain.transform.rotation);
            preview.transform.localScale = plan.Terrain.transform.lossyScale;
            preview.transform.SetParent(area.transform, true);

            bool loadedAny = TryBuildPreviewFromLastBakedAssets(lastBake, preview.transform)
                || TryInstantiateLastBakedPrefab(area.Profile, lastBake, preview.transform)
                || TryInstantiateBakedPrefab(area.Profile, preview.transform)
                || TryBuildPreviewFromBakedAssets(area.Profile, plan.AreaRect, preview.transform);
            if (!loadedAny)
            {
                Object.DestroyImmediate(preview);
                error = "No baked prefab or mesh assets were found. Bake assets first.";
                return false;
            }

            Selection.activeGameObject = preview;
            SceneView.RepaintAll();
            return true;
        }

        public static void RecordLastBake(
            TerrainMeshCaptureProfile profile,
            Rect areaRect,
            List<TerrainMeshCaptureAssetWriter.ChunkBakeItem> chunks,
            List<string> meshPaths,
            List<string> materialPaths,
            List<string> prefabPaths)
        {
            if (profile == null)
            {
                return;
            }

            LastBakeRecords[profile.GetInstanceID()] = new LastBakeRecord(
                areaRect,
                chunks,
                meshPaths,
                materialPaths,
                prefabPaths);
        }

        public static void ClearPreview(TerrainMeshCaptureArea area)
        {
            if (area == null)
            {
                return;
            }

            Transform preview = area.transform.Find(PreviewObjectName);
            if (preview == null)
            {
                return;
            }

            Object.DestroyImmediate(preview.gameObject);
            SceneView.RepaintAll();
        }

        public static bool HasPreview(TerrainMeshCaptureArea area)
        {
            return area != null && area.transform.Find(PreviewObjectName) != null;
        }

        private static bool TryGetLastBake(TerrainMeshCaptureProfile profile, out LastBakeRecord record)
        {
            record = default;
            return profile != null
                && LastBakeRecords.TryGetValue(profile.GetInstanceID(), out record)
                && record.HasMeshes;
        }

        private static bool TryInstantiateLastBakedPrefab(TerrainMeshCaptureProfile profile, LastBakeRecord record, Transform parent)
        {
            if (profile == null || !profile.CreatePrefab || !record.HasPrefab)
            {
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(record.PrefabPaths[record.PrefabPaths.Count - 1]);
            return prefab != null && InstantiatePrefab(prefab, parent);
        }

        private static bool TryInstantiateBakedPrefab(TerrainMeshCaptureProfile profile, Transform parent)
        {
            if (profile == null || !profile.CreatePrefab)
            {
                return false;
            }

            GameObject prefab = LoadLatestAsset<GameObject>(
                profile.PrefabOutputFolder,
                $"{Sanitize(profile.AssetName)}_Prefab",
                "prefab");
            if (prefab == null)
            {
                return false;
            }

            return InstantiatePrefab(prefab, parent);
        }

        private static bool InstantiatePrefab(GameObject prefab, Transform parent)
        {
            Object instanceObject = PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instanceObject is not GameObject instance)
            {
                return false;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ApplyDontSave(instance);
            return true;
        }

        private static bool TryBuildPreviewFromLastBakedAssets(LastBakeRecord record, Transform parent)
        {
            if (!record.HasMeshes)
            {
                return false;
            }

            return TryBuildPreviewFromBakedAssetPaths(record.Chunks, record.MeshPaths, record.MaterialPaths, record.AreaRect, parent);
        }

        private static bool TryBuildPreviewFromBakedAssets(TerrainMeshCaptureProfile profile, Rect areaRect, Transform parent)
        {
            List<TerrainMeshCaptureAssetWriter.ChunkBakeItem> chunks = TerrainMeshCaptureAssetWriter.BuildChunks(areaRect, profile);
            var meshPaths = new List<string>(chunks.Count);
            var materialPaths = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                TerrainMeshCaptureAssetWriter.ChunkBakeItem chunk = chunks[i];
                Mesh mesh = LoadLatestAsset<Mesh>(profile.MeshOutputFolder, $"{chunk.Name}_Mesh", "asset");
                meshPaths.Add(mesh != null ? AssetDatabase.GetAssetPath(mesh) : string.Empty);

                Material material = LoadLatestAsset<Material>(profile.MaterialOutputFolder, $"{chunk.Name}_Mat", "mat");
                materialPaths.Add(material != null ? AssetDatabase.GetAssetPath(material) : string.Empty);
            }

            return TryBuildPreviewFromBakedAssetPaths(chunks, meshPaths, materialPaths, areaRect, parent);
        }

        private static bool TryBuildPreviewFromBakedAssetPaths(
            List<TerrainMeshCaptureAssetWriter.ChunkBakeItem> chunks,
            List<string> meshPaths,
            List<string> materialPaths,
            Rect areaRect,
            Transform parent)
        {
            Vector3 areaPivot = new Vector3(areaRect.center.x, 0f, areaRect.center.y);
            bool loadedAny = false;

            for (int i = 0; i < chunks.Count; i++)
            {
                TerrainMeshCaptureAssetWriter.ChunkBakeItem chunk = chunks[i];
                string meshPath = i < meshPaths.Count ? meshPaths[i] : string.Empty;
                Mesh mesh = string.IsNullOrEmpty(meshPath) ? null : AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null)
                {
                    continue;
                }

                var child = new GameObject(chunk.Name)
                {
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                };
                child.transform.SetParent(parent, false);
                Vector3 chunkPivot = new Vector3(chunk.Rect.center.x, 0f, chunk.Rect.center.y);
                child.transform.localPosition = chunkPivot - areaPivot;

                MeshFilter meshFilter = child.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = mesh;

                string materialPath = i < materialPaths.Count ? materialPaths[i] : string.Empty;
                Material material = string.IsNullOrEmpty(materialPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material != null)
                {
                    meshRenderer.sharedMaterial = material;
                }

                loadedAny = true;
            }

            return loadedAny;
        }

        private static T LoadLatestAsset<T>(string folder, string assetName, string extension) where T : Object
        {
            string expectedPath = BuildExpectedAssetPath(folder, assetName, extension);
            T asset = AssetDatabase.LoadAssetAtPath<T>(expectedPath);
            if (asset != null)
            {
                return asset;
            }

            if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            string typeFilter = typeof(T) == typeof(GameObject) ? "Prefab" : typeof(T).Name;
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:{typeFilter}", new[] { folder });
            string latestPath = string.Empty;
            System.DateTime latestWriteTime = System.DateTime.MinValue;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!Path.GetFileNameWithoutExtension(path).StartsWith(assetName, System.StringComparison.Ordinal)
                    || !path.EndsWith($".{extension}", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                System.DateTime writeTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : System.DateTime.MinValue;
                if (writeTime <= latestWriteTime)
                {
                    continue;
                }

                latestWriteTime = writeTime;
                latestPath = path;
            }

            return string.IsNullOrEmpty(latestPath) ? null : AssetDatabase.LoadAssetAtPath<T>(latestPath);
        }

        private static string BuildExpectedAssetPath(string folder, string assetName, string extension)
        {
            folder = string.IsNullOrWhiteSpace(folder) ? "Assets" : folder.Replace("\\", "/").TrimEnd('/');
            return $"{folder}/{Sanitize(assetName)}.{extension}";
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

        private static void ApplyDontSave(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
        }

        private readonly struct LastBakeRecord
        {
            public LastBakeRecord(
                Rect areaRect,
                List<TerrainMeshCaptureAssetWriter.ChunkBakeItem> chunks,
                List<string> meshPaths,
                List<string> materialPaths,
                List<string> prefabPaths)
            {
                AreaRect = areaRect;
                Chunks = new List<TerrainMeshCaptureAssetWriter.ChunkBakeItem>(chunks);
                MeshPaths = new List<string>(meshPaths);
                MaterialPaths = new List<string>(materialPaths);
                PrefabPaths = new List<string>(prefabPaths);
            }

            public Rect AreaRect { get; }
            public List<TerrainMeshCaptureAssetWriter.ChunkBakeItem> Chunks { get; }
            public List<string> MeshPaths { get; }
            public List<string> MaterialPaths { get; }
            public List<string> PrefabPaths { get; }
            public bool HasMeshes => Chunks != null && MeshPaths != null && MeshPaths.Count > 0;
            public bool HasPrefab => PrefabPaths != null && PrefabPaths.Count > 0;
        }
    }
}
