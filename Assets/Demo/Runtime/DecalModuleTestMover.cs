using UnityEngine;

namespace ModularDemo.Runtime
{
    public sealed class DecalModuleTestMover : MonoBehaviour
    {
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField] private float distance = 4f;
        [SerializeField] private float speed = 1f;
        [SerializeField] private bool rotateAlongPath = true;

        private Vector3 _origin;

        private void OnEnable()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.forward;
            float offset = Mathf.PingPong(Time.time * speed, distance) - distance * 0.5f;
            Vector3 worldAxis = transform.parent != null ? transform.parent.TransformDirection(axis) : axis;
            transform.position = _origin + worldAxis * offset;

            if (rotateAlongPath && worldAxis.sqrMagnitude > 0.0001f)
            {
                float phase = Mathf.PingPong(Time.time * speed, distance);
                Vector3 forward = phase < distance * 0.5f ? worldAxis : -worldAxis;
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
    }
}
