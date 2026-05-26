using UnityEngine;
using UnityEngine.Events;
using InteractionSystem.Runtime;

namespace PortalSystem.Runtime
{
    /// <summary>
    /// 工业级传送门中枢 (Portal Hub) - 零依赖版
    /// </summary>
    [ExecuteInEditMode]
    [SelectionBase]
    [AddComponentMenu("Portal System/Portal Hub")]
    public class PortalHub : MonoBehaviour, IInteractable
    {
        // --- IInteractable Implementation ---
        public void OnInteract(GameObject interactor) => TriggerTeleport();
        public int InteractionPriority => 100;
        public bool IsInteractable => true;

        [Header("Destination Configuration")]
        [Tooltip("Target scene name or ID.")]
        public string targetLevelName;

        [Tooltip("The SpawnPoint ID in the target scene.")]
        public string targetSpawnPointID = "Start";

        [Header("Aesthetics")]
        public Color portalColor = new(0, 1, 1, 0.4f);

        [Header("Events")]
        public UnityEvent<string, string> OnPortalTriggered;

        /// <summary>
        /// C# 原始事件，供高性能逻辑层订阅
        /// 参数 1: 目标关卡, 参数 2: 目标生成点
        /// </summary>
        public event System.Action<string, string> OnPortalTriggeredAction;

        public string TargetLevelName => targetLevelName;
        public string TargetSpawnPointID => targetSpawnPointID;
        public bool HasValidDestination => !string.IsNullOrWhiteSpace(targetLevelName);

        public string DescribeRuntimeStatus()
        {
            string level = string.IsNullOrEmpty(targetLevelName) ? "<empty>" : targetLevelName;
            string spawn = string.IsNullOrEmpty(targetSpawnPointID) ? "<empty>" : targetSpawnPointID;
            return $"PortalHub(Name='{name}', TargetLevel='{level}', SpawnPoint='{spawn}', Valid={HasValidDestination}, Interactable={IsInteractable})";
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
            if (string.IsNullOrEmpty(targetLevelName))
            {
                Debug.LogWarning($"[PortalHub] {gameObject.name} 尝试传送，但目标关卡为空！");
                return;
            }

            Debug.Log(
                $"<color=#7C8CFF><b>[PortalHub]</b></color> 触发器激活 -> {targetLevelName} [{targetSpawnPointID}]"
            );

            OnPortalTriggered?.Invoke(targetLevelName, targetSpawnPointID);
            OnPortalTriggeredAction?.Invoke(targetLevelName, targetSpawnPointID);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 简单的触发检测逻辑
            if (other.CompareTag("Player"))
            {
                TriggerTeleport();
            }
        }

        #region Editor Visualization
        private void OnDrawGizmos()
        {
            // 0. Draw custom icon (Portal style, shifted by 0.8f)
            Gizmos.DrawIcon(transform.position + Vector3.up * 0.8f, "icon_Protal_1.png", true);

            Vector3 center = Vector3.zero;
            Vector3 size = Vector3.one;

            if (TryGetComponent<BoxCollider>(out var col))
            {
                center = col.center;
                size = col.size;
            }

            // Set matrix to follow transform (Position, Rotation, Scale)
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // 1. Draw Outer Frame (Glassy)
            Gizmos.color = portalColor;
            Gizmos.DrawCube(center, size);

            Color edgeColor = portalColor;
            edgeColor.a = 1.0f;
            Gizmos.color = edgeColor;
            Gizmos.DrawWireCube(center, size);

            // 2. Draw Inner Core (Vortex/Pulse look)
            float pulse = Mathf.PingPong(Time.realtimeSinceStartup, 1f);
            Gizmos.color = new Color(
                portalColor.r,
                portalColor.g,
                portalColor.b,
                0.1f + pulse * 0.2f
            );
            Gizmos.DrawSphere(center, Mathf.Min(size.x, size.y) * 0.4f);

            // 3. Draw Directional Indicator (Forward is the entry direction)
            Gizmos.color = edgeColor;
            Gizmos.DrawRay(center, Vector3.forward * size.z * 0.6f);

            // Restore matrix
            Gizmos.matrix = oldMatrix;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(targetLevelName))
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = edgeColor;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontStyle = FontStyle.Bold;

                string label = $"Portal -> {targetLevelName}";
                if (!string.IsNullOrEmpty(targetSpawnPointID))
                    label += $"\n[{targetSpawnPointID}]";

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
