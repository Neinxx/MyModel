using UnityEngine;

namespace TerrainMeshCapture
{
    public readonly struct TerrainMeshCaptureResult
    {
        public TerrainMeshCaptureResult(Mesh mesh, Rect terrainLocalRect, Vector3 terrainLocalPivot)
        {
            Mesh = mesh;
            TerrainLocalRect = terrainLocalRect;
            TerrainLocalPivot = terrainLocalPivot;
        }

        public Mesh Mesh { get; }
        public Rect TerrainLocalRect { get; }
        public Vector3 TerrainLocalPivot { get; }
    }
}
