using System;
using UnityEngine;

namespace TerrainMeshCapture
{
    [CreateAssetMenu(menuName = "Terrain Mesh Capture/Bake Profile", fileName = "TerrainMeshCaptureProfile")]
    public sealed class TerrainMeshCaptureProfile : ScriptableObject
    {
        [SerializeField] private string assetName = "TerrainCapture";
        [SerializeField] private string meshOutputFolder = "Assets/TerrainMeshCaptures/Meshes";
        [SerializeField] private string textureOutputFolder = "Assets/TerrainMeshCaptures/Textures";
        [SerializeField] private string materialOutputFolder = "Assets/TerrainMeshCaptures/Materials";
        [SerializeField] private string prefabOutputFolder = "Assets/TerrainMeshCaptures/Prefabs";
        [SerializeField] private TerrainCaptureAssetWriteMode writeMode = TerrainCaptureAssetWriteMode.ReplaceByName;
        [SerializeField] private Shader shader;
        [SerializeField] private TerrainCaptureBakeScope bakeScope = TerrainCaptureBakeScope.SplitByBlockSize;
        [Min(0.01f)]
        [SerializeField] private Vector2 areaSize = new Vector2(64f, 64f);
        [Min(0.01f)]
        [SerializeField] private Vector2 blockSize = new Vector2(16f, 16f);
        [Min(2)]
        [SerializeField] private int samplesX = 65;
        [Min(2)]
        [SerializeField] private int samplesZ = 65;
        [SerializeField] private float heightOffset;
        [SerializeField] private TerrainCaptureBoundsMode boundsMode = TerrainCaptureBoundsMode.RejectOutOfBounds;
        [SerializeField] private TerrainMeshHeightSamplingMode heightSamplingMode = TerrainMeshHeightSamplingMode.CachedHeightmap;
        [SerializeField] private TerrainMeshGenerationMode meshGenerationMode = TerrainMeshGenerationMode.UniformGrid;
        [SerializeField] private bool generateSkirts;
        [Min(0f)]
        [SerializeField] private float skirtDepth = 1f;
        [Min(0f)]
        [SerializeField] private float adaptiveMaxHeightError = 0.25f;
        [Min(1)]
        [SerializeField] private int adaptiveMinCellSamples = 1;
        [Min(32)]
        [SerializeField] private int adaptiveMaxTriangles = 8192;
        [Range(0f, 1f)]
        [SerializeField] private float adaptiveCurvatureThreshold = 0.35f;
        [Min(0f)]
        [SerializeField] private float adaptiveCurvaturePenalty = 12f;
        [Range(0, 256)]
        [SerializeField] private int adaptiveFlipPasses = 64;
        [SerializeField] private bool generateNormals = true;
        [SerializeField] private bool generateTangents;
        [SerializeField] private bool generateUv2 = true;
        [SerializeField] private TerrainTextureBakeMode textureBakeMode = TerrainTextureBakeMode.Albedo;
        [SerializeField] private TerrainTextureBakeOutputs textureBakeOutputs = TerrainTextureBakeOutputs.All;
        [SerializeField] private TerrainTextureSizeMode textureSizeMode = TerrainTextureSizeMode.MatchAreaAspect;
        [Min(4)]
        [SerializeField] private int textureResolution = 1024;
        [SerializeField] private bool textureMipMaps;
        [SerializeField] private Color fallbackAlbedo = Color.white;
        [SerializeField] private bool createMaterials = true;
        [SerializeField] private bool createPrefab = true;

        public string AssetName => assetName;
        public string MeshOutputFolder => meshOutputFolder;
        public string TextureOutputFolder => textureOutputFolder;
        public string MaterialOutputFolder => materialOutputFolder;
        public string PrefabOutputFolder => prefabOutputFolder;
        public TerrainCaptureAssetWriteMode WriteMode => writeMode;
        public Shader Shader => shader;
        public TerrainCaptureBakeScope BakeScope => bakeScope;
        public Vector2 AreaSize => areaSize;
        public Vector2 BlockSize => blockSize;
        public float SquareBlockSize => Mathf.Max(1f, Mathf.Round(blockSize.x));
        public int BlockColumns => bakeScope == TerrainCaptureBakeScope.SingleArea ? 1 : GetBlockCount(areaSize.x, SquareBlockSize);
        public int BlockRows => bakeScope == TerrainCaptureBakeScope.SingleArea ? 1 : GetBlockCount(areaSize.y, SquareBlockSize);
        public int SamplesX => samplesX;
        public int SamplesZ => samplesZ;
        public float HeightOffset => heightOffset;
        public TerrainCaptureBoundsMode BoundsMode => boundsMode;
        public TerrainMeshHeightSamplingMode HeightSamplingMode => heightSamplingMode;
        public TerrainMeshGenerationMode MeshGenerationMode => meshGenerationMode;
        public bool GenerateSkirts => generateSkirts;
        public float SkirtDepth => skirtDepth;
        public float AdaptiveMaxHeightError => adaptiveMaxHeightError;
        public int AdaptiveMinCellSamples => adaptiveMinCellSamples;
        public int AdaptiveMaxTriangles => adaptiveMaxTriangles;
        public float AdaptiveCurvatureThreshold => adaptiveCurvatureThreshold;
        public float AdaptiveCurvaturePenalty => adaptiveCurvaturePenalty;
        public int AdaptiveFlipPasses => adaptiveFlipPasses;
        public bool GenerateNormals => generateNormals;
        public bool GenerateTangents => generateTangents;
        public bool GenerateUv2 => generateUv2;
        public TerrainTextureBakeMode TextureBakeMode => textureBakeMode;
        public TerrainTextureBakeOutputs TextureBakeOutputs => textureBakeOutputs;
        public bool HasTextureOutputs => textureBakeOutputs != TerrainTextureBakeOutputs.None;
        public TerrainTextureSizeMode TextureSizeMode => textureSizeMode;
        public int TextureResolution => textureResolution;
        public bool TextureMipMaps => textureMipMaps;
        public Color FallbackAlbedo => fallbackAlbedo;
        public bool CreateMaterials => createMaterials;
        public bool CreatePrefab => createPrefab;

