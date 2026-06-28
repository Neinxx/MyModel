using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainMeshCapture
{
    public static class TerrainMeshCaptureBaker
    {
        private const int MaxVertexCount = 4097 * 4097;

        public static bool TryBuildMesh(
            Terrain terrain,
            Rect terrainLocalRect,
            TerrainMeshCaptureSettings settings,
            out TerrainMeshCaptureResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!ValidateInput(terrain, settings, out TerrainData terrainData, out error))
            {
                return false;
            }

            settings.Sanitize();
            int samplesX = settings.SamplesX;
            int samplesZ = settings.SamplesZ;
            int vertexCount = samplesX * samplesZ;
            if (vertexCount > MaxVertexCount)
            {
                error = $"Requested mesh has {vertexCount} vertices. Limit is {MaxVertexCount}.";
                return false;
            }

            Rect rect = terrainLocalRect;
            rect = ResolveRect(rect, terrainData.size, settings.BoundsMode, out bool inside);
            if (!inside)
            {
                error = $"Capture rect {rect} is outside terrain size {terrainData.size}.";
                return false;
            }

            TerrainMeshHeightRegion heightRegion = settings.HeightSamplingMode == TerrainMeshHeightSamplingMode.CachedHeightmap
                ? TerrainMeshHeightRegion.Read(terrainData, rect)
                : default;
            var sampler = new TerrainMeshSampleContext(terrainData, rect, settings, heightRegion);
            TerrainMeshBuildData meshData = BuildMeshData(sampler, samplesX, samplesZ, settings);

            Vector3 pivot = new Vector3(rect.center.x, 0f, rect.center.y);
            meshData.ApplyPivot(pivot);

            var mesh = new Mesh
            {
                name = $"{terrain.name}_Capture_{samplesX}x{samplesZ}",
                indexFormat = meshData.VertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(meshData.Vertices);
            mesh.SetUVs(0, meshData.Uv);
            if (meshData.Uv2 != null)
            {
                mesh.SetUVs(1, meshData.Uv2);
            }

            mesh.SetTriangles(meshData.Triangles, 0, true);
            if (meshData.Normals != null)
            {
                mesh.SetNormals(meshData.Normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            if (settings.GenerateTangents)
            {
                mesh.RecalculateTangents();
            }

            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            result = new TerrainMeshCaptureResult(mesh, rect, pivot);
            return true;
        }

        private static TerrainMeshBuildData BuildMeshData(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings)
        {
            switch (settings.MeshGenerationMode)
            {
                case TerrainMeshGenerationMode.SimplifiedGrid:
                    return BuildSimplifiedMeshData(sampler, samplesX, samplesZ, settings);
                default:
                    return BuildUniformMeshData(sampler, samplesX, samplesZ, settings);
            }
        }

        private static TerrainMeshBuildData BuildUniformMeshData(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings)
        {
            int vertexCount = samplesX * samplesZ;
            var meshData = new TerrainMeshBuildData(vertexCount, (samplesX - 1) * (samplesZ - 1) * 6, settings);
            int[,] indices = new int[samplesZ, samplesX];

            for (int z = 0; z < samplesZ; z++)
            {
                for (int x = 0; x < samplesX; x++)
                {
                    indices[z, x] = meshData.AddSample(sampler, x, z, samplesX, samplesZ);
                }
            }

            for (int z = 0; z < samplesZ - 1; z++)
            {
                for (int x = 0; x < samplesX - 1; x++)
                {
                    int a = indices[z, x];
                    int b = indices[z, x + 1];
                    int c = indices[z + 1, x];
                    int d = indices[z + 1, x + 1];
                    meshData.AddTriangle(a, c, b);
                    meshData.AddTriangle(b, c, d);
                }
            }

            if (settings.GenerateSkirts && settings.SkirtDepth > 0f)
            {
                AddSkirts(meshData, sampler, indices, samplesX, samplesZ, settings.SkirtDepth);
            }

            return meshData;
        }

        private static TerrainMeshBuildData BuildSimplifiedMeshData(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings)
        {
            return BuildUniformMeshData(sampler, samplesX, samplesZ, settings);
        }

        private static void AddSkirts(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            int[,] indices,
            int samplesX,
            int samplesZ,
            float skirtDepth)
        {
            AddSkirtEdge(meshData, sampler, indices, samplesX, samplesZ, 0, 0, 1, 0, samplesX, skirtDepth);
            AddSkirtEdge(meshData, sampler, indices, samplesX, samplesZ, samplesX - 1, 0, 0, 1, samplesZ, skirtDepth);
            AddSkirtEdge(meshData, sampler, indices, samplesX, samplesZ, samplesX - 1, samplesZ - 1, -1, 0, samplesX, skirtDepth);
            AddSkirtEdge(meshData, sampler, indices, samplesX, samplesZ, 0, samplesZ - 1, 0, -1, samplesZ, skirtDepth);
        }

        private static void AddSkirtEdge(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            int[,] surfaceIndices,
            int samplesX,
            int samplesZ,
            int startX,
            int startZ,
            int stepX,
            int stepZ,
            int count,
            float skirtDepth)
        {
            int previousSurface = -1;
            int previousSkirt = -1;
            for (int i = 0; i < count; i++)
            {
                int x = startX + stepX * i;
                int z = startZ + stepZ * i;
                int surface = surfaceIndices[z, x];
                TerrainMeshSamplePoint sample = sampler.SampleAtIndex(x, z, samplesX, samplesZ);
                Vector3 skirtPosition = sample.Position;
                skirtPosition.y -= skirtDepth;
                int skirt = meshData.AddPoint(new TerrainMeshSamplePoint(skirtPosition, sample.Normal, sample.Uv, sample.Uv2));

                if (previousSurface >= 0)
                {
                    meshData.AddTriangleUnchecked(previousSurface, previousSkirt, surface);
                    meshData.AddTriangleUnchecked(surface, previousSkirt, skirt);
                }

                previousSurface = surface;
                previousSkirt = skirt;
            }
        }

        public static bool TryBakeTexture(
            Terrain terrain,
            Rect terrainLocalRect,
            TerrainMeshCaptureSettings settings,
            out Texture2D texture,
            out string error)
        {
            texture = null;
            error = string.Empty;

            if (!ValidateInput(terrain, settings, out TerrainData terrainData, out error))
            {
                return false;
            }

            settings.Sanitize();
            if (settings.TextureBakeMode == TerrainTextureBakeMode.None)
            {
                error = "Texture bake mode is None.";
                return false;
            }

            int resolution = settings.TextureResolution;
            texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, settings.TextureMipMaps, false)
            {
                name = $"{terrain.name}_{settings.TextureBakeMode}_{resolution}"
            };

            if (settings.TextureBakeMode == TerrainTextureBakeMode.SplatWeights)
            {
                BakeSplatWeights(terrainData, terrainLocalRect, texture);
            }
            else
            {
                BakeAlbedo(terrainData, terrainLocalRect, texture, settings.FallbackAlbedo);
            }

            texture.Apply(settings.TextureMipMaps, false);
            return true;
        }

        public static Rect BuildRect(Vector3 terrainLocalCenter, Vector2 size)
        {
            return new Rect(
                terrainLocalCenter.x - size.x * 0.5f,
                terrainLocalCenter.z - size.y * 0.5f,
                size.x,
                size.y);
        }

        public static Rect ResolveRect(Rect rect, Vector3 terrainSize, TerrainCaptureBoundsMode boundsMode, out bool inside)
        {
            inside = rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= terrainSize.x && rect.yMax <= terrainSize.z;
            if (inside || boundsMode == TerrainCaptureBoundsMode.RejectOutOfBounds)
            {
                return rect;
            }

            float xMin = Mathf.Clamp(rect.xMin, 0f, terrainSize.x);
            float xMax = Mathf.Clamp(rect.xMax, 0f, terrainSize.x);
            float zMin = Mathf.Clamp(rect.yMin, 0f, terrainSize.z);
            float zMax = Mathf.Clamp(rect.yMax, 0f, terrainSize.z);
            inside = xMax > xMin && zMax > zMin;
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        private static bool ValidateInput(
            Terrain terrain,
            TerrainMeshCaptureSettings settings,
            out TerrainData terrainData,
            out string error)
        {
            terrainData = null;
            error = string.Empty;

            if (terrain == null)
            {
                error = "Source terrain is missing.";
                return false;
            }

            terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                error = "Source terrain has no TerrainData.";
                return false;
            }

            if (settings == null)
            {
                error = "Capture settings are missing.";
                return false;
            }

            return true;
        }

        private static void BakeSplatWeights(TerrainData terrainData, Rect rect, Texture2D target)
        {
            int layers = terrainData.alphamapLayers;
            int width = target.width;
            int height = target.height;
            var pixels = new Color32[width * height];
            if (layers == 0)
            {
                target.SetPixels32(pixels);
                return;
            }

            AlphaRegion alphaRegion = AlphaRegion.Read(terrainData, rect);

            for (int y = 0; y < height; y++)
            {
                float v = height > 1 ? (float)y / (height - 1) : 0f;
                float localZ = Mathf.Lerp(rect.yMin, rect.yMax, v);

                for (int x = 0; x < width; x++)
                {
                    float u = width > 1 ? (float)x / (width - 1) : 0f;
                    float localX = Mathf.Lerp(rect.xMin, rect.xMax, u);

                    float r = layers > 0 ? alphaRegion.Sample(localX, localZ, 0) : 0f;
                    float g = layers > 1 ? alphaRegion.Sample(localX, localZ, 1) : 0f;
                    float b = layers > 2 ? alphaRegion.Sample(localX, localZ, 2) : 0f;
                    float a = layers > 3 ? alphaRegion.Sample(localX, localZ, 3) : 1f;
                    pixels[y * width + x] = new Color(r, g, b, a);
                }
            }

            target.SetPixels32(pixels);
        }

        private static void BakeAlbedo(TerrainData terrainData, Rect rect, Texture2D target, Color fallback)
        {
            TerrainLayer[] layers = terrainData.terrainLayers;
            int layerCount = layers != null ? layers.Length : 0;
            int width = target.width;
            int height = target.height;
            var pixels = new Color[width * height];

            if (layerCount == 0 || terrainData.alphamapLayers == 0)
            {
                Fill(pixels, fallback);
                target.SetPixels(pixels);
                return;
            }

            AlphaRegion alphaRegion = AlphaRegion.Read(terrainData, rect);
            int blendCount = Mathf.Min(layerCount, terrainData.alphamapLayers);
            var layerSamplers = new LayerSampler[blendCount];
            for (int i = 0; i < blendCount; i++)
            {
                layerSamplers[i] = new LayerSampler(layers[i], fallback);
            }

            for (int y = 0; y < height; y++)
            {
                float v = height > 1 ? (float)y / (height - 1) : 0f;
                float localZ = Mathf.Lerp(rect.yMin, rect.yMax, v);

                for (int x = 0; x < width; x++)
                {
                    float u = width > 1 ? (float)x / (width - 1) : 0f;
                    float localX = Mathf.Lerp(rect.xMin, rect.xMax, u);

                    Color color = Color.clear;
                    for (int layerIndex = 0; layerIndex < blendCount; layerIndex++)
                    {
                        float weight = alphaRegion.Sample(localX, localZ, layerIndex);
                        if (weight <= 0f)
                        {
                            continue;
                        }

                        Color sample = layerSamplers[layerIndex].Sample(localX, localZ);
                        color += sample * weight;
                    }

                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }

            target.SetPixels(pixels);
        }

        private static void Fill(Color[] pixels, Color color)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
        }

        private readonly struct LayerSampler
        {
            private readonly Color fallback;
            private readonly Color32[] pixels;
            private readonly int width;
            private readonly int height;
            private readonly Vector2 tileSize;
            private readonly Vector2 tileOffset;

            public LayerSampler(TerrainLayer layer, Color fallback)
            {
                this.fallback = fallback;
                pixels = null;
                width = 0;
                height = 0;
                tileSize = Vector2.one;
                tileOffset = Vector2.zero;

                Texture2D texture = layer != null ? layer.diffuseTexture : null;
                if (texture == null)
                {
                    return;
                }

                tileSize = layer.tileSize;
                tileOffset = layer.tileOffset;
                width = texture.width;
                height = texture.height;

                try
                {
                    pixels = texture.GetPixels32();
                }
                catch (UnityException)
                {
                    pixels = null;
                    width = 0;
                    height = 0;
                }
            }

            public Color Sample(float terrainLocalX, float terrainLocalZ)
            {
                if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
                {
                    return fallback;
                }

                if (tileSize.x <= 0f || tileSize.y <= 0f)
                {
                    return SampleUv(0f, 0f);
                }

                float u = Mathf.Repeat((terrainLocalX + tileOffset.x) / tileSize.x, 1f);
                float v = Mathf.Repeat((terrainLocalZ + tileOffset.y) / tileSize.y, 1f);
                return SampleUv(u, v);
            }

            private Color SampleUv(float u, float v)
            {
                float px = Mathf.Clamp01(u) * (width - 1);
                float py = Mathf.Clamp01(v) * (height - 1);
                int x0 = Mathf.FloorToInt(px);
                int y0 = Mathf.FloorToInt(py);
                int x1 = Mathf.Min(x0 + 1, width - 1);
                int y1 = Mathf.Min(y0 + 1, height - 1);
                float tx = px - x0;
                float ty = py - y0;

                Color a = pixels[y0 * width + x0];
                Color b = pixels[y0 * width + x1];
                Color c = pixels[y1 * width + x0];
                Color d = pixels[y1 * width + x1];
                return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
            }
        }

        private readonly struct AlphaRegion
        {
            private readonly TerrainData terrainData;
            private readonly float[,,] data;
            private readonly int startX;
            private readonly int startZ;
            private readonly int width;
            private readonly int height;

            private AlphaRegion(TerrainData terrainData, float[,,] data, int startX, int startZ, int width, int height)
            {
                this.terrainData = terrainData;
                this.data = data;
                this.startX = startX;
                this.startZ = startZ;
                this.width = width;
                this.height = height;
            }

            public static AlphaRegion Read(TerrainData terrainData, Rect rect)
            {
                int alphaWidth = terrainData.alphamapWidth;
                int alphaHeight = terrainData.alphamapHeight;
                float x0 = terrainData.size.x > 0f ? Mathf.Clamp01(rect.xMin / terrainData.size.x) * (alphaWidth - 1) : 0f;
                float x1 = terrainData.size.x > 0f ? Mathf.Clamp01(rect.xMax / terrainData.size.x) * (alphaWidth - 1) : 0f;
                float z0 = terrainData.size.z > 0f ? Mathf.Clamp01(rect.yMin / terrainData.size.z) * (alphaHeight - 1) : 0f;
                float z1 = terrainData.size.z > 0f ? Mathf.Clamp01(rect.yMax / terrainData.size.z) * (alphaHeight - 1) : 0f;

                int startX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(x0, x1)), 0, alphaWidth - 1);
                int endX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(x0, x1)), 0, alphaWidth - 1);
                int startZ = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(z0, z1)), 0, alphaHeight - 1);
                int endZ = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(z0, z1)), 0, alphaHeight - 1);
                int width = Mathf.Max(1, endX - startX + 1);
                int height = Mathf.Max(1, endZ - startZ + 1);
                float[,,] data = terrainData.GetAlphamaps(startX, startZ, width, height);
                return new AlphaRegion(terrainData, data, startX, startZ, width, height);
            }

            public float Sample(float terrainLocalX, float terrainLocalZ, int layer)
            {
                if (data == null || layer < 0 || layer >= terrainData.alphamapLayers)
                {
                    return 0f;
                }

                float alphaX = terrainData.size.x > 0f
                    ? Mathf.Clamp01(terrainLocalX / terrainData.size.x) * (terrainData.alphamapWidth - 1)
                    : 0f;
                float alphaZ = terrainData.size.z > 0f
                    ? Mathf.Clamp01(terrainLocalZ / terrainData.size.z) * (terrainData.alphamapHeight - 1)
                    : 0f;

                float localX = Mathf.Clamp(alphaX - startX, 0f, width - 1);
                float localZ = Mathf.Clamp(alphaZ - startZ, 0f, height - 1);
                int x0 = Mathf.FloorToInt(localX);
                int z0 = Mathf.FloorToInt(localZ);
                int x1 = Mathf.Min(x0 + 1, width - 1);
                int z1 = Mathf.Min(z0 + 1, height - 1);
                float tx = localX - x0;
                float tz = localZ - z0;

                float a = data[z0, x0, layer];
                float b = data[z0, x1, layer];
                float c = data[z1, x0, layer];
                float d = data[z1, x1, layer];
                return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
            }
        }
    }
}
