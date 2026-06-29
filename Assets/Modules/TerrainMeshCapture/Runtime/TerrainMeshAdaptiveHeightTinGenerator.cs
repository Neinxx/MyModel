using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainMeshCapture
{
    internal sealed class TerrainMeshAdaptiveHeightTinGenerator : ITerrainMeshGenerator
    {
        private const float AreaEpsilon = 0.000001f;

        public TerrainMeshGenerationMode Mode => TerrainMeshGenerationMode.AdaptiveHeightTin;

        public TerrainMeshBuildData Generate(
            TerrainMeshSampleContext sampler,
            int samplesX,
            int samplesZ,
            TerrainMeshCaptureSettings settings)
        {
            int vertexCount = samplesX * samplesZ;
            var vertices = new SimplifyVertex[vertexCount];
            for (int z = 0; z < samplesZ; z++)
            {
                for (int x = 0; x < samplesX; x++)
                {
                    int index = GetIndex(x, z, samplesX);
                    vertices[index] = new SimplifyVertex(sampler.SampleAtIndex(x, z, samplesX, samplesZ), x, z);
                }
            }

            var triangles = BuildUniformTriangles(samplesX, samplesZ);
            MarkLockedVertices(vertices, samplesX, samplesZ, settings.AdaptiveEdgeConstraints);
            ComputeCurvature(vertices, triangles);
            MarkFeatureVertices(vertices, settings.AdaptiveCurvatureThreshold);
            BuildQuadrics(vertices, triangles);
            Simplify(vertices, triangles, settings);
            return BuildMeshData(vertices, triangles, settings);
        }

        private static List<SimplifyTriangle> BuildUniformTriangles(int samplesX, int samplesZ)
        {
            var triangles = new List<SimplifyTriangle>((samplesX - 1) * (samplesZ - 1) * 2);
            for (int z = 0; z < samplesZ - 1; z++)
            {
                for (int x = 0; x < samplesX - 1; x++)
                {
                    int a = GetIndex(x, z, samplesX);
                    int b = GetIndex(x + 1, z, samplesX);
                    int c = GetIndex(x, z + 1, samplesX);
                    int d = GetIndex(x + 1, z + 1, samplesX);
                    triangles.Add(new SimplifyTriangle(a, c, b));
                    triangles.Add(new SimplifyTriangle(b, c, d));
                }
            }

            return triangles;
        }

        private static void MarkLockedVertices(
            SimplifyVertex[] vertices,
            int samplesX,
            int samplesZ,
            TerrainMeshAdaptiveEdgeConstraints edgeConstraints)
        {
            int lastX = samplesX - 1;
            int lastZ = samplesZ - 1;
            for (int i = 0; i < vertices.Length; i++)
            {
                bool border = vertices[i].X == 0 || vertices[i].X == lastX || vertices[i].Z == 0 || vertices[i].Z == lastZ;
                vertices[i].Locked = border;
            }

            if (edgeConstraints == null)
            {
                return;
            }

            LockHorizontal(vertices, samplesX, 0, edgeConstraints.BottomX, edgeConstraints.LockBottom);
            LockHorizontal(vertices, samplesX, lastZ, edgeConstraints.TopX, edgeConstraints.LockTop);
            LockVertical(vertices, samplesX, 0, edgeConstraints.LeftZ, edgeConstraints.LockLeft);
            LockVertical(vertices, samplesX, lastX, edgeConstraints.RightZ, edgeConstraints.LockRight);
        }

        private static void LockHorizontal(SimplifyVertex[] vertices, int samplesX, int z, int[] points, bool locked)
        {
            if (!locked || points == null)
            {
                return;
            }

            for (int i = 0; i < points.Length; i++)
            {
                int x = Mathf.Clamp(points[i], 0, samplesX - 1);
                vertices[GetIndex(x, z, samplesX)].Locked = true;
            }
        }

        private static void LockVertical(SimplifyVertex[] vertices, int samplesX, int x, int[] points, bool locked)
        {
            if (!locked || points == null)
            {
                return;
            }

            int samplesZ = vertices.Length / samplesX;
            for (int i = 0; i < points.Length; i++)
            {
                int z = Mathf.Clamp(points[i], 0, samplesZ - 1);
                vertices[GetIndex(x, z, samplesX)].Locked = true;
            }
        }

        private static void ComputeCurvature(SimplifyVertex[] vertices, List<SimplifyTriangle> triangles)
        {
            var laplace = new Vector3[vertices.Length];
            var mixedArea = new float[vertices.Length];
            for (int i = 0; i < triangles.Count; i++)
            {
                SimplifyTriangle triangle = triangles[i];
                Vector3 a = vertices[triangle.A].Point.Position;
                Vector3 b = vertices[triangle.B].Point.Position;
                Vector3 c = vertices[triangle.C].Point.Position;
                float area = Mathf.Abs(Vector3.Cross(b - a, c - a).magnitude) * 0.5f;
                if (area <= AreaEpsilon)
                {
                    continue;
                }

                mixedArea[triangle.A] += area / 3f;
                mixedArea[triangle.B] += area / 3f;
                mixedArea[triangle.C] += area / 3f;
                AddCotangentWeight(vertices, laplace, triangle.A, triangle.B, triangle.C);
                AddCotangentWeight(vertices, laplace, triangle.B, triangle.C, triangle.A);
                AddCotangentWeight(vertices, laplace, triangle.C, triangle.A, triangle.B);
            }

            float maxCurvature = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                float area = Mathf.Max(AreaEpsilon, mixedArea[i]);
                vertices[i].Curvature = laplace[i].magnitude / (2f * area);
                maxCurvature = Mathf.Max(maxCurvature, vertices[i].Curvature);
            }

            if (maxCurvature <= AreaEpsilon)
            {
                return;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].Curvature = Mathf.Clamp01(vertices[i].Curvature / maxCurvature);
            }
        }

        private static void AddCotangentWeight(SimplifyVertex[] vertices, Vector3[] laplace, int a, int b, int opposite)
        {
            Vector3 oppositePosition = vertices[opposite].Point.Position;
            Vector3 edgeA = vertices[a].Point.Position - oppositePosition;
            Vector3 edgeB = vertices[b].Point.Position - oppositePosition;
            float denominator = Vector3.Cross(edgeA, edgeB).magnitude;
            if (denominator <= AreaEpsilon)
            {
                return;
            }

            float cotangent = Vector3.Dot(edgeA, edgeB) / denominator;
            laplace[a] += cotangent * (vertices[a].Point.Position - vertices[b].Point.Position);
            laplace[b] += cotangent * (vertices[b].Point.Position - vertices[a].Point.Position);
        }

        private static void MarkFeatureVertices(SimplifyVertex[] vertices, float threshold)
        {
            threshold = Mathf.Clamp01(threshold);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].Curvature >= threshold)
                {
                    vertices[i].Locked = true;
                }
            }
        }

        private static void BuildQuadrics(SimplifyVertex[] vertices, List<SimplifyTriangle> triangles)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                SimplifyTriangle triangle = triangles[i];
                if (!TryBuildPlane(vertices[triangle.A].Point.Position, vertices[triangle.B].Point.Position, vertices[triangle.C].Point.Position, out Quadric quadric))
                {
                    continue;
                }

                vertices[triangle.A].Quadric.Add(quadric);
                vertices[triangle.B].Quadric.Add(quadric);
                vertices[triangle.C].Quadric.Add(quadric);
            }
        }

        private static bool TryBuildPlane(Vector3 a, Vector3 b, Vector3 c, out Quadric quadric)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            float length = normal.magnitude;
            if (length <= AreaEpsilon)
            {
                quadric = default;
                return false;
            }

            normal /= length;
            float d = -Vector3.Dot(normal, a);
            quadric = Quadric.FromPlane(normal.x, normal.y, normal.z, d);
            return true;
        }

        private static void Simplify(SimplifyVertex[] vertices, List<SimplifyTriangle> triangles, TerrainMeshCaptureSettings settings)
        {
            int activeTriangles = CountActiveTriangles(triangles);
            int targetTriangles = Mathf.Clamp(settings.AdaptiveMaxTriangles, 2, activeTriangles);
            int guard = Mathf.Max(1, vertices.Length);
            while (activeTriangles > targetTriangles && guard-- > 0)
            {
                var candidates = BuildCollapseCandidates(vertices, triangles, settings.AdaptiveCurvaturePenalty);
                if (candidates.Count == 0)
                {
                    return;
                }

                candidates.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                int collapsedEdges = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (activeTriangles <= targetTriangles)
                    {
                        break;
                    }

                    CollapseCandidate candidate = candidates[i];
                    if (!CanCollapse(vertices, triangles, candidate.Keep, candidate.Remove))
                    {
                        continue;
                    }

                    int removedTriangles = Collapse(vertices, triangles, candidate.Keep, candidate.Remove);
                    activeTriangles -= removedTriangles;
                    collapsedEdges++;
                }

                if (collapsedEdges == 0)
                {
                    return;
                }
            }
        }

        private static List<CollapseCandidate> BuildCollapseCandidates(
            SimplifyVertex[] vertices,
            List<SimplifyTriangle> triangles,
            float curvaturePenalty)
        {
            var edges = new HashSet<EdgeKey>();
            for (int i = 0; i < triangles.Count; i++)
            {
                SimplifyTriangle triangle = triangles[i];
                if (!triangle.Active)
                {
                    continue;
                }

                edges.Add(new EdgeKey(triangle.A, triangle.B));
                edges.Add(new EdgeKey(triangle.B, triangle.C));
                edges.Add(new EdgeKey(triangle.C, triangle.A));
            }

            var candidates = new List<CollapseCandidate>(edges.Count);
            foreach (EdgeKey edge in edges)
            {
                AddCandidate(vertices, candidates, edge.A, edge.B, curvaturePenalty);
                AddCandidate(vertices, candidates, edge.B, edge.A, curvaturePenalty);
            }

            return candidates;
        }

        private static void AddCandidate(
            SimplifyVertex[] vertices,
            List<CollapseCandidate> candidates,
            int keep,
            int remove,
            float curvaturePenalty)
        {
            if (!vertices[keep].Active || !vertices[remove].Active || vertices[remove].Locked)
            {
                return;
            }

            if (vertices[keep].Locked && vertices[remove].Locked)
            {
                return;
            }

            Quadric quadric = vertices[keep].Quadric;
            quadric.Add(vertices[remove].Quadric);
            Vector3 target = vertices[keep].Point.Position;
            float cost = quadric.Evaluate(target);
            float curvature = Mathf.Max(vertices[keep].Curvature, vertices[remove].Curvature);
            cost *= 1f + Mathf.Max(0f, curvaturePenalty) * curvature;
            cost += CalculateSquaredDistanceXZ(vertices[keep].Point.Position, vertices[remove].Point.Position) * 0.0001f;
            candidates.Add(new CollapseCandidate(keep, remove, cost));
        }

        private static bool CanCollapse(SimplifyVertex[] vertices, List<SimplifyTriangle> triangles, int keep, int remove)
        {
            if (!vertices[keep].Active || !vertices[remove].Active || vertices[remove].Locked)
            {
                return false;
            }

            for (int i = 0; i < triangles.Count; i++)
            {
                SimplifyTriangle triangle = triangles[i];
                if (!triangle.Active || !triangle.Contains(remove))
                {
                    continue;
                }

                int a = triangle.A == remove ? keep : triangle.A;
                int b = triangle.B == remove ? keep : triangle.B;
                int c = triangle.C == remove ? keep : triangle.C;
                if (a == b || b == c || c == a)
                {
                    continue;
                }

                float oldArea = SignedAreaXZ(
                    vertices[triangle.A].Point.Position,
                    vertices[triangle.B].Point.Position,
                    vertices[triangle.C].Point.Position);
                float newArea = SignedAreaXZ(vertices[a].Point.Position, vertices[b].Point.Position, vertices[c].Point.Position);
                if (Mathf.Abs(newArea) <= AreaEpsilon || oldArea * newArea < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static int Collapse(SimplifyVertex[] vertices, List<SimplifyTriangle> triangles, int keep, int remove)
        {
            vertices[keep].Quadric.Add(vertices[remove].Quadric);
            vertices[keep].Curvature = Mathf.Max(vertices[keep].Curvature, vertices[remove].Curvature);
            vertices[remove].Active = false;
            int removedTriangles = 0;

            for (int i = 0; i < triangles.Count; i++)
            {
                SimplifyTriangle triangle = triangles[i];
                if (!triangle.Active || !triangle.Contains(remove))
                {
                    continue;
                }

                triangle.Replace(remove, keep);
                if (triangle.IsDegenerate)
                {
                    triangle.Active = false;
                    removedTriangles++;
                }

                triangles[i] = triangle;
            }

            return removedTriangles;
        }

        private static TerrainMeshBuildData BuildMeshData(
            SimplifyVertex[] vertices,
            List<SimplifyTriangle> triangles,
            TerrainMeshCaptureSettings settings)
        {
            int activeTriangles = CountActiveTriangles(triangles);
            var meshData = new TerrainMeshBuildData(vertices.Length, activeTriangles * 3, settings);
            var remap = new int[vertices.Length];
            Array.Fill(remap, -1);

            for (int i = 0; i < triangles.Count; i++)
            {
                SimplifyTriangle triangle = triangles[i];
                if (!triangle.Active)
                {
                    continue;
                }

                int a = GetOrCreateVertex(meshData, vertices, remap, triangle.A);
                int b = GetOrCreateVertex(meshData, vertices, remap, triangle.B);
                int c = GetOrCreateVertex(meshData, vertices, remap, triangle.C);
                meshData.AddTriangle(a, b, c);
            }

            if (settings.GenerateSkirts && settings.SkirtDepth > 0f)
            {
                AddSkirts(meshData, settings.SkirtDepth);
            }

            return meshData;
        }

        private static int GetOrCreateVertex(
            TerrainMeshBuildData meshData,
            SimplifyVertex[] vertices,
            int[] remap,
            int sourceIndex)
        {
            if (remap[sourceIndex] >= 0)
            {
                return remap[sourceIndex];
            }

            int index = meshData.AddPoint(vertices[sourceIndex].Point);
            remap[sourceIndex] = index;
            return index;
        }

        private static void AddSkirts(TerrainMeshBuildData meshData, float skirtDepth)
        {
            var edgeCounts = new Dictionary<EdgeKey, int>();
            for (int i = 0; i < meshData.Triangles.Count; i += 3)
            {
                AddEdgeUse(edgeCounts, meshData.Triangles[i], meshData.Triangles[i + 1]);
                AddEdgeUse(edgeCounts, meshData.Triangles[i + 1], meshData.Triangles[i + 2]);
                AddEdgeUse(edgeCounts, meshData.Triangles[i + 2], meshData.Triangles[i]);
            }

            foreach (KeyValuePair<EdgeKey, int> edge in edgeCounts)
            {
                if (edge.Value != 1)
                {
                    continue;
                }

                int a = edge.Key.A;
                int b = edge.Key.B;
                TerrainMeshSamplePoint pointA = BuildSkirtPoint(meshData, a, skirtDepth);
                TerrainMeshSamplePoint pointB = BuildSkirtPoint(meshData, b, skirtDepth);
                int skirtA = meshData.AddPoint(pointA);
                int skirtB = meshData.AddPoint(pointB);
                meshData.AddTriangleUnchecked(a, skirtA, b);
                meshData.AddTriangleUnchecked(b, skirtA, skirtB);
            }
        }

        private static TerrainMeshSamplePoint BuildSkirtPoint(TerrainMeshBuildData meshData, int index, float skirtDepth)
        {
            Vector3 position = meshData.Vertices[index];
            position.y -= skirtDepth;
            Vector3 normal = meshData.Normals != null ? meshData.Normals[index] : Vector3.up;
            Vector2 uv = meshData.Uv[index];
            Vector2 uv2 = meshData.Uv2 != null ? meshData.Uv2[index] : uv;
            return new TerrainMeshSamplePoint(position, normal, uv, uv2);
        }

        private static void AddEdgeUse(Dictionary<EdgeKey, int> edgeCounts, int a, int b)
        {
            var key = new EdgeKey(a, b);
            edgeCounts.TryGetValue(key, out int count);
            edgeCounts[key] = count + 1;
        }

        private static int CountActiveTriangles(List<SimplifyTriangle> triangles)
        {
            int count = 0;
            for (int i = 0; i < triangles.Count; i++)
            {
                if (triangles[i].Active)
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetIndex(int x, int z, int samplesX)
        {
            return z * samplesX + x;
        }

        private static float CalculateSquaredDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float SignedAreaXZ(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        }

        private struct SimplifyVertex
        {
            public SimplifyVertex(TerrainMeshSamplePoint point, int x, int z)
            {
                Point = point;
                X = x;
                Z = z;
                Active = true;
                Locked = false;
                Curvature = 0f;
                Quadric = default;
            }

            public TerrainMeshSamplePoint Point;
            public int X;
            public int Z;
            public bool Active;
            public bool Locked;
            public float Curvature;
            public Quadric Quadric;
        }

        private struct SimplifyTriangle
        {
            public SimplifyTriangle(int a, int b, int c)
            {
                A = a;
                B = b;
                C = c;
                Active = true;
            }

            public int A;
            public int B;
            public int C;
            public bool Active;
            public bool IsDegenerate => A == B || B == C || C == A;

            public bool Contains(int vertex)
            {
                return A == vertex || B == vertex || C == vertex;
            }

            public void Replace(int from, int to)
            {
                if (A == from)
                {
                    A = to;
                }

                if (B == from)
                {
                    B = to;
                }

                if (C == from)
                {
                    C = to;
                }
            }
        }

        private readonly struct CollapseCandidate
        {
            public CollapseCandidate(int keep, int remove, float cost)
            {
                Keep = keep;
                Remove = remove;
                Cost = cost;
            }

            public int Keep { get; }
            public int Remove { get; }
            public float Cost { get; }
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

        private struct Quadric
        {
            private float aa;
            private float ab;
            private float ac;
            private float ad;
            private float bb;
            private float bc;
            private float bd;
            private float cc;
            private float cd;
            private float dd;

            public static Quadric FromPlane(float a, float b, float c, float d)
            {
                return new Quadric
                {
                    aa = a * a,
                    ab = a * b,
                    ac = a * c,
                    ad = a * d,
                    bb = b * b,
                    bc = b * c,
                    bd = b * d,
                    cc = c * c,
                    cd = c * d,
                    dd = d * d
                };
            }

            public void Add(Quadric other)
            {
                aa += other.aa;
                ab += other.ab;
                ac += other.ac;
                ad += other.ad;
                bb += other.bb;
                bc += other.bc;
                bd += other.bd;
                cc += other.cc;
                cd += other.cd;
                dd += other.dd;
            }

            public float Evaluate(Vector3 position)
            {
                float x = position.x;
                float y = position.y;
                float z = position.z;
                return aa * x * x
                    + 2f * ab * x * y
                    + 2f * ac * x * z
                    + 2f * ad * x
                    + bb * y * y
                    + 2f * bc * y * z
                    + 2f * bd * y
                    + cc * z * z
                    + 2f * cd * z
                    + dd;
            }
        }
    }
}
