using UnityEngine;

namespace DecalMini
{
    [AddComponentMenu("ShaderMini/Decal Bullet Impact")]
    public sealed class DecalBulletImpactComponent : MonoBehaviour
    {
        public enum TriggerMode
        {
            Collision,
            Raycast,
            CollisionAndRaycast,
            Scripting,
        }

        [Header("Impact")]
        [SerializeField] private DecalImpactModule impactModule = new();
        [SerializeField] private TriggerMode triggerMode = TriggerMode.CollisionAndRaycast;
        [SerializeField] private LayerMask hitLayers = -1;

        [Header("Bullet Lifecycle")]
        [SerializeField] private bool emitOnce = true;
        [SerializeField] private bool destroyAfterImpact = true;

        [Header("Raycast Mode")]
        [SerializeField] private float raycastPadding = 0.05f;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private bool _hasEmitted;

        public void Configure(
            DecalImpactModule module,
            TriggerMode mode,
            LayerMask layers,
            bool shouldEmitOnce,
            bool shouldDestroyAfterImpact,
            float padding
        )
        {
            impactModule = module ?? new DecalImpactModule();
            triggerMode = mode;
            hitLayers = layers;
            emitOnce = shouldEmitOnce;
            destroyAfterImpact = shouldDestroyAfterImpact;
            raycastPadding = Mathf.Max(0f, padding);
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _hasLastPosition = true;
            _hasEmitted = false;
        }

        private void LateUpdate()
        {
            if (!UsesRaycastMode())
                return;

            Vector3 currentPosition = transform.position;
            if (_hasLastPosition)
                TryEmitAlongSegment(_lastPosition, currentPosition);

            _lastPosition = currentPosition;
            _hasLastPosition = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!UsesCollisionMode() || collision.contactCount == 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            if (!IsLayerAllowed(contact.otherCollider.gameObject.layer))
                return;

            Vector3 incomingDirection = collision.relativeVelocity.sqrMagnitude > 0.0001f
                ? collision.relativeVelocity.normalized
                : transform.forward;

            TryEmit(new DecalImpactHit(contact, incomingDirection));
        }

        public bool Emit(RaycastHit hit, Vector3 incomingDirection)
        {
            return TryEmit(new DecalImpactHit(hit, incomingDirection));
        }

        public bool Emit(Vector3 point, Vector3 normal, Vector3 incomingDirection, Collider collider)
        {
            return TryEmit(new DecalImpactHit(point, normal, incomingDirection, collider));
        }

        public bool FireHitscan(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            if (direction.sqrMagnitude <= 0.0001f || maxDistance <= 0f)
                return false;

            Vector3 rayDirection = direction.normalized;
            if (!Physics.Raycast(origin, rayDirection, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
                return false;

            return TryEmit(new DecalImpactHit(hit, rayDirection));
        }

        private bool TryEmitAlongSegment(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return false;

            Vector3 direction = delta / distance;
            float queryDistance = distance + Mathf.Max(0f, raycastPadding);
            if (!Physics.Raycast(from, direction, out var hit, queryDistance, hitLayers, QueryTriggerInteraction.Ignore))
                return false;

            return TryEmit(new DecalImpactHit(hit, direction));
        }

        private bool TryEmit(DecalImpactHit hit)
        {
            if (impactModule == null)
                return false;
            if (emitOnce && _hasEmitted)
                return false;

            bool emitted = impactModule.Emit(hit);
            if (!emitted)
                return false;

            _hasEmitted = true;
            if (destroyAfterImpact)
                Destroy(gameObject);

            return true;
        }

        private bool UsesCollisionMode() =>
            triggerMode == TriggerMode.Collision || triggerMode == TriggerMode.CollisionAndRaycast;

        private bool UsesRaycastMode() =>
            triggerMode == TriggerMode.Raycast || triggerMode == TriggerMode.CollisionAndRaycast;

        private bool IsLayerAllowed(int layer)
        {
            int mask = 1 << layer;
            return (hitLayers.value & mask) != 0;
        }
    }
}
