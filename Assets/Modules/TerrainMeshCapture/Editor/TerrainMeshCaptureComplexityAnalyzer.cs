using UnityEditor;
using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    internal static class TerrainMeshCaptureComplexityAnalyzer
    {
        private const int AnalysisSamples = 129;

        public static bool TryAnalyze(
            TerrainMeshCaptureAssetWriter.TerrainMeshCaptureBakePlan plan,
            TerrainMeshCaptureProfile profile,
            out TerrainComplexityAnalysis analysis,
            out string error)
        {
            analysis = default;
            error = string.Empty;
            if (plan.Terrain == null || plan.Terrain.terrainData == null)
            {
                error = "Source terrain is missing.";
                return false;
            }

            if (profile == null)
            {
                error = "Bake profile is missing.";
                return false;
            }

            TerrainData terrainData = plan.Terrain.terrainData;
            Rect rect = plan.AreaRect;
            int samplesX = Mathf.Clamp(Mathf.CeilToInt(rect.width) + 1, 17, AnalysisSamples);
            int samplesZ = Mathf.Clamp(Mathf.CeilToInt(rect.height) + 1, 17, AnalysisSamples);
            float[,] heights = new float[samplesZ, samplesX];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int z = 0; z < samplesZ; z++)
            {
                float v = samplesZ > 1 ? (float)z / (samplesZ - 1) : 0f;
                float localZ = Mathf.Lerp(rect.yMin, rect.yMax, v);
                float normalizedZ = terrainData.size.z > 0f ? Mathf.Clamp01(localZ / terrainData.size.z) : 0f;
                for (int x = 0; x < samplesX; x++)
                {
                    float u = samplesX > 1 ? (float)x / (samplesX - 1) : 0f;
                    float localX = Mathf.Lerp(rect.xMin, rect.xMax, u);
                    float normalizedX = terrainData.size.x > 0f ? Mathf.Clamp01(localX / terrainData.size.x) : 0f;
                    float height = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
                    heights[z, x] = height;
                    minHeight = Mathf.Min(minHeight, height);
                    maxHeight = Mathf.Max(maxHeight, height);
                }
            }

            float dx = samplesX > 1 ? rect.width / (samplesX - 1) : 1f;
            float dz = samplesZ > 1 ? rect.height / (samplesZ - 1) : 1f;
            float slopeSum = 0f;
            float roughnessSum = 0f;
            int metricCount = 0;

            for (int z = 1; z < samplesZ - 1; z++)
            {
                for (int x = 1; x < samplesX - 1; x++)
                {
                    float gradientX = (heights[z, x + 1] - heights[z, x - 1]) / Mathf.Max(0.001f, dx * 2f);
                    float gradientZ = (heights[z + 1, x] - heights[z - 1, x]) / Mathf.Max(0.001f, dz * 2f);
                    float slope = Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ);
                    float neighborAverage = (heights[z, x - 1] + heights[z, x + 1] + heights[z - 1, x] + heights[z + 1, x]) * 0.25f;
                    slopeSum += slope;
                    roughnessSum += Mathf.Abs(heights[z, x] - neighborAverage);
                    metricCount++;
                }
            }

            float heightRange = Mathf.Max(0f, maxHeight - minHeight);
            float averageSlope = metricCount > 0 ? slopeSum / metricCount : 0f;
            float roughness = metricCount > 0 ? roughnessSum / metricCount : 0f;
            float normalizedRange = Mathf.Clamp01(heightRange / Mathf.Max(1f, terrainData.size.y * 0.25f));
            float normalizedSlope = Mathf.Clamp01(averageSlope / 1.2f);
            float normalizedRoughness = Mathf.Clamp01(roughness / Mathf.Max(0.05f, terrainData.size.y * 0.0015f));
            float complexity = Mathf.Clamp01(normalizedRange * 0.25f + normalizedSlope * 0.35f + normalizedRoughness * 0.4f);

            float chunkWidth = profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize ? profile.SquareBlockSize : rect.width;
            float chunkDepth = profile.BakeScope == TerrainCaptureBakeScope.SplitByBlockSize ? profile.SquareBlockSize : rect.height;
            float targetSpacing = Mathf.Lerp(2.5f, 0.5f, complexity);
            int recommendedSamplesX = NextPowerOfTwoPlusOne(Mathf.CeilToInt(chunkWidth / targetSpacing) + 1);
            int recommendedSamplesZ = NextPowerOfTwoPlusOne(Mathf.CeilToInt(chunkDepth / targetSpacing) + 1);
            recommendedSamplesX = Mathf.Clamp(recommendedSamplesX, 33, 1025);
            recommendedSamplesZ = Mathf.Clamp(recommendedSamplesZ, 33, 1025);

            int uniformTriangles = Mathf.Max(2, (recommendedSamplesX - 1) * (recommendedSamplesZ - 1) * 2);
            int boundaryBudget = Mathf.Min(uniformTriangles, Mathf.Max(32, (recommendedSamplesX + recommendedSamplesZ) * 2));
            float triangleRatio = Mathf.Lerp(0.35f, 0.85f, complexity);
            int maxTriangles = Mathf.Clamp(Mathf.RoundToInt(uniformTriangles * triangleRatio), boundaryBudget, uniformTriangles);
            float terrainScaleError = terrainData.size.y * Mathf.Lerp(0.0006f, 0.00012f, complexity);
            float detailError = Mathf.Max(roughness * 1.5f, heightRange * Mathf.Lerp(0.006f, 0.0015f, complexity));
            float maxHeightError = Mathf.Clamp(Mathf.Min(terrainScaleError, detailError), 0.02f, 0.5f);
            int textureResolution = complexity > 0.7f ? 2048 : complexity > 0.35f ? 1024 : 512;
            float skirtDepth = Mathf.Max(1f, Mathf.Ceil(Mathf.Max(maxHeightError * 2f, roughness * 4f)));

            analysis = new TerrainComplexityAnalysis(
                complexity,
                heightRange,
                averageSlope,
                roughness,
                recommendedSamplesX,
                recommendedSamplesZ,
                maxHeightError,
                maxTriangles,
                textureResolution,
                skirtDepth);
            return true;
        }

        public static void ApplyToProfile(TerrainMeshCaptureProfile profile, TerrainComplexityAnalysis analysis)
        {
            if (profile == null)
            {
                return;
            }

            Undo.RecordObject(profile, "Apply Terrain Complexity Recommendations");
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("samplesX").intValue = analysis.RecommendedSamplesX;
            serializedProfile.FindProperty("samplesZ").intValue = analysis.RecommendedSamplesZ;
            serializedProfile.FindProperty("textureSizeMode").intValue = (int)TerrainTextureSizeMode.MatchAreaAspect;
            serializedProfile.FindProperty("textureResolution").intValue = analysis.RecommendedTextureResolution;
            serializedProfile.FindProperty("generateSkirts").boolValue = true;
            serializedProfile.FindProperty("skirtDepth").floatValue = analysis.RecommendedSkirtDepth;
            serializedProfile.ApplyModifiedProperties();
            profile.Sanitize();
            EditorUtility.SetDirty(profile);
        }

        private static int NextPowerOfTwoPlusOne(int value)
        {
            int edgeSegments = Mathf.Max(16, value - 1);
            int power = 16;
            while (power < edgeSegments && power < 1024)
            {
                power <<= 1;
            }

            return power + 1;
        }
    }

    internal readonly struct TerrainComplexityAnalysis
    {
        public TerrainComplexityAnalysis(
            float complexity,
            float heightRange,
            float averageSlope,
            float roughness,
            int recommendedSamplesX,
            int recommendedSamplesZ,
            float recommendedMaxHeightError,
            int recommendedMaxTriangles,
            int recommendedTextureResolution,
            float recommendedSkirtDepth)
        {
            Complexity = complexity;
            HeightRange = heightRange;
            AverageSlope = averageSlope;
            Roughness = roughness;
            RecommendedSamplesX = recommendedSamplesX;
            RecommendedSamplesZ = recommendedSamplesZ;
            RecommendedMaxHeightError = recommendedMaxHeightError;
            RecommendedMaxTriangles = recommendedMaxTriangles;
            RecommendedTextureResolution = recommendedTextureResolution;
            RecommendedSkirtDepth = recommendedSkirtDepth;
        }

        public float Complexity { get; }
        public float HeightRange { get; }
        public float AverageSlope { get; }
        public float Roughness { get; }
        public int RecommendedSamplesX { get; }
        public int RecommendedSamplesZ { get; }
        public float RecommendedMaxHeightError { get; }
        public int RecommendedMaxTriangles { get; }
        public int RecommendedTextureResolution { get; }
        public float RecommendedSkirtDepth { get; }
    }
}
