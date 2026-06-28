using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace PortalSystem.Runtime
{
    [DisallowMultipleComponent]
    [SelectionBase]
    [AddComponentMenu("Portal System/Portal Hub")]
    public class PortalHub : MonoBehaviour
    {
        [Header("Destination Configuration")]
        [Tooltip("Target scene name or ID.")]
        [FormerlySerializedAs("targetLevelName")]
        [SerializeField]
        private string _targetLevelName;

        [Tooltip("The SpawnPoint ID in the target scene.")]
        [FormerlySerializedAs("targetSpawnPointID")]
        [SerializeField]
        private string _targetSpawnPointId = "Start";

        [Header("Trigger")]
        [SerializeField]
        private bool _triggerOnTagEnter = true;

        [SerializeField]
        private string _triggerTag = "Player";

        [Header("Aesthetics")]
        [FormerlySerializedAs("portalColor")]
        [SerializeField]
        private Color _portalColor = new(0, 1, 1, 0.4f);

        [Header("Diagnostics")]
        [SerializeField]
        private bool _logTriggers = false;

        [Header("Events")]
        [FormerlySerializedAs("OnPortalTriggered")]
        [SerializeField]
        private UnityEvent<string, string> _onPortalTriggered = new();

        public event System.Action<string, string> OnPortalTriggeredAction;

        public string TargetLevelName => _targetLevelName;
        public string TargetSpawnPointID => _targetSpawnPointId;
        public Color PortalColor => _portalColor;
        public bool HasValidDestination => !string.IsNullOrWhiteSpace(_targetLevelName);
        public UnityEvent<string, string> PortalTriggered => _onPortalTriggered;

        public void ConfigureDestination(string targetLevelName, string targetSpawnPointId)
        {
            _targetLevelName = targetLevelName;
            _targetSpawnPointId = string.IsNullOrEmpty(targetSpawnPointId)
                ? "Start"
                : targetSpawnPointId;
        }

        public string DescribeRuntimeStatus()
        {
            string level = string.IsNullOrEmpty(_targetLevelName) ? "<empty>" : _targetLevelName;
            string spawn = string.IsNullOrEmpty(_targetSpawnPointId) ? "<empty>" : _targetSpawnPointId;
            return $"PortalHub(Name='{name}', TargetLevel='{level}', SpawnPoint='{spawn}', Valid={HasValidDestination})";
        }

        private void Reset()
        {
            if (!TryGetComponent<BoxCollider>(out var col))
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;
        }

        private void OnValidate()
        {
            if (TryGetComponent<BoxCollider>(out var col))
            {
                col.isTrigger = true;
            }
        }

        /// <summary>
        /// 外部触发传送的入口
        /// </summary>
        public void TriggerTeleport()
        {
            if (!HasValidDestination)
            {
                Debug.LogWarning($"[PortalHub] {gameObject.name} has no target level.");
                return;
            }

            if (_logTriggers)
            {
                Debug.Log($"[PortalHub] Triggered {gameObject.name} -> {_targetLevelName} [{_targetSpawnPointId}]");
            }

            _onPortalTriggered?.Invoke(_targetLevelName, _targetSpawnPointId);
            OnPortalTriggeredAction?.Invoke(_targetLevelName, _targetSpawnPointId);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_triggerOnTagEnter || string.IsNullOrEmpty(_triggerTag))
            {
                return;
            }

            if (other.CompareTag(_triggerTag))
            {
                TriggerTeleport();
            }
        }

        #region Editor Visualization
        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position + Vector3.up * 0.8f, "icon_Protal_1.png", true);

            Vector3 center = Vector3.zero;
            Vector3 size = Vector3.one;

            if (TryGetComponent<BoxCollider>(out var col))
            {
                center = col.center;
                size = col.size;
            }

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = _portalColor;
            Gizmos.DrawCube(center, size);

            Color edgeColor = _portalColor;
            edgeColor.a = 1.0f;
            Gizmos.color = edgeColor;
            Gizmos.DrawWireCube(center, size);

            float pulse = Mathf.PingPong(Time.realtimeSinceStartup, 1f);
            Gizmos.color = new Color(
                _portalColor.r,
                _portalColor.g,
                _portalColor.b,
                0.1f + pulse * 0.2f
            );
            Gizmos.DrawSphere(center, Mathf.Min(size.x, size.y) * 0.4f);

            Gizmos.color = edgeColor;
            Gizmos.DrawRay(center, Vector3.forward * size.z * 0.6f);

            Gizmos.matrix = oldMatrix;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(_targetLevelName))
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = edgeColor;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontStyle = FontStyle.Bold;

                string label = $"Portal -> {_targetLevelName}";
                if (!string.IsNullOrEmpty(_targetSpawnPointId))
                    label += $"\n[{_targetSpawnPointId}]";

                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * (transform.localScale.y * 0.6f),
                    label,
                    style
                );
            }
#endif
        }
        #endregion
    }
}
