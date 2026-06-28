using UnityEngine;

namespace TerrainMeshCapture
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class TerrainMeshCaptureArea : MonoBehaviour
    {
        private const int MaxDecalSegments = 32;
        private const float GizmoSurfaceLift = 0.08f;

        [SerializeField] private TerrainMeshCaptureProfile profile;
        [SerializeField] private Color gizmoColor = new Color(0.1f, 0.75f, 1f, 0.22f);
        [SerializeField] private Color gizmoWireColor = new Color(0.05f, 0.95f, 1f, 0.9f);
        [SerializeField] private bool drawGizmo = true;

        private Mesh decalMesh;
        private int cachedTerrainId;
        private Rect cachedRect;
        private float cachedHeightOffset;
        private Vector3 cachedPosition;
        private Vector2 cachedProfileSize;

        public TerrainMeshCaptureProfile Profile => profile;

        private void OnDrawGizmos()
        {
            Terrain sourceTerrain = SourceTerrain;
            if (!drawGizmo || sourceTerrain == null || sourceTerrain.terrainData == null)
            {
                return;
            }

            DrawTerrainRectGizmo(sourceTerrain);
        }

        private void DrawTerrainRectGizmo(Terrain sourceTerrain)
        {
            TerrainData terrainData = sourceTerrain.terrainData;
            Vector3 centerLocal = sourceTerrain.transform.InverseTransformPoint(transform.position);
            Rect rawRect = TerrainMeshCaptureBaker.BuildRect(centerLocal, GizmoSize);
            Rect rect = TerrainMeshCaptureBaker.ResolveRect(rawRect, terrainData.size, GizmoBoundsMode, out bool inside);
            if (!inside)
            {
                rect = rawRect;
            }

            float heightOffset = GizmoHeightOffset;
            Color fill = inside ? gizmoColor : new Color(1f, 0.25f, 0.1f, 0.18f);
            Color wire = inside ? gizmoWireColor : new Color(1f, 0.1f, 0.05f, 0.9f);

            Gizmos.color = fill;
            Gizmos.DrawMesh(GetDecalMesh(sourceTerrain, terrainData, rect, heightOffset + GizmoSurfaceLift));
            Gizmos.color = wire;
            DrawTerrainPolyline(sourceTerrain, terrainData, rect.xMin, rect.yMin, rect.xMax, rect.yMin, heightOffset + GizmoSurfaceLift);
            DrawTerrainPolyline(sourceTerrain, terrainData, rect.xMax, rect.yMin, rect.xMax, rect.yMax, heightOffset + GizmoSurfaceLift);
            DrawTerrainPolyline(sourceTerrain, terrainData, rect.xMax, rect.yMax, rect.xMin, rect.yMax, heightOffset + GizmoSurfaceLift);
            DrawTerrainPolyline(sourceTerrain, terrainData, rect.xMin, rect.yMax, rect.xMin, rect.yMin, heightOffset + GizmoSurfaceLift);

            DrawSplitGrid(sourceTerrain, terrainData, rect, inside, heightOffset + GizmoSurfaceLift);
        }

        private static Vector3 SampleWorld(Terrain sourceTerrain, TerrainData terrainData, float localX, float localZ, float heightOffset)
        {
            float normalizedX = terrainData.size.x > 0f ? Mathf.Clamp01(localX / terrainData.size.x) : 0f;
            float normalizedZ = terrainData.size.z > 0f ? Mathf.Clamp01(localZ / terrainData.size.z) : 0f;
            float height = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) + heightOffset;
            return sourceTerrain.transform.TransformPoint(new Vector3(localX, height, localZ));
        }

        private static int GetSegmentCount(float length)
        {
            return Mathf.Clamp(Mathf.CeilToInt(length / 4f), 2, MaxDecalSegments);
        }

        private static void DrawTerrainPolyline(
            Terrain sourceTerrain,
            TerrainData terrainData,
            float x0,
            float z0,
            float x1,
            float z1,
            float heightOffset)
        {
            int segments = GetSegmentCount(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0)));
            Vector3 previous = SampleWorld(sourceTerrain, terrainData, x0, z0, heightOffset);
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 current = SampleWorld(
                    sourceTerrain,
                    terrainData,
                    Mathf.Lerp(x0, x1, t),
                    Mathf.Lerp(z0, z1, t),
                    heightOffset);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        private void DrawSplitGrid(Terrain sourceTerrain, TerrainData terrainData, Rect rect, bool areaInsideTerrain, float heightOffset)
        {
            if (!areaInsideTerrain || profile == null || profile.BakeScope != TerrainCaptureBakeScope.SplitByBlockSize)
            {
                return;
            }

            Gizmos.color = new Color(gizmoWireColor.r, gizmoWireColor.g, gizmoWireColor.b, 0.38f);
            float blockSize = profile.SquareBlockSize;
            int columns = Mathf.Max(1, Mathf.RoundToInt(rect.width / blockSize));
            int rows = Mathf.Max(1, Mathf.RoundToInt(rect.height / blockSize));

            for (int column = 1; column < columns; column++)
            {
                float x = rect.xMin + blockSize * column;
                DrawTerrainPolyline(sourceTerrain, terrainData, x, rect.yMin, x, rect.yMax, heightOffset);
            }

            for (int row = 1; row < rows; row++)
            {
                float z = rect.yMin + blockSize * row;
                DrawTerrainPolyline(sourceTerrain, terrainData, rect.xMin, z, rect.xMax, z, heightOffset);
            }
        }

        private Terrain SourceTerrain => ResolveSourceTerrain();

        private Vector2 GizmoSize => profile != null ? profile.AreaSize : new Vector2(16f, 16f);
        private float GizmoHeightOffset => profile != null ? profile.HeightOffset : 0f;
        private TerrainCaptureBoundsMode GizmoBoundsMode => profile != null ? profile.BoundsMode : TerrainCaptureBoundsMode.RejectOutOfBounds;

        private Mesh GetDecalMesh(Terrain sourceTerrain, TerrainData terrainData, Rect rect, float heightOffset)
        {
            int terrainId = sourceTerrain.GetInstanceID();
            Vector2 profileSize = GizmoSize;
            if (decalMesh == null
                || cachedTerrainId != terrainId
                || cachedRect != rect
                || !Mathf.Approximately(cachedHeightOffset, heightOffset)
                || cachedPosition != transform.position
                || cachedProfileSize != profileSize)
            {
                RebuildDecalMesh(sourceTerrain, terrainData, rect, heightOffset);
                cachedTerrainId = terrainId;
                cachedRect = rect;
                cachedHeightOffset = heightOffset;
                cachedPosition = transform.position;
                cachedProfileSize = profileSize;
            }

            return decalMesh;
        }

        private void RebuildDecalMesh(Terrain sourceTerrain, TerrainData terrainData, Rect rect, float heightOffset)
        {
            if (decalMesh == null)
            {
                decalMesh = new Mesh
                {
                    name = "Terrain Mesh Capture Decal Gizmo",
                    hideFlags = HideFlags.HideAndDontSave
                };
                decalMesh.MarkDynamic();
            }

            int segmentsX = GetSegmentCount(rect.width);
            int segmentsZ = GetSegmentCount(rect.height);
            int vertexColumns = segmentsX + 1;
            int vertexRows = segmentsZ + 1;
            int vertexCount = vertexColumns * vertexRows;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var triangles = new int[segmentsX * segmentsZ * 6];

            int vertexIndex = 0;
            for (int z = 0; z < vertexRows; z++)
            {
                float v = segmentsZ > 0 ? (float)z / segmentsZ : 0f;
                float localZ = Mathf.Lerp(rect.yMin, rect.yMax, v);
                for (int x = 0; x < vertexColumns; x++)
                {
                    float u = segmentsX > 0 ? (float)x / segmentsX : 0f;
                    float localX = Mathf.Lerp(rect.xMin, rect.xMax, u);
                    vertices[vertexIndex] = SampleWorld(sourceTerrain, terrainData, localX, localZ, heightOffset);
                    normals[vertexIndex] = Vector3.up;
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < segmentsZ; z++)
            {
                int row = z * vertexColumns;
                int nextRow = (z + 1) * vertexColumns;
                for (int x = 0; x < segmentsX; x++)
                {
                    int a = row + x;
                    int b = row + x + 1;
                    int c = nextRow + x;
                    int d = nextRow + x + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            decalMesh.Clear();
            decalMesh.vertices = vertices;
            decalMesh.normals = normals;
            decalMesh.triangles = triangles;
            decalMesh.RecalculateBounds();
        }

        private void OnDisable()
        {
            if (decalMesh != null)
            {
                DestroyImmediate(decalMesh);
                decalMesh = null;
            }
        }

        private Terrain ResolveSourceTerrain()
        {
            Terrain[] terrains = Terrain.activeTerrains;
            Vector3 worldPosition = transform.position;
            Terrain fallback = null;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = terrain;
                }

                Vector3 local = terrain.transform.InverseTransformPoint(worldPosition);
                Vector3 size = terrain.terrainData.size;
                if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
                {
                    return terrain;
                }
            }

            return Terrain.activeTerrain != null ? Terrain.activeTerrain : fallback;
        }
    }
}
