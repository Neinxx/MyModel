using System;
using UnityEngine;

namespace TerrainMeshCapture
{
    public enum TerrainCaptureBoundsMode
    {
        RejectOutOfBounds = 0,
        ClampToTerrain = 1
    }

    public enum TerrainTextureBakeMode
    {
        None = 0,
        Albedo = 1,
        SplatWeights = 2,
        [InspectorName("Normal Map")]
        NormalMap = 3
    }

    [Flags]
    public enum TerrainTextureBakeOutputs
    {
        None = 0,
        Albedo = 1,
        [InspectorName("Normal Map")]
        NormalMap = 2,
        SplatWeights = 4,
        All = Albedo | NormalMap | SplatWeights
    }

    public enum TerrainTextureSizeMode
    {
        [InspectorName("Fixed Square")]
        FixedSquare = 0,
        [InspectorName("Match Area Aspect")]
        MatchAreaAspect = 1
    }

    public enum TerrainCaptureBakeScope
    {
        SingleArea = 0,
        SplitByBlockSize = 1
    }

    public enum TerrainCaptureAssetWriteMode
    {
        ReplaceByName = 0,
        GenerateUnique = 1
    }

    public enum TerrainMeshHeightSamplingMode
    {
        CachedHeightmap = 0,
        InterpolatedTerrain = 1
    }

    public enum TerrainMeshGenerationMode
    {
        UniformGrid = 0,
        [InspectorName("Adaptive Height TIN")]
        AdaptiveHeightTin = 2
    }

    [Serializable]
    public sealed class TerrainMeshCaptureSettings
    {
        [Min(0.01f)]
        [SerializeField] private Vector2 size = new Vector2(16f, 16f);
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
        [SerializeField] private bool generateUv2;
        [SerializeField] private TerrainTextureBakeMode textureBakeMode = TerrainTextureBakeMode.Albedo;
        [SerializeField] private TerrainTextureBakeOutputs textureBakeOutputs = TerrainTextureBakeOutputs.All;
        [SerializeField] private TerrainTextureSizeMode textureSizeMode = TerrainTextureSizeMode.MatchAreaAspect;
        [Min(4)]
        [SerializeField] private int textureResolution = 1024;
        [SerializeField] private Color fallbackAlbedo = Color.white;
        [SerializeField] private bool textureMipMaps;
        private TerrainMeshAdaptiveEdgeConstraints adaptiveEdgeConstraints;

        public Vector2 Size => size;
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
        public Color FallbackAlbedo => fallbackAlbedo;
        public bool TextureMipMaps => textureMipMaps;
        public TerrainMeshAdaptiveEdgeConstraints AdaptiveEdgeConstraints => adaptiveEdgeConstraints;

        public void ApplyProfile(TerrainMeshCaptureProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.Sanitize();
            size = profile.AreaSize;
            samplesX = profile.SamplesX;
            samplesZ = profile.SamplesZ;
            heightOffset = profile.HeightOffset;
            boundsMode = profile.BoundsMode;
            heightSamplingMode = profile.HeightSamplingMode;
            meshGenerationMode = profile.MeshGenerationMode;
            generateSkirts = profile.GenerateSkirts;
            skirtDepth = profile.SkirtDepth;
            adaptiveMaxHeightError = profile.AdaptiveMaxHeightError;
            adaptiveMinCellSamples = profile.AdaptiveMinCellSamples;
            adaptiveMaxTriangles = profile.AdaptiveMaxTriangles;
            adaptiveCurvatureThreshold = profile.AdaptiveCurvatureThreshold;
            adaptiveCurvaturePenalty = profile.AdaptiveCurvaturePenalty;
            adaptiveFlipPasses = profile.AdaptiveFlipPasses;
            generateNormals = profile.GenerateNormals;
            generateTangents = profile.GenerateTangents;
            generateUv2 = profile.GenerateUv2;
            textureBakeMode = profile.TextureBakeMode;
            textureBakeOutputs = profile.TextureBakeOutputs;
            textureSizeMode = profile.TextureSizeMode;
            textureResolution = profile.TextureResolution;
            fallbackAlbedo = profile.FallbackAlbedo;
            textureMipMaps = profile.TextureMipMaps;
        }

        public void SetAdaptiveEdgeConstraints(TerrainMeshAdaptiveEdgeConstraints constraints)
        {
            adaptiveEdgeConstraints = constraints;
        }

