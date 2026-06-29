using System;
using System.Collections.Generic;
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
            return TerrainMeshGeneratorRegistry.Get(settings.MeshGenerationMode)
                .Generate(sampler, samplesX, samplesZ, settings);
        }

        internal static TerrainMeshBuildData BuildUniformMeshData(
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

        internal static TerrainMeshBuildData BuildAdaptiveHeightTinMeshData(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings)
        {
            int uniformTriangleCount = (samplesX - 1) * (samplesZ - 1) * 2;
            AdaptiveEdgeConstraints edgeConstraints = BuildAdaptiveEdgeConstraints(
                sampler,
                samplesX,
                samplesZ,
                settings.AdaptiveMaxHeightError,
                settings.AdaptiveEdgeConstraints);
            int maxTriangles = GetSafeAdaptiveTriangleBudget(settings.AdaptiveMaxTriangles, edgeConstraints, uniformTriangleCount);
            int maxLeaves = Mathf.Max(1, maxTriangles / 4);
            var leaves = new List<AdaptiveLeaf>(Mathf.Min(maxLeaves, 4096));
            int leafBudgetUsed = 0;
            BuildAdaptiveLeaves(
                sampler,
                0,
                0,
                samplesX - 1,
                samplesZ - 1,
                settings.AdaptiveMaxHeightError,
                settings.AdaptiveMinCellSamples,
                maxLeaves,
                samplesX - 1,
                samplesZ - 1,
                edgeConstraints,
                leaves,
                ref leafBudgetUsed);

            var edgePoints = new HashSet<Vector2Int>();
            AddAdaptiveEdgePoints(edgePoints, leaves, edgeConstraints, samplesX, samplesZ);

            int vertexCapacity = Mathf.Min(samplesX * samplesZ, edgePoints.Count + leaves.Count);
            var meshData = new TerrainMeshBuildData(vertexCapacity, Mathf.Min(maxTriangles * 3, uniformTriangleCount * 3), settings);
            var vertexCache = new Dictionary<Vector2Int, int>(edgePoints.Count);
            for (int i = 0; i < leaves.Count; i++)
            {
                AddAdaptiveLeaf(meshData, sampler, leaves[i], edgePoints, vertexCache, samplesX, samplesZ);
            }

            OptimizeAdaptiveEdges(meshData, settings.AdaptiveFlipPasses);

            if (settings.GenerateSkirts && settings.SkirtDepth > 0f)
            {
                AddAdaptiveSkirts(meshData, sampler, vertexCache, edgeConstraints, samplesX, samplesZ, settings.SkirtDepth);
            }

            return meshData;
        }

        private static int GetSafeAdaptiveTriangleBudget(int requestedMaxTriangles, AdaptiveEdgeConstraints edgeConstraints, int uniformTriangleCount)
        {
            int boundaryTriangleBudget = Mathf.Max(4, edgeConstraints.BoundaryVertexCount * 2);
            int safeBudget = Mathf.Max(requestedMaxTriangles, boundaryTriangleBudget);
            return Mathf.Clamp(safeBudget, 2, uniformTriangleCount);
        }

        private static AdaptiveEdgeConstraints BuildAdaptiveEdgeConstraints(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            float maxHeightError,
            TerrainMeshAdaptiveEdgeConstraints plannedConstraints)
        {
            var constraints = new AdaptiveEdgeConstraints();
            int lastX = samplesX - 1;
            int lastZ = samplesZ - 1;
            SimplifyHorizontalEdge(sampler, 0, 0, lastX, maxHeightError, constraints.BottomX);
            SimplifyHorizontalEdge(sampler, lastZ, 0, lastX, maxHeightError, constraints.TopX);
            SimplifyVerticalEdge(sampler, 0, 0, lastZ, maxHeightError, constraints.LeftZ);
            SimplifyVerticalEdge(sampler, lastX, 0, lastZ, maxHeightError, constraints.RightZ);
            ApplyPlannedEdgeConstraints(constraints, plannedConstraints, lastX, lastZ);
            return constraints;
        }

        private static void ApplyPlannedEdgeConstraints(
            AdaptiveEdgeConstraints constraints,
            TerrainMeshAdaptiveEdgeConstraints plannedConstraints,
            int lastX,
            int lastZ)
        {
            if (plannedConstraints == null)
            {
                return;
            }

            ApplyPlannedEdge(constraints.BottomX, plannedConstraints.BottomX, plannedConstraints.LockBottom, 0, lastX);
            ApplyPlannedEdge(constraints.TopX, plannedConstraints.TopX, plannedConstraints.LockTop, 0, lastX);
            ApplyPlannedEdge(constraints.LeftZ, plannedConstraints.LeftZ, plannedConstraints.LockLeft, 0, lastZ);
            ApplyPlannedEdge(constraints.RightZ, plannedConstraints.RightZ, plannedConstraints.LockRight, 0, lastZ);
            constraints.BottomLocked = plannedConstraints.LockBottom;
            constraints.TopLocked = plannedConstraints.LockTop;
            constraints.LeftLocked = plannedConstraints.LockLeft;
            constraints.RightLocked = plannedConstraints.LockRight;
        }

        private static void ApplyPlannedEdge(HashSet<int> target, int[] source, bool locked, int min, int max)
        {
            if (source == null || source.Length == 0)
            {
                if (locked)
                {
                    target.Clear();
                    target.Add(min);
                    target.Add(max);
                }

                return;
            }

            if (locked)
            {
                target.Clear();
            }

            target.Add(min);
            target.Add(max);
            for (int i = 0; i < source.Length; i++)
            {
                target.Add(Mathf.Clamp(source[i], min, max));
            }
        }

        private static void SimplifyHorizontalEdge(
            TerrainMeshSampleContext sampler,
            int z,
            int x0,
            int x1,
            float maxHeightError,
            HashSet<int> points)
        {
            points.Add(x0);
            points.Add(x1);
            if (x1 - x0 <= 1)
            {
                return;
            }

            float h0 = sampler.SampleHeightAtIndex(x0, z);
            float h1 = sampler.SampleHeightAtIndex(x1, z);
            float maxError = 0f;
            int splitX = -1;
            for (int x = x0 + 1; x < x1; x++)
            {
                float t = (float)(x - x0) / (x1 - x0);
                float projected = Mathf.Lerp(h0, h1, t);
                float error = Mathf.Abs(sampler.SampleHeightAtIndex(x, z) - projected);
                if (error <= maxError)
                {
                    continue;
                }

                maxError = error;
                splitX = x;
            }

            if (splitX < 0 || maxError <= maxHeightError)
            {
                return;
            }

            SimplifyHorizontalEdge(sampler, z, x0, splitX, maxHeightError, points);
            SimplifyHorizontalEdge(sampler, z, splitX, x1, maxHeightError, points);
        }

        private static void SimplifyVerticalEdge(
            TerrainMeshSampleContext sampler,
            int x,
            int z0,
            int z1,
            float maxHeightError,
            HashSet<int> points)
        {
            points.Add(z0);
            points.Add(z1);
            if (z1 - z0 <= 1)
            {
                return;
            }

            float h0 = sampler.SampleHeightAtIndex(x, z0);
            float h1 = sampler.SampleHeightAtIndex(x, z1);
            float maxError = 0f;
            int splitZ = -1;
            for (int z = z0 + 1; z < z1; z++)
            {
                float t = (float)(z - z0) / (z1 - z0);
                float projected = Mathf.Lerp(h0, h1, t);
                float error = Mathf.Abs(sampler.SampleHeightAtIndex(x, z) - projected);
                if (error <= maxError)
                {
                    continue;
                }

                maxError = error;
                splitZ = z;
            }

            if (splitZ < 0 || maxError <= maxHeightError)
            {
                return;
            }

            SimplifyVerticalEdge(sampler, x, z0, splitZ, maxHeightError, points);
            SimplifyVerticalEdge(sampler, x, splitZ, z1, maxHeightError, points);
        }

        private static void BuildAdaptiveLeaves(
            TerrainMeshSampleContext sampler,
            int x0,
            int z0,
            int x1,
            int z1,
            float maxHeightError,
            int minCellSamples,
            int maxLeaves,
            int lastX,
            int lastZ,
            AdaptiveEdgeConstraints edgeConstraints,
            List<AdaptiveLeaf> leaves,
            ref int leafBudgetUsed)
        {
            int width = x1 - x0;
            int height = z1 - z0;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            int midX = x0 + width / 2;
            int midZ = z0 + height / 2;
            bool canSplitX = width > minCellSamples && CanSplitXAtBoundary(z0, z1, midX, lastZ, edgeConstraints);
            bool canSplitZ = height > minCellSamples && CanSplitZAtBoundary(x0, x1, midZ, lastX, edgeConstraints);
            bool budgetAvailable = leafBudgetUsed + 4 < maxLeaves;
            float leafError = CalculateLeafError(sampler, x0, z0, x1, z1);
            if (!budgetAvailable || (!canSplitX && !canSplitZ) || leafError <= maxHeightError)
            {
                leaves.Add(new AdaptiveLeaf(x0, z0, x1, z1));
                leafBudgetUsed++;
                return;
            }

            if (canSplitX && canSplitZ)
            {
                RegisterBoundarySplitX(z0, z1, midX, lastZ, edgeConstraints);
                RegisterBoundarySplitZ(x0, x1, midZ, lastX, edgeConstraints);
                BuildAdaptiveLeaves(sampler, x0, z0, midX, midZ, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
                BuildAdaptiveLeaves(sampler, midX, z0, x1, midZ, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
                BuildAdaptiveLeaves(sampler, x0, midZ, midX, z1, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
                BuildAdaptiveLeaves(sampler, midX, midZ, x1, z1, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
                return;
            }

            if (canSplitX)
            {
                RegisterBoundarySplitX(z0, z1, midX, lastZ, edgeConstraints);
                BuildAdaptiveLeaves(sampler, x0, z0, midX, z1, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
                BuildAdaptiveLeaves(sampler, midX, z0, x1, z1, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
                return;
            }

            RegisterBoundarySplitZ(x0, x1, midZ, lastX, edgeConstraints);
            BuildAdaptiveLeaves(sampler, x0, z0, x1, midZ, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
            BuildAdaptiveLeaves(sampler, x0, midZ, x1, z1, maxHeightError, minCellSamples, maxLeaves, lastX, lastZ, edgeConstraints, leaves, ref leafBudgetUsed);
        }

        private static bool CanSplitXAtBoundary(int z0, int z1, int midX, int lastZ, AdaptiveEdgeConstraints edgeConstraints)
        {
            return (!edgeConstraints.BottomLocked || z0 != 0 || edgeConstraints.BottomX.Contains(midX))
                && (!edgeConstraints.TopLocked || z1 != lastZ || edgeConstraints.TopX.Contains(midX));
        }

        private static bool CanSplitZAtBoundary(int x0, int x1, int midZ, int lastX, AdaptiveEdgeConstraints edgeConstraints)
        {
            return (!edgeConstraints.LeftLocked || x0 != 0 || edgeConstraints.LeftZ.Contains(midZ))
                && (!edgeConstraints.RightLocked || x1 != lastX || edgeConstraints.RightZ.Contains(midZ));
        }

        private static void RegisterBoundarySplitX(int z0, int z1, int midX, int lastZ, AdaptiveEdgeConstraints edgeConstraints)
        {
            if (z0 == 0 && !edgeConstraints.BottomLocked)
            {
                edgeConstraints.BottomX.Add(midX);
            }

            if (z1 == lastZ && !edgeConstraints.TopLocked)
            {
                edgeConstraints.TopX.Add(midX);
            }
        }

        private static void RegisterBoundarySplitZ(int x0, int x1, int midZ, int lastX, AdaptiveEdgeConstraints edgeConstraints)
        {
            if (x0 == 0 && !edgeConstraints.LeftLocked)
            {
                edgeConstraints.LeftZ.Add(midZ);
            }

            if (x1 == lastX && !edgeConstraints.RightLocked)
            {
                edgeConstraints.RightZ.Add(midZ);
            }
        }

        private static float CalculateLeafError(TerrainMeshSampleContext sampler, int x0, int z0, int x1, int z1)
        {
            int width = x1 - x0;
            int height = z1 - z0;
            if (width <= 1 && height <= 1)
            {
                return 0f;
            }

            float h00 = sampler.SampleHeightAtIndex(x0, z0);
            float h10 = sampler.SampleHeightAtIndex(x1, z0);
            float h01 = sampler.SampleHeightAtIndex(x0, z1);
            float h11 = sampler.SampleHeightAtIndex(x1, z1);
            float maxError = 0f;
            int stride = GetAdaptiveErrorStride(width, height);

            for (int z = z0 + 1; z < z1; z += stride)
            {
                for (int x = x0 + 1; x < x1; x += stride)
                {
                    maxError = Mathf.Max(maxError, CalculatePointError(sampler, x0, z0, x1, z1, x, z, h00, h10, h01, h11));
                }
            }

            int centerX = (x0 + x1) / 2;
            int centerZ = (z0 + z1) / 2;
            return Mathf.Max(maxError, CalculatePointError(sampler, x0, z0, x1, z1, centerX, centerZ, h00, h10, h01, h11));
        }

        private static int GetAdaptiveErrorStride(int width, int height)
        {
            int longest = Mathf.Max(width, height);
            return longest <= 128 ? 1 : Mathf.Max(1, longest / 128);
        }

        private static float CalculatePointError(
            TerrainMeshSampleContext sampler,
            int x0,
            int z0,
            int x1,
            int z1,
            int x,
            int z,
            float h00,
            float h10,
            float h01,
            float h11)
        {
            float u = (float)(x - x0) / (x1 - x0);
            float v = (float)(z - z0) / (z1 - z0);
            float actual = sampler.SampleHeightAtIndex(x, z);
            float diagonalA = u + v <= 1f
                ? h00 + (h10 - h00) * u + (h01 - h00) * v
                : (1f - v) * h10 + (1f - u) * h01 + (u + v - 1f) * h11;
            float diagonalB = u <= v
                ? (1f - v) * h00 + u * h11 + (v - u) * h01
                : (1f - u) * h00 + v * h11 + (u - v) * h10;
            return Mathf.Min(Mathf.Abs(actual - diagonalA), Mathf.Abs(actual - diagonalB));
        }

        private static void AddAdaptiveEdgePoints(
            HashSet<Vector2Int> edgePoints,
            List<AdaptiveLeaf> leaves,
            AdaptiveEdgeConstraints edgeConstraints,
            int samplesX,
            int samplesZ)
        {
            for (int i = 0; i < leaves.Count; i++)
            {
                AdaptiveLeaf leaf = leaves[i];
                edgePoints.Add(new Vector2Int(leaf.X0, leaf.Z0));
                edgePoints.Add(new Vector2Int(leaf.X1, leaf.Z0));
                edgePoints.Add(new Vector2Int(leaf.X1, leaf.Z1));
                edgePoints.Add(new Vector2Int(leaf.X0, leaf.Z1));
            }

            int lastX = samplesX - 1;
            int lastZ = samplesZ - 1;
            foreach (int x in edgeConstraints.BottomX)
            {
                edgePoints.Add(new Vector2Int(x, 0));
            }

            foreach (int x in edgeConstraints.TopX)
            {
                edgePoints.Add(new Vector2Int(x, lastZ));
            }

            foreach (int z in edgeConstraints.LeftZ)
            {
                edgePoints.Add(new Vector2Int(0, z));
            }

            foreach (int z in edgeConstraints.RightZ)
            {
                edgePoints.Add(new Vector2Int(lastX, z));
            }
        }

        private static void AddAdaptiveLeaf(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            AdaptiveLeaf leaf,
            HashSet<Vector2Int> edgePoints,
            Dictionary<Vector2Int, int> vertexCache,
            int samplesX,
            int samplesZ)
        {
            var boundary = new List<int>();
            AddLeafEdge(boundary, meshData, sampler, edgePoints, vertexCache, leaf.X0, leaf.X1, 1, leaf.Z0, true, samplesX, samplesZ);
            AddLeafEdge(boundary, meshData, sampler, edgePoints, vertexCache, leaf.Z0 + 1, leaf.Z1, 1, leaf.X1, false, samplesX, samplesZ);
            AddLeafEdge(boundary, meshData, sampler, edgePoints, vertexCache, leaf.X1 - 1, leaf.X0, -1, leaf.Z1, true, samplesX, samplesZ);
            AddLeafEdge(boundary, meshData, sampler, edgePoints, vertexCache, leaf.Z1 - 1, leaf.Z0 + 1, -1, leaf.X0, false, samplesX, samplesZ);

            if (boundary.Count < 3)
            {
                return;
            }

            if (boundary.Count == 4)
            {
                AddBestDiagonalQuad(meshData, sampler, leaf, boundary);
                return;
            }

            TriangulateBoundaryPolygon(meshData, boundary);
        }

        private static void AddBestDiagonalQuad(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            AdaptiveLeaf leaf,
            List<int> boundary)
        {
            float errorA = CalculateQuadDiagonalError(sampler, leaf, false);
            float errorB = CalculateQuadDiagonalError(sampler, leaf, true);
            if (errorB + 0.0001f < errorA)
            {
                meshData.AddTriangle(boundary[0], boundary[3], boundary[2]);
                meshData.AddTriangle(boundary[0], boundary[2], boundary[1]);
                return;
            }

            meshData.AddTriangle(boundary[0], boundary[3], boundary[1]);
            meshData.AddTriangle(boundary[1], boundary[3], boundary[2]);
        }

        private static void TriangulateBoundaryPolygon(TerrainMeshBuildData meshData, List<int> boundary)
        {
            TriangulateBoundaryRange(meshData, boundary, 0, boundary.Count - 1);
        }

        private static void TriangulateBoundaryRange(TerrainMeshBuildData meshData, List<int> boundary, int first, int last)
        {
            int count = last - first + 1;
            if (count < 3)
            {
                return;
            }

            if (count == 3)
            {
                meshData.AddTriangle(boundary[first], boundary[first + 1], boundary[last]);
                return;
            }

            if (count == 4)
            {
                AddBestQualityQuad(meshData, boundary[first], boundary[first + 1], boundary[first + 2], boundary[last]);
                return;
            }

            int split = first + count / 2;
            meshData.AddTriangle(boundary[first], boundary[split], boundary[last]);
            TriangulateBoundaryRange(meshData, boundary, first, split);
            TriangulateBoundaryRange(meshData, boundary, split, last);
        }

        private static void AddBestQualityQuad(TerrainMeshBuildData meshData, int a, int b, int c, int d)
        {
            float qualityA = Mathf.Min(
                CalculateTriangleQuality(meshData.Vertices[a], meshData.Vertices[d], meshData.Vertices[b]),
                CalculateTriangleQuality(meshData.Vertices[b], meshData.Vertices[d], meshData.Vertices[c]));
            float qualityB = Mathf.Min(
                CalculateTriangleQuality(meshData.Vertices[a], meshData.Vertices[d], meshData.Vertices[c]),
                CalculateTriangleQuality(meshData.Vertices[a], meshData.Vertices[c], meshData.Vertices[b]));
            if (qualityB > qualityA)
            {
                meshData.AddTriangle(a, d, c);
                meshData.AddTriangle(a, c, b);
                return;
            }

            meshData.AddTriangle(a, d, b);
            meshData.AddTriangle(b, d, c);
        }

        private static float CalculateQuadDiagonalError(TerrainMeshSampleContext sampler, AdaptiveLeaf leaf, bool diagonal02)
        {
            float h00 = sampler.SampleHeightAtIndex(leaf.X0, leaf.Z0);
            float h10 = sampler.SampleHeightAtIndex(leaf.X1, leaf.Z0);
            float h01 = sampler.SampleHeightAtIndex(leaf.X0, leaf.Z1);
            float h11 = sampler.SampleHeightAtIndex(leaf.X1, leaf.Z1);
            int width = leaf.X1 - leaf.X0;
            int height = leaf.Z1 - leaf.Z0;
            int stride = GetAdaptiveErrorStride(width, height);
            float maxError = 0f;

            for (int z = leaf.Z0 + 1; z < leaf.Z1; z += stride)
            {
                for (int x = leaf.X0 + 1; x < leaf.X1; x += stride)
                {
                    maxError = Mathf.Max(maxError, CalculateQuadDiagonalPointError(sampler, leaf, x, z, h00, h10, h01, h11, diagonal02));
                }
            }

            int centerX = (leaf.X0 + leaf.X1) / 2;
            int centerZ = (leaf.Z0 + leaf.Z1) / 2;
            return Mathf.Max(maxError, CalculateQuadDiagonalPointError(sampler, leaf, centerX, centerZ, h00, h10, h01, h11, diagonal02));
        }

        private static float CalculateQuadDiagonalPointError(
            TerrainMeshSampleContext sampler,
            AdaptiveLeaf leaf,
            int x,
            int z,
            float h00,
            float h10,
            float h01,
            float h11,
            bool diagonal02)
        {
            float u = leaf.X1 > leaf.X0 ? (float)(x - leaf.X0) / (leaf.X1 - leaf.X0) : 0f;
            float v = leaf.Z1 > leaf.Z0 ? (float)(z - leaf.Z0) / (leaf.Z1 - leaf.Z0) : 0f;
            float actual = sampler.SampleHeightAtIndex(x, z);
            float projected = diagonal02
                ? (u <= v
                    ? (1f - v) * h00 + u * h11 + (v - u) * h01
                    : (1f - u) * h00 + v * h11 + (u - v) * h10)
                : (u + v <= 1f
                    ? h00 + (h10 - h00) * u + (h01 - h00) * v
                    : (1f - v) * h10 + (1f - u) * h01 + (u + v - 1f) * h11);
            return Mathf.Abs(actual - projected);
        }

        private static void OptimizeAdaptiveEdges(TerrainMeshBuildData meshData, int passCount)
        {
            if (passCount <= 0)
            {
                return;
            }

            for (int pass = 0; pass < passCount; pass++)
            {
                if (!TryFlipAdaptiveEdges(meshData))
                {
                    return;
                }
            }
        }

        private static bool TryFlipAdaptiveEdges(TerrainMeshBuildData meshData)
        {
            var edges = new Dictionary<EdgeKey, EdgeUse>(meshData.Triangles.Count);
            for (int triangle = 0; triangle < meshData.Triangles.Count; triangle += 3)
            {
                RegisterEdge(edges, meshData.Triangles[triangle], meshData.Triangles[triangle + 1], meshData.Triangles[triangle + 2], triangle);
                RegisterEdge(edges, meshData.Triangles[triangle + 1], meshData.Triangles[triangle + 2], meshData.Triangles[triangle], triangle);
                RegisterEdge(edges, meshData.Triangles[triangle + 2], meshData.Triangles[triangle], meshData.Triangles[triangle + 1], triangle);
            }

            foreach (KeyValuePair<EdgeKey, EdgeUse> pair in edges)
            {
                EdgeUse edge = pair.Value;
                if (!edge.HasPair || edge.TriangleA < 0 || edge.TriangleB < 0)
                {
                    continue;
                }

                int a = pair.Key.A;
                int b = pair.Key.B;
                int c = edge.OppositeA;
                int d = edge.OppositeB;
                if (c == d || a == c || a == d || b == c || b == d)
                {
                    continue;
                }

                if (!CanFlipEdge(meshData.Vertices, a, b, c, d))
                {
                    continue;
                }

                float currentQuality = Mathf.Min(
                    CalculateTriangleQuality(meshData.Vertices[a], meshData.Vertices[c], meshData.Vertices[b]),
                    CalculateTriangleQuality(meshData.Vertices[a], meshData.Vertices[b], meshData.Vertices[d]));
                float flippedQuality = Mathf.Min(
                    CalculateTriangleQuality(meshData.Vertices[c], meshData.Vertices[d], meshData.Vertices[b]),
                    CalculateTriangleQuality(meshData.Vertices[d], meshData.Vertices[c], meshData.Vertices[a]));
                if (flippedQuality <= currentQuality + 0.0001f)
                {
                    continue;
                }

                SetTriangle(meshData, edge.TriangleA, c, d, b);
                SetTriangle(meshData, edge.TriangleB, d, c, a);
                return true;
            }

            return false;
        }

        private static void RegisterEdge(Dictionary<EdgeKey, EdgeUse> edges, int a, int b, int opposite, int triangle)
        {
            var key = new EdgeKey(a, b);
            if (edges.TryGetValue(key, out EdgeUse edge))
            {
                edge.TriangleB = triangle;
                edge.OppositeB = opposite;
                edge.HasPair = true;
                edges[key] = edge;
                return;
            }

            edges.Add(key, new EdgeUse(triangle, opposite));
        }

        private static bool CanFlipEdge(List<Vector3> vertices, int a, int b, int c, int d)
        {
            Vector2 pointA = ToXZ(vertices[a]);
            Vector2 pointB = ToXZ(vertices[b]);
            Vector2 pointC = ToXZ(vertices[c]);
            Vector2 pointD = ToXZ(vertices[d]);
            if (Mathf.Abs(CalculateSignedArea(pointC, pointD, pointB)) <= 0.000001f
                || Mathf.Abs(CalculateSignedArea(pointD, pointC, pointA)) <= 0.000001f)
            {
                return false;
            }

            return SegmentsIntersect(pointA, pointB, pointC, pointD);
        }

        private static void SetTriangle(TerrainMeshBuildData meshData, int triangle, int a, int b, int c)
        {
            if (CalculateSignedArea(ToXZ(meshData.Vertices[a]), ToXZ(meshData.Vertices[b]), ToXZ(meshData.Vertices[c])) > 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }

            meshData.Triangles[triangle] = a;
            meshData.Triangles[triangle + 1] = b;
            meshData.Triangles[triangle + 2] = c;
        }

        private static float CalculateTriangleQuality(Vector3 a, Vector3 b, Vector3 c)
        {
            float ab = CalculateSquaredDistanceXZ(a, b);
            float bc = CalculateSquaredDistanceXZ(b, c);
            float ca = CalculateSquaredDistanceXZ(c, a);
            float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
            if (longest <= 0.000001f)
            {
                return 0f;
            }

            float area = Mathf.Abs(CalculateSignedArea(ToXZ(a), ToXZ(b), ToXZ(c)));
            return area / longest;
        }

        private static float CalculateSquaredDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static Vector2 ToXZ(Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }

        private static float CalculateSignedArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            return CalculateSignedArea(a, c, d) * CalculateSignedArea(b, c, d) < 0f
                && CalculateSignedArea(c, a, b) * CalculateSignedArea(d, a, b) < 0f;
        }

        private static void AddLeafEdge(
            List<int> boundary,
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            HashSet<Vector2Int> edgePoints,
            Dictionary<Vector2Int, int> vertexCache,
            int start,
            int end,
            int step,
            int fixedCoordinate,
            bool horizontal,
            int samplesX,
            int samplesZ)
        {
            for (int value = start; step > 0 ? value <= end : value >= end; value += step)
            {
                var key = horizontal
                    ? new Vector2Int(value, fixedCoordinate)
                    : new Vector2Int(fixedCoordinate, value);
                if (!edgePoints.Contains(key))
                {
                    continue;
                }

                boundary.Add(GetAdaptiveVertex(meshData, sampler, vertexCache, key, samplesX, samplesZ));
            }
        }

        private static int GetAdaptiveVertex(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            Dictionary<Vector2Int, int> vertexCache,
            Vector2Int key,
            int samplesX,
            int samplesZ)
        {
            if (vertexCache.TryGetValue(key, out int index))
            {
                return index;
            }

            index = meshData.AddSample(sampler, key.x, key.y, samplesX, samplesZ);
            vertexCache.Add(key, index);
            return index;
        }

        private static void AddAdaptiveSkirts(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            Dictionary<Vector2Int, int> vertexCache,
            AdaptiveEdgeConstraints edgeConstraints,
            int samplesX,
            int samplesZ,
            float skirtDepth)
        {
            int lastX = samplesX - 1;
            int lastZ = samplesZ - 1;
            AddAdaptiveSkirtEdge(meshData, sampler, vertexCache, edgeConstraints.BottomX, 0, true, false, samplesX, samplesZ, skirtDepth);
            AddAdaptiveSkirtEdge(meshData, sampler, vertexCache, edgeConstraints.RightZ, lastX, false, false, samplesX, samplesZ, skirtDepth);
            AddAdaptiveSkirtEdge(meshData, sampler, vertexCache, edgeConstraints.TopX, lastZ, true, true, samplesX, samplesZ, skirtDepth);
            AddAdaptiveSkirtEdge(meshData, sampler, vertexCache, edgeConstraints.LeftZ, 0, false, true, samplesX, samplesZ, skirtDepth);
        }

        private static void AddAdaptiveSkirtEdge(
            TerrainMeshBuildData meshData,
            TerrainMeshSampleContext sampler,
            Dictionary<Vector2Int, int> vertexCache,
            HashSet<int> edgeCoordinates,
            int fixedCoordinate,
            bool horizontal,
            bool reverse,
            int samplesX,
            int samplesZ,
            float skirtDepth)
        {
            if (edgeCoordinates.Count < 2)
            {
                return;
            }

            var coordinates = new List<int>(edgeCoordinates);
            coordinates.Sort();

            int previousSurface = -1;
            int previousSkirt = -1;
            int count = coordinates.Count;
            for (int i = 0; i < count; i++)
            {
                int value = reverse ? coordinates[count - i - 1] : coordinates[i];
                int x = horizontal ? value : fixedCoordinate;
                int z = horizontal ? fixedCoordinate : value;
                int surface = GetAdaptiveVertex(meshData, sampler, vertexCache, new Vector2Int(x, z), samplesX, samplesZ);
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

        private readonly struct AdaptiveLeaf
        {
            public AdaptiveLeaf(int x0, int z0, int x1, int z1)
            {
                X0 = x0;
                Z0 = z0;
                X1 = x1;
                Z1 = z1;
            }

            public int X0 { get; }
            public int Z0 { get; }
            public int X1 { get; }
            public int Z1 { get; }
        }

        private sealed class AdaptiveEdgeConstraints
        {
            public readonly HashSet<int> BottomX = new HashSet<int>();
            public readonly HashSet<int> TopX = new HashSet<int>();
            public readonly HashSet<int> LeftZ = new HashSet<int>();
            public readonly HashSet<int> RightZ = new HashSet<int>();
            public bool BottomLocked;
            public bool TopLocked;
            public bool LeftLocked;
            public bool RightLocked;

            public int BoundaryVertexCount => BottomX.Count + TopX.Count + LeftZ.Count + RightZ.Count;
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public int A { get; }
            public int B { get; }

            public bool Equals(EdgeKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }

        private struct EdgeUse
        {
            public EdgeUse(int triangle, int opposite)
            {
                TriangleA = triangle;
                OppositeA = opposite;
                TriangleB = -1;
                OppositeB = -1;
                HasPair = false;
            }

            public int TriangleA;
            public int OppositeA;
            public int TriangleB;
            public int OppositeB;
            public bool HasPair;
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

            Vector2Int textureSize = settings.ResolveTextureSize(terrainLocalRect);
            texture = new Texture2D(textureSize.x, textureSize.y, TextureFormat.RGBA32, settings.TextureMipMaps, false)
            {
                name = $"{terrain.name}_{settings.TextureBakeMode}_{textureSize.x}x{textureSize.y}"
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

            float terrainWidth = Mathf.Max(0f, terrainSize.x);
            float terrainDepth = Mathf.Max(0f, terrainSize.z);
            float width = Mathf.Min(Mathf.Max(0f, rect.width), terrainWidth);
            float depth = Mathf.Min(Mathf.Max(0f, rect.height), terrainDepth);
            if (width <= 0f || depth <= 0f)
            {
                inside = false;
                return rect;
            }

            float xMin = Mathf.Clamp(rect.xMin, 0f, terrainWidth - width);
            float zMin = Mathf.Clamp(rect.yMin, 0f, terrainDepth - depth);
            inside = true;
            return new Rect(xMin, zMin, width, depth);
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
