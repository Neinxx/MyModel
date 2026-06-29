namespace TerrainMeshCapture
{
    internal sealed class TerrainMeshUniformGridGenerator : ITerrainMeshGenerator
    {
        public TerrainMeshGenerationMode Mode => TerrainMeshGenerationMode.UniformGrid;

        public TerrainMeshBuildData Generate(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings)
        {
            return TerrainMeshCaptureBaker.BuildUniformMeshData(sampler, samplesX, samplesZ, settings);
        }
    }
}
