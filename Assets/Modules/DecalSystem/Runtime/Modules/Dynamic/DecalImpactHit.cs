using UnityEngine;

namespace DecalMini
{
    public readonly struct DecalImpactHit
    {
        public readonly Vector3 point;
        public readonly Vector3 normal;
        public readonly Vector3 incomingDirection;
        public readonly Collider collider;
        public readonly int layer;

        public DecalImpactHit(
            Vector3 point,
            Vector3 normal,
            Vector3 incomingDirection,
            Collider collider
        )
        {
            this.point = point;
            this.normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            this.incomingDirection = incomingDirection.sqrMagnitude > 0.0001f
                ? incomingDirection.normalized
                : -this.normal;
            this.collider = collider;
            layer = collider != null ? collider.gameObject.layer : 0;
        }

        public DecalImpactHit(RaycastHit hit, Vector3 incomingDirection)
            : this(hit.point, hit.normal, incomingDirection, hit.collider)
        {
        }

        public DecalImpactHit(ContactPoint contact, Vector3 incomingDirection)
            : this(contact.point, contact.normal, incomingDirection, contact.otherCollider)
        {
        }
    }
}
