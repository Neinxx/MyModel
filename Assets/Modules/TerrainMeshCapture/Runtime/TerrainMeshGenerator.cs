namespace TerrainMeshCapture
{
    internal interface ITerrainMeshGenerator
    {
        TerrainMeshGenerationMode Mode { get; }

        TerrainMeshBuildData Generate(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings);
    }

    internal static class TerrainMeshGeneratorRegistry
    {
        private static readonly ITerrainMeshGenerator UniformGrid = new TerrainMeshUniformGridGenerator();
        private static readonly ITerrainMeshGenerator AdaptiveHeightTin = new TerrainMeshAdaptiveHeightTinGenerator();

        public static ITerrainMeshGenerator Get(TerrainMeshGenerationMode mode)
        {
            switch (mode)
            {
                case TerrainMeshGenerationMode.AdaptiveHeightTin:
                    return AdaptiveHeightTin;
                default:
                    return UniformGrid;
            }
        }
    }
}
