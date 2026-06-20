using UnityEngine;

namespace DecalMini
{
    [System.Serializable]
    public class DecalImpactModule
    {
        public const int DefaultImpactSortingOrder = 12000;

        [Header("Visual")]
        [HideInInspector]
        public Texture2D texture;
        public Color color = Color.white;
        public Vector2 sizeRange = new(0.08f, 0.16f);
        public float projectionDepth = 0.35f;
        [Range(0f, 1f)] public float softFade = 0.25f;
        public int sortingOrder = DefaultImpactSortingOrder;

        [Header("Lifetime")]
        public float lifetime = 12f;
        public float normalOffset = 0.01f;

        [Header("Filtering")]
        public LayerMask allowedLayers = -1;

        [Header("Orientation")]
        public bool alignToIncomingDirection = true;
        public bool randomRoll = true;

        [Header("Particle")]
        public ParticleSystem impactParticlePrefab;
        public float particleLifetime = 2f;

        public bool Emit(DecalImpactHit hit)
        {
            if (texture == null || !IsLayerAllowed(hit.layer))
                return false;

            float width = Mathf.Max(0.001f, Random.Range(sizeRange.x, sizeRange.y));
            float depth = Mathf.Max(0.001f, projectionDepth);
            Vector3 position = hit.point + hit.normal * normalOffset;
            Quaternion rotation = CalculateRotation(hit.normal, hit.incomingDirection);
            Vector3 size = new(width, width, depth);

            DecalSystemMini.SpawnRuntimeDecal(
                position,
                rotation,
                size,
                texture,
                Mathf.Max(0.01f, lifetime),
                color,
                softFade,
                sortingOrder
            );

            if (impactParticlePrefab != null)
            {
                DecalParticlePoolMini.Play(
                    impactParticlePrefab,
                    position,
                    rotation,
                    Mathf.Max(0.01f, particleLifetime)
                );
            }

            return true;
        }

        private bool IsLayerAllowed(int layer)
        {
            int mask = 1 << layer;
            return (allowedLayers.value & mask) != 0;
        }

        private Quaternion CalculateRotation(Vector3 normal, Vector3 incomingDirection)
        {
            Vector3 tangent = alignToIncomingDirection
                ? Vector3.ProjectOnPlane(-incomingDirection, normal)
                : Vector3.zero;

            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(normal, Vector3.right);

            Quaternion rotation = Quaternion.LookRotation(-normal, tangent.normalized);
            if (randomRoll)
                rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), -normal) * rotation;

            return rotation;
        }
    }
}
