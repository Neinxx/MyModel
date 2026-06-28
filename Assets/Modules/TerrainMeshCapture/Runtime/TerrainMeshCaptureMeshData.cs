using System.Collections.Generic;
using UnityEngine;

namespace TerrainMeshCapture
{
    internal sealed class TerrainMeshBuildData
    {
        public TerrainMeshBuildData(int vertexCapacity, int triangleCapacity, TerrainMeshCaptureSettings settings)
        {
            Vertices = new List<Vector3>(vertexCapacity);
            Normals = settings.GenerateNormals ? new List<Vector3>(vertexCapacity) : null;
            Uv = new List<Vector2>(vertexCapacity);
            Uv2 = settings.GenerateUv2 ? new List<Vector2>(vertexCapacity) : null;
            Triangles = new List<int>(triangleCapacity);
        }

        public List<Vector3> Vertices { get; }
        public List<Vector3> Normals { get; }
        public List<Vector2> Uv { get; }
        public List<Vector2> Uv2 { get; }
        public List<int> Triangles { get; }
        public int VertexCount => Vertices.Count;

        public int AddSample(TerrainMeshSampleContext sampler, int x, int z, int samplesX, int samplesZ)
        {
            return AddPoint(sampler.SampleAtIndex(x, z, samplesX, samplesZ));
        }

        public int AddPoint(TerrainMeshSamplePoint point)
        {
            int index = Vertices.Count;
            Vertices.Add(point.Position);
            Normals?.Add(point.Normal);
            Uv.Add(point.Uv);
            Uv2?.Add(point.Uv2);
            return index;
        }

