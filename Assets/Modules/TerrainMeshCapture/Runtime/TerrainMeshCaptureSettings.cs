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
        SplatWeights = 2
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
        SimplifiedGrid = 2
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
        [Range(0.05f, 1f)]
        [SerializeField] private float simplifyTargetRatio = 0.45f;
        [Min(0f)]
        [SerializeField] private float simplifyMaxError = 0.15f;
        [SerializeField] private bool generateNormals = true;
        [SerializeField] private bool generateTangents;
        [SerializeField] private bool generateUv2;
        [SerializeField] private TerrainTextureBakeMode textureBakeMode = TerrainTextureBakeMode.Albedo;
        [Min(4)]
        [SerializeField] private int textureResolution = 1024;
        [SerializeField] private Color fallbackAlbedo = Color.white;
        [SerializeField] private bool textureMipMaps;

        public Vector2 Size => size;
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
        public Color FallbackAlbedo => fallbackAlbedo;
        public bool TextureMipMaps => textureMipMaps;

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
            simplifyTargetRatio = profile.SimplifyTargetRatio;
            simplifyMaxError = profile.SimplifyMaxError;
            generateNormals = profile.GenerateNormals;
            generateTangents = profile.GenerateTangents;
            generateUv2 = profile.GenerateUv2;
            textureBakeMode = profile.TextureBakeMode;
            textureResolution = profile.TextureResolution;
            fallbackAlbedo = profile.FallbackAlbedo;
            textureMipMaps = profile.TextureMipMaps;
        }

        public void Sanitize()
        {
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
            samplesX = Mathf.Clamp(samplesX, 2, 4097);
            samplesZ = Mathf.Clamp(samplesZ, 2, 4097);
            if (meshGenerationMode == TerrainMeshGenerationMode.SimplifiedGrid)
            {
                meshGenerationMode = TerrainMeshGenerationMode.UniformGrid;
            }

            skirtDepth = Mathf.Max(0f, skirtDepth);
            simplifyTargetRatio = Mathf.Clamp(simplifyTargetRatio, 0.05f, 1f);
            simplifyMaxError = Mathf.Max(0f, simplifyMaxError);
            textureResolution = Mathf.Clamp(textureResolution, 4, 8192);
        }
    }
}
