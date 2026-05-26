using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// 通用近战/感知交互器 (Proximity Interactor)
    /// 自动扫描周围的 IInteractable 物体并触发交互。
    /// </summary>
    public class ProximityInteractor : MonoBehaviour
    {
        [Header("Detection")]
        public float radius = 1.5f;
        [Tooltip("The position offset relative to the GameObject's local space.")]
        public Vector3 detectionOffset = Vector3.zero;
        public LayerMask targetLayers;
        public float scanFrequency = 0.2f;

        [Header("Runtime State")]
        [SerializeField] private bool _autoTrigger = true;
        private Collider[] _hits = new Collider[10];
        private float _timer;

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < scanFrequency) return;
            _timer = 0;

            ScanAndInteract();
        }

        private void ScanAndInteract()
        {
            Vector3 center = transform.TransformPoint(detectionOffset);
            int count = Physics.OverlapSphereNonAlloc(
                center, 
                radius, 
                _hits, 
                targetLayers, 
                QueryTriggerInteraction.Collide
            );

            if (count == 0) return;

            // 寻找最佳交互目标 (按优先级排序)
            var bestTarget = FindBestInteractable(count);
            
            if (bestTarget != null && _autoTrigger)
            {
                bestTarget.OnInteract(gameObject);
                
                // 如果是瞬时交互（如传送门），我们可能需要暂时禁用自己防止连发
                // 但具体策略由派生类或事件处理
            }
        }

        private IInteractable FindBestInteractable(int count)
        {
            IInteractable best = null;
            int highestPriority = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
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

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.TransformPoint(detectionOffset);
            Gizmos.color = new Color(0, 1, 1, 0.2f);
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}
