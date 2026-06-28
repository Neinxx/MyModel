using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

[assembly: InternalsVisibleTo("InteractionSystem.Editor.Tests")]

namespace InteractionSystem.Runtime
{
    [DisallowMultipleComponent]
    public class ProximityInteractor : MonoBehaviour
    {
        [Header("Detection")]
        [FormerlySerializedAs("radius")]
        [Min(0f)]
        [SerializeField] private float _radius = 1.5f;
        [Tooltip("The position offset relative to the GameObject's local space.")]
        [FormerlySerializedAs("detectionOffset")]
        [SerializeField] private Vector3 _detectionOffset = Vector3.zero;
        [FormerlySerializedAs("targetLayers")]
        [SerializeField] private LayerMask _targetLayers = ~0;
        [FormerlySerializedAs("scanFrequency")]
        [Min(0.01f)]
        [SerializeField] private float _scanFrequency = 0.2f;
        [FormerlySerializedAs("hitCapacity")]
        [Min(1)]
        [SerializeField] private int _hitCapacity = 10;

        [Header("Runtime State")]
        [SerializeField] private bool _autoTrigger = true;
        [SerializeField] private bool _warnWhenHitBufferIsFull = true;
        private Collider[] _hits;
        private float _timer;
        private IInteractable _currentInteractable;
        private bool _isHitBufferFull;
        private bool _hasWarnedHitBufferFull;

        public float Radius => _radius;
        public Vector3 DetectionOffset => _detectionOffset;
        public LayerMask TargetLayers => _targetLayers;
        public float ScanFrequency => _scanFrequency;
        public int HitCapacity => _hitCapacity;
        public bool AutoTrigger => _autoTrigger;
        public IInteractable CurrentInteractable => _currentInteractable;
        public bool HasTarget => _currentInteractable != null;
        public bool IsHitBufferFull => _isHitBufferFull;
        public event System.Action<IInteractable> TargetChanged;

        [System.Obsolete("Use Radius for reading and SetRadius for writing.")]
        public float radius
        {
            get => _radius;
            set => _radius = Mathf.Max(0f, value);
        }

        [System.Obsolete("Use DetectionOffset for reading and SetDetectionOffset for writing.")]
        public Vector3 detectionOffset
        {
            get => _detectionOffset;
            set => _detectionOffset = value;
        }

        [System.Obsolete("Use TargetLayers for reading and SetTargetLayers for writing.")]
        public LayerMask targetLayers
        {
            get => _targetLayers;
            set => _targetLayers = value;
        }

        [System.Obsolete("Use ScanFrequency for reading and SetScanFrequency for writing.")]
        public float scanFrequency
        {
            get => _scanFrequency;
            set => _scanFrequency = Mathf.Max(0.01f, value);
        }

        public void SetRadius(float value)
        {
            _radius = Mathf.Max(0f, value);
        }

        public void SetDetectionOffset(Vector3 value)
        {
            _detectionOffset = value;
        }

        public void SetTargetLayers(LayerMask value)
        {
            _targetLayers = value;
        }

        public void SetScanFrequency(float value)
        {
            _scanFrequency = Mathf.Max(0.01f, value);
        }

        public void SetHitCapacity(int value)
        {
            _hitCapacity = Mathf.Max(1, value);
            EnsureHitBuffer();
        }

        public void SetAutoTrigger(bool value)
        {
            _autoTrigger = value;
        }

        private void Awake()
        {
            EnsureHitBuffer();
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(0f, _radius);
            _scanFrequency = Mathf.Max(0.01f, _scanFrequency);
            _hitCapacity = Mathf.Max(1, _hitCapacity);
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _scanFrequency)
                return;

            _timer = 0;

            Scan();
            if (_autoTrigger)
            {
                InteractCurrent();
            }
        }

        public IInteractable Scan()
        {
            EnsureHitBuffer();
            Vector3 center = transform.TransformPoint(_detectionOffset);
            int count = Physics.OverlapSphereNonAlloc(
                center, 
                _radius, 
                _hits, 
                _targetLayers, 
                QueryTriggerInteraction.Collide
            );

            _isHitBufferFull = count >= _hits.Length;
            if (_isHitBufferFull && !_hasWarnedHitBufferFull && _warnWhenHitBufferIsFull)
            {
                Debug.LogWarning(
                    $"[InteractionSystem] Hit buffer is full on {name}. Increase hit capacity to avoid truncated interaction scans.",
                    this
                );
                _hasWarnedHitBufferFull = true;
            }
            else if (!_isHitBufferFull)
            {
                _hasWarnedHitBufferFull = false;
            }

            var target = FindBestInteractable(_hits, count);
            SetCurrentInteractable(target);
            return _currentInteractable;
        }

        public bool InteractCurrent()
        {
            if (_currentInteractable == null || !_currentInteractable.IsInteractable)
            {
                return false;
            }

            _currentInteractable.OnInteract(gameObject);
            return true;
        }

        internal bool TryInteract(Collider[] hits, int count)
        {
            if (count <= 0)
                return false;

            var bestTarget = FindBestInteractable(hits, count);
            if (bestTarget == null)
                return false;

            SetCurrentInteractable(bestTarget);
            return InteractCurrent();
        }

        internal IInteractable FindBestInteractable(Collider[] hits, int count)
        {
            IInteractable best = null;
            int highestPriority = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;

                var interactable = hit.GetComponentInParent<IInteractable>();

                if (interactable != null && interactable.IsInteractable)
                {
                    if (interactable.InteractionPriority > highestPriority)
                    {
                        highestPriority = interactable.InteractionPriority;
                        best = interactable;
                    }
                }
            }
            return best;
        }

        private void EnsureHitBuffer()
        {
            int capacity = Mathf.Max(1, _hitCapacity);
            if (_hits != null && _hits.Length == capacity)
            {
                return;
            }

            _hits = new Collider[capacity];
        }

        private void SetCurrentInteractable(IInteractable interactable)
        {
            if (ReferenceEquals(_currentInteractable, interactable))
            {
                return;
            }

            _currentInteractable = interactable;
            TargetChanged?.Invoke(_currentInteractable);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.TransformPoint(_detectionOffset);
            Gizmos.color = new Color(0, 1, 1, 0.2f);
            Gizmos.DrawSphere(center, _radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, _radius);
        }
    }
}
