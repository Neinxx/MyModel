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
        [Range(0.05f, 1f)]
        [SerializeField] private float simplifyTargetRatio = 0.45f;
        [Min(0f)]
        [SerializeField] private float simplifyMaxError = 0.15f;
        [SerializeField] private bool generateNormals = true;
        [SerializeField] private bool generateTangents;
        [SerializeField] private bool generateUv2 = true;
        [SerializeField] private TerrainTextureBakeMode textureBakeMode = TerrainTextureBakeMode.Albedo;
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
        public int BlockColumns => GetBlockCount(areaSize.x, SquareBlockSize);
        public int BlockRows => GetBlockCount(areaSize.y, SquareBlockSize);
        public int SamplesX => samplesX;
        public int SamplesZ => samplesZ;
        public float HeightOffset => heightOffset;
        public TerrainCaptureBoundsMode BoundsMode => boundsMode;
        public TerrainMeshHeightSamplingMode HeightSamplingMode => heightSamplingMode;
        public TerrainMeshGenerationMode MeshGenerationMode => meshGenerationMode;
        public bool GenerateSkirts => generateSkirts;
        public float SkirtDepth => skirtDepth;
        public float SimplifyTargetRatio => simplifyTargetRatio;
        public float SimplifyMaxError => simplifyMaxError;
        public bool GenerateNormals => generateNormals;
        public bool GenerateTangents => generateTangents;
        public bool GenerateUv2 => generateUv2;
        public TerrainTextureBakeMode TextureBakeMode => textureBakeMode;
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
            areaSize = SnapAreaSizeToBlockGrid(areaSize, squareBlockSize);
            samplesX = Mathf.Clamp(samplesX, 2, 4097);
            samplesZ = Mathf.Clamp(samplesZ, 2, 4097);
            if (meshGenerationMode == TerrainMeshGenerationMode.SimplifiedGrid)
            {
                meshGenerationMode = TerrainMeshGenerationMode.UniformGrid;
            }

            heightOffset = Mathf.Round(heightOffset);
            skirtDepth = Mathf.Max(0f, Mathf.Round(skirtDepth));
            simplifyTargetRatio = Mathf.Clamp(simplifyTargetRatio, 0.05f, 1f);
            simplifyMaxError = Mathf.Max(0f, simplifyMaxError);
            textureResolution = Mathf.Clamp(textureResolution, 4, 8192);
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
            return folder.StartsWith("Assets") ? folder : fallback;
        }
    }
}
