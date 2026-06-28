using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalMini
{
    public sealed class DecalTireTrackRenderer
    {
        private readonly string _name;
        private readonly Mesh _mesh;
        private readonly GameObject _root;
        private readonly MeshRenderer _meshRenderer;
        private readonly MeshFilter _meshFilter;
        private readonly MaterialPropertyBlock _propertyBlock;
        private readonly List<Vector3> _vertices = new(2048);
        private readonly List<Vector3> _normals = new(2048);
        private readonly List<Vector2> _uvs = new(2048);
        private readonly List<Color32> _colors = new(2048);
        private readonly List<int> _triangles = new(3072);

        private Material _runtimeMaterial;
        private Material _activeMaterial;

        public DecalTireTrackRenderer(string ownerName)
        {
            _name = string.IsNullOrEmpty(ownerName) ? "Decal Tire Track" : ownerName;
            _root = new GameObject($"{_name} Track Mesh");
            _root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _root.transform.localScale = Vector3.one;

            _meshFilter = _root.AddComponent<MeshFilter>();
            _meshRenderer = _root.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _mesh = new Mesh
            {
                name = $"{_name} Track Mesh"
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;

            _propertyBlock = new MaterialPropertyBlock();
        }

        public void SetMaterial(Material material, Texture2D texture, Color color, int sortingOrder)
        {
            Material selectedMaterial = material != null ? material : GetRuntimeMaterial();
            if (_activeMaterial != selectedMaterial)
            {
                _activeMaterial = selectedMaterial;
                _meshRenderer.sharedMaterial = _activeMaterial;
            }

            _meshRenderer.sortingOrder = sortingOrder;
            _propertyBlock.Clear();

            if (texture != null)
            {
                _propertyBlock.SetTexture("_BaseMap", texture);
                _propertyBlock.SetTexture("_MainTex", texture);
            }

            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        public void UpdateMesh(
            IReadOnlyList<DecalTireTrackModule.TrackPoint>[] wheelPoints,
            int wheelCount,
            Color color,
            float trackWidth,
            float textureRepeatMeters,
            float now,
            float lifetime,
            float fadeDuration
        )
        {
            ClearBuffers();

            float safeLifetime = Mathf.Max(0.01f, lifetime);
            float safeFade = Mathf.Clamp(fadeDuration, 0.01f, safeLifetime);

            for (int wheelIndex = 0; wheelIndex < wheelCount; wheelIndex++)
            {
                IReadOnlyList<DecalTireTrackModule.TrackPoint> points = wheelPoints[wheelIndex];
                if (points == null || points.Count < 2)
                    continue;

                int baseVertex = _vertices.Count;
                for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
                {
                    DecalTireTrackModule.TrackPoint point = points[pointIndex];
                    float halfWidth = trackWidth * 0.5f;
                    Vector3 offset = point.right * halfWidth;
                    byte alpha = CalculateAlpha(point.createdAt, now, safeLifetime, safeFade, color.a);
                    Color32 vertexColor = color;
                    vertexColor.a = alpha;

                    _vertices.Add(point.position - offset);
                    _vertices.Add(point.position + offset);
                    _normals.Add(point.normal);
                    _normals.Add(point.normal);
                    float v = point.distance / textureRepeatMeters;
                    _uvs.Add(new Vector2(0f, v));
                    _uvs.Add(new Vector2(1f, v));
                    _colors.Add(vertexColor);
                    _colors.Add(vertexColor);
                }

                for (int pointIndex = 1; pointIndex < points.Count; pointIndex++)
                {
                    int previousLeft = baseVertex + (pointIndex - 1) * 2;
                    int previousRight = previousLeft + 1;
                    int currentLeft = baseVertex + pointIndex * 2;
                    int currentRight = currentLeft + 1;

                    _triangles.Add(previousLeft);
                    _triangles.Add(currentLeft);
                    _triangles.Add(previousRight);
                    _triangles.Add(previousRight);
                    _triangles.Add(currentLeft);
                    _triangles.Add(currentRight);
                }
            }

            _mesh.Clear(false);
            if (_vertices.Count == 0)
                return;

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0, true);
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);

            if (_mesh != null)
                Object.Destroy(_mesh);

            if (_runtimeMaterial != null)
                Object.Destroy(_runtimeMaterial);
        }

        private Material GetRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
                return _runtimeMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Decal_TireTrack_Mini");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _runtimeMaterial = new Material(shader)
            {
                name = "Decal Tire Track Runtime Material",
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _runtimeMaterial;
        }

        private void ClearBuffers()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        private static byte CalculateAlpha(
            float createdAt,
            float now,
            float lifetime,
            float fadeDuration,
            float colorAlpha
        )
        {
            float age = Mathf.Max(0f, now - createdAt);
            float remaining = Mathf.Max(0f, lifetime - age);
            float fade = remaining >= fadeDuration ? 1f : remaining / Mathf.Max(0.01f, fadeDuration);
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(fade * colorAlpha) * 255f);
        }
    }
}