        public void Sanitize()
        {
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
            samplesX = Mathf.Clamp(samplesX, 2, 4097);
            samplesZ = Mathf.Clamp(samplesZ, 2, 4097);
            int adaptiveUniformTriangleLimit = Mathf.Max(2, (samplesX - 1) * (samplesZ - 1) * 2);
            int adaptiveBoundaryTriangleBudget = Mathf.Min(adaptiveUniformTriangleLimit, Mathf.Max(32, (samplesX + samplesZ) * 2));
            int adaptiveMaxCellSamples = Mathf.Max(1, Mathf.Min(samplesX - 1, samplesZ - 1));

            skirtDepth = Mathf.Max(0f, skirtDepth);
            adaptiveMaxHeightError = Mathf.Max(0f, adaptiveMaxHeightError);
            adaptiveMinCellSamples = Mathf.Clamp(adaptiveMinCellSamples, 1, adaptiveMaxCellSamples);
            adaptiveMaxTriangles = Mathf.Clamp(adaptiveMaxTriangles, adaptiveBoundaryTriangleBudget, Mathf.Min(2000000, adaptiveUniformTriangleLimit));
            adaptiveCurvatureThreshold = Mathf.Clamp01(adaptiveCurvatureThreshold);
            adaptiveCurvaturePenalty = Mathf.Max(0f, adaptiveCurvaturePenalty);
            adaptiveFlipPasses = Mathf.Clamp(adaptiveFlipPasses, 0, 256);
            textureBakeOutputs &= TerrainTextureBakeOutputs.All;
            if (textureBakeOutputs == TerrainTextureBakeOutputs.None && textureBakeMode != TerrainTextureBakeMode.None)
            {
                textureBakeOutputs = ToTextureBakeOutput(textureBakeMode);
            }

            if ((textureBakeOutputs & TerrainTextureBakeOutputs.NormalMap) != 0)
            {
                generateTangents = true;
            }

            textureBakeMode = GetPrimaryTextureBakeMode(textureBakeOutputs);
            textureResolution = Mathf.Clamp(textureResolution, 4, 8192);
        }

        public void SetTextureBakeMode(TerrainTextureBakeMode mode)
        {
            textureBakeMode = mode;
            textureBakeOutputs = ToTextureBakeOutput(mode);
        }

        public Vector2Int ResolveTextureSize(Rect terrainLocalRect)
        {
            return TerrainTextureSizeUtility.Resolve(textureSizeMode, textureResolution, terrainLocalRect);
        }

        public static TerrainTextureBakeOutputs ToTextureBakeOutput(TerrainTextureBakeMode mode)
        {
            switch (mode)
            {
                case TerrainTextureBakeMode.Albedo:
                    return TerrainTextureBakeOutputs.Albedo;
                case TerrainTextureBakeMode.SplatWeights:
                    return TerrainTextureBakeOutputs.SplatWeights;
                case TerrainTextureBakeMode.NormalMap:
                    return TerrainTextureBakeOutputs.NormalMap;
                default:
                    return TerrainTextureBakeOutputs.None;
            }
        }

        public static TerrainTextureBakeMode GetPrimaryTextureBakeMode(TerrainTextureBakeOutputs outputs)
        {
            if ((outputs & TerrainTextureBakeOutputs.Albedo) != 0)
            {
                return TerrainTextureBakeMode.Albedo;
            }

            if ((outputs & TerrainTextureBakeOutputs.NormalMap) != 0)
            {
                return TerrainTextureBakeMode.NormalMap;
            }

            return (outputs & TerrainTextureBakeOutputs.SplatWeights) != 0
                ? TerrainTextureBakeMode.SplatWeights
                : TerrainTextureBakeMode.None;
        }
    }

    internal static class TerrainTextureSizeUtility
    {
        public static Vector2Int Resolve(TerrainTextureSizeMode mode, int resolution, Rect terrainLocalRect)
        {
            int longSide = Mathf.Clamp(resolution, 4, 8192);
            if (mode == TerrainTextureSizeMode.FixedSquare)
            {
                return new Vector2Int(longSide, longSide);
            }

            float width = Mathf.Max(0.01f, terrainLocalRect.width);
            float height = Mathf.Max(0.01f, terrainLocalRect.height);
            if (width >= height)
            {
                return new Vector2Int(longSide, Mathf.Clamp(Mathf.RoundToInt(longSide * height / width), 4, 8192));
            }

            return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(longSide * width / height), 4, 8192), longSide);
        }
    }

    public sealed class TerrainMeshAdaptiveEdgeConstraints
    {
        public int[] BottomX { get; set; }
        public int[] TopX { get; set; }
        public int[] LeftZ { get; set; }
        public int[] RightZ { get; set; }
        public bool LockBottom { get; set; }
        public bool LockTop { get; set; }
        public bool LockLeft { get; set; }
        public bool LockRight { get; set; }
    }
}