        private void OnValidate()
        {
            Sanitize();
        }

        public void Sanitize()
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                assetName = "TerrainCapture";
            }

            meshOutputFolder = NormalizeFolder(meshOutputFolder, "Assets/TerrainMeshCaptures/Meshes");
            textureOutputFolder = NormalizeFolder(textureOutputFolder, "Assets/TerrainMeshCaptures/Textures");
            materialOutputFolder = NormalizeFolder(materialOutputFolder, "Assets/TerrainMeshCaptures/Materials");
            prefabOutputFolder = NormalizeFolder(prefabOutputFolder, "Assets/TerrainMeshCaptures/Prefabs");

            float squareBlockSize = Mathf.Max(1f, Mathf.Round(blockSize.x));
            blockSize = new Vector2(squareBlockSize, squareBlockSize);
            areaSize = bakeScope == TerrainCaptureBakeScope.SplitByBlockSize
                ? SnapAreaSizeToBlockGrid(areaSize, squareBlockSize)
                : new Vector2(
                    Mathf.Max(1f, Mathf.Round(areaSize.x)),
                    Mathf.Max(1f, Mathf.Round(areaSize.y)));
            samplesX = Mathf.Clamp(samplesX, 2, 4097);
            samplesZ = Mathf.Clamp(samplesZ, 2, 4097);
            int adaptiveUniformTriangleLimit = Mathf.Max(2, (samplesX - 1) * (samplesZ - 1) * 2);
            int adaptiveBoundaryTriangleBudget = Mathf.Min(adaptiveUniformTriangleLimit, Mathf.Max(32, (samplesX + samplesZ) * 2));
            int adaptiveMaxCellSamples = Mathf.Max(1, Mathf.Min(samplesX - 1, samplesZ - 1));

            heightOffset = Mathf.Round(heightOffset);
            skirtDepth = Mathf.Max(0f, Mathf.Round(skirtDepth));
            adaptiveMaxHeightError = Mathf.Max(0f, adaptiveMaxHeightError);
            adaptiveMinCellSamples = Mathf.Clamp(adaptiveMinCellSamples, 1, adaptiveMaxCellSamples);
            adaptiveMaxTriangles = Mathf.Clamp(adaptiveMaxTriangles, adaptiveBoundaryTriangleBudget, Mathf.Min(2000000, adaptiveUniformTriangleLimit));
            adaptiveCurvatureThreshold = Mathf.Clamp01(adaptiveCurvatureThreshold);
            adaptiveCurvaturePenalty = Mathf.Max(0f, adaptiveCurvaturePenalty);
            adaptiveFlipPasses = Mathf.Clamp(adaptiveFlipPasses, 0, 256);
            textureBakeOutputs &= TerrainTextureBakeOutputs.All;
            if (textureBakeOutputs == TerrainTextureBakeOutputs.None && textureBakeMode != TerrainTextureBakeMode.None)
            {
                textureBakeOutputs = TerrainMeshCaptureSettings.ToTextureBakeOutput(textureBakeMode);
            }

            if ((textureBakeOutputs & TerrainTextureBakeOutputs.NormalMap) != 0)
            {
                generateTangents = true;
            }

            textureBakeMode = TerrainMeshCaptureSettings.GetPrimaryTextureBakeMode(textureBakeOutputs);
            textureResolution = Mathf.Clamp(textureResolution, 4, 8192);
        }

        public Vector2Int ResolveTextureSize(Rect terrainLocalRect)
        {
            return TerrainTextureSizeUtility.Resolve(textureSizeMode, textureResolution, terrainLocalRect);
        }

        public static Vector2 SnapAreaSizeToBlockGrid(Vector2 size, float blockSize)
        {
            blockSize = Mathf.Max(1f, Mathf.Round(blockSize));
            return new Vector2(
                GetBlockCount(size.x, blockSize) * blockSize,
                GetBlockCount(size.y, blockSize) * blockSize);
        }

        public static int GetBlockCount(float length, float blockSize)
        {
            blockSize = Mathf.Max(1f, Mathf.Round(blockSize));
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1f, Mathf.Round(length)) / blockSize));
        }

        private static string NormalizeFolder(string folder, string fallback)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return fallback;
            }

            folder = folder.Replace("\\", "/").TrimEnd('/');
            return IsAssetsFolderPath(folder)
                ? folder
                : fallback;
        }

        private static bool IsAssetsFolderPath(string folder)
        {
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
    }
}