        public void AddTriangle(int a, int b, int c)
        {
            if (a == b || b == c || c == a)
            {
                return;
            }

            Vector3 va = Vertices[a];
            Vector3 vb = Vertices[b];
            Vector3 vc = Vertices[c];
            float signedArea = (vb.x - va.x) * (vc.z - va.z) - (vb.z - va.z) * (vc.x - va.x);
            if (signedArea > 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }

            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        public void AddTriangleUnchecked(int a, int b, int c)
        {
            if (a == b || b == c || c == a)
            {
                return;
            }

            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        public void ApplyPivot(Vector3 pivot)
        {
            for (int i = 0; i < Vertices.Count; i++)
            {
                Vertices[i] -= pivot;
            }
        }

    }

    internal readonly struct TerrainMeshSampleContext
    {
        private readonly TerrainData terrainData;
        private readonly Rect rect;
        private readonly TerrainMeshCaptureSettings settings;
        private readonly TerrainMeshHeightRegion heightRegion;
        private readonly bool useCachedHeights;

        public TerrainMeshSampleContext(
            TerrainData terrainData,
            Rect rect,
            TerrainMeshCaptureSettings settings,
            TerrainMeshHeightRegion heightRegion)
        {
            this.terrainData = terrainData;
            this.rect = rect;
            this.settings = settings;
            this.heightRegion = heightRegion;
            useCachedHeights = settings.HeightSamplingMode == TerrainMeshHeightSamplingMode.CachedHeightmap
                && heightRegion.IsValid;
        }

        public TerrainMeshSamplePoint SampleAtIndex(int x, int z, int samplesX, int samplesZ)
        {
            float u = samplesX > 1 ? (float)x / (samplesX - 1) : 0f;
            float v = samplesZ > 1 ? (float)z / (samplesZ - 1) : 0f;
            return SampleAtUv(u, v);
        }

        public float SampleHeightAtIndex(int x, int z)
        {
            float u = settings.SamplesX > 1 ? (float)x / (settings.SamplesX - 1) : 0f;
            float v = settings.SamplesZ > 1 ? (float)z / (settings.SamplesZ - 1) : 0f;
            float terrainX = Mathf.Lerp(rect.xMin, rect.xMax, u);
            float terrainZ = Mathf.Lerp(rect.yMin, rect.yMax, v);
            return SampleHeight(terrainX, terrainZ);
        }

        private TerrainMeshSamplePoint SampleAtUv(float u, float v)
        {
            float terrainX = Mathf.Lerp(rect.xMin, rect.xMax, u);
            float terrainZ = Mathf.Lerp(rect.yMin, rect.yMax, v);
            float normalizedX = terrainData.size.x > 0f ? terrainX / terrainData.size.x : 0f;
            float normalizedZ = terrainData.size.z > 0f ? terrainZ / terrainData.size.z : 0f;
            float height = SampleHeight(terrainX, terrainZ) + settings.HeightOffset;
            Vector3 normal = terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
            return new TerrainMeshSamplePoint(
                new Vector3(terrainX, height, terrainZ),
                normal,
                new Vector2(u, v),
                new Vector2(normalizedX, normalizedZ));
        }

        private float SampleHeight(float terrainX, float terrainZ)
        {
            if (useCachedHeights)
            {
                return heightRegion.Sample(terrainX, terrainZ);
            }

            float normalizedX = terrainData.size.x > 0f ? terrainX / terrainData.size.x : 0f;
            float normalizedZ = terrainData.size.z > 0f ? terrainZ / terrainData.size.z : 0f;
            return terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
        }
    }

    internal readonly struct TerrainMeshSamplePoint
    {
        public TerrainMeshSamplePoint(Vector3 position, Vector3 normal, Vector2 uv, Vector2 uv2)
        {
            Position = position;
            Normal = normal;
            Uv = uv;
            Uv2 = uv2;
        }

        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public Vector2 Uv { get; }
        public Vector2 Uv2 { get; }
    }

    internal readonly struct TerrainMeshHeightRegion
    {
        private readonly TerrainData terrainData;
        private readonly float[,] data;
        private readonly int startX;
        private readonly int startZ;
        private readonly int width;
        private readonly int height;

        private TerrainMeshHeightRegion(TerrainData terrainData, float[,] data, int startX, int startZ, int width, int height)
        {
            this.terrainData = terrainData;
            this.data = data;
            this.startX = startX;
            this.startZ = startZ;
            this.width = width;
            this.height = height;
        }

        public bool IsValid => terrainData != null && data != null && width > 0 && height > 0;

        public static TerrainMeshHeightRegion Read(TerrainData terrainData, Rect rect)
        {
            if (terrainData == null)
            {
                return default;
            }

            int resolution = terrainData.heightmapResolution;
            float x0 = terrainData.size.x > 0f ? Mathf.Clamp01(rect.xMin / terrainData.size.x) * (resolution - 1) : 0f;
            float x1 = terrainData.size.x > 0f ? Mathf.Clamp01(rect.xMax / terrainData.size.x) * (resolution - 1) : 0f;
            float z0 = terrainData.size.z > 0f ? Mathf.Clamp01(rect.yMin / terrainData.size.z) * (resolution - 1) : 0f;
            float z1 = terrainData.size.z > 0f ? Mathf.Clamp01(rect.yMax / terrainData.size.z) * (resolution - 1) : 0f;

            int startX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(x0, x1)), 0, resolution - 1);
            int endX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(x0, x1)), 0, resolution - 1);
            int startZ = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(z0, z1)), 0, resolution - 1);
            int endZ = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(z0, z1)), 0, resolution - 1);
            int width = Mathf.Max(1, endX - startX + 1);
            int height = Mathf.Max(1, endZ - startZ + 1);
            float[,] data = terrainData.GetHeights(startX, startZ, width, height);
            return new TerrainMeshHeightRegion(terrainData, data, startX, startZ, width, height);
        }

        public float Sample(float terrainLocalX, float terrainLocalZ)
        {
            if (!IsValid)
            {
                return 0f;
            }

            int resolution = terrainData.heightmapResolution;
            float heightX = terrainData.size.x > 0f
                ? Mathf.Clamp01(terrainLocalX / terrainData.size.x) * (resolution - 1)
                : 0f;
            float heightZ = terrainData.size.z > 0f
                ? Mathf.Clamp01(terrainLocalZ / terrainData.size.z) * (resolution - 1)
                : 0f;

            float localX = Mathf.Clamp(heightX - startX, 0f, width - 1);
            float localZ = Mathf.Clamp(heightZ - startZ, 0f, height - 1);
            int x0 = Mathf.FloorToInt(localX);
            int z0 = Mathf.FloorToInt(localZ);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int z1 = Mathf.Min(z0 + 1, height - 1);
            float tx = localX - x0;
            float tz = localZ - z0;

            float a = data[z0, x0];
            float b = data[z0, x1];
            float c = data[z1, x0];
            float d = data[z1, x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz) * terrainData.size.y;
        }
    }
}
