using InteractionSystem.Runtime;
using PlayerState.Runtime;
using UnityEngine;

namespace ModularDemo.Runtime
{
    /// <summary>
    /// 通用特性拾取组件 (Feature Pickup Component)
    /// 挂载在场景中的拾取物上，实现 IInteractable 接口，
    /// 当玩家接近并触发交互时，自动将指定的 BaseFeatureSO 装备到玩家状态中。
    /// </summary>
    [AddComponentMenu("Modular Demo/Feature Pickup")]
    [SelectionBase]
    [RequireComponent(typeof(BoxCollider))]
    public class FeaturePickupComponent : MonoBehaviour, IInteractable
    {
        [Header("Feature Configuration")]
        [Tooltip("要装备的玩家特性。")]
        public BaseFeatureSO featureToEquip;

        [Header("Interaction Configuration")]
        [Tooltip("交互优先级。")]
        [SerializeField]
        private int _interactionPriority = 50;

        [Tooltip("交互后是否销毁此拾取物。")]
        public bool destroyOnInteract = true;

        [Tooltip("拾取时生成的粒子特效。")]
        public GameObject pickupEffectPrefab;

        // --- IInteractable 接口实现 ---
        public int InteractionPriority => _interactionPriority;
        public bool IsInteractable => featureToEquip != null;

        public void OnInteract(GameObject interactor)
        {
            if (featureToEquip == null)
                return;

            // 1. 获取玩家身上的 PlayerStateBridge 组件
            var bridge = interactor.GetComponent<PlayerStateBridge>();
            if (bridge != null)
            {
                Debug.Log(
                    $"<color=#7C8CFF><b>[FeaturePickup]</b></color> 成功拾取并装备特性: <color=yellow>\"{featureToEquip.displayName}\"</color> -> 装备主体: <color=white>{interactor.name}</color>."
                );
                bridge.PickUpFeature(featureToEquip);

                // 2. 播放拾取特效
                if (pickupEffectPrefab != null)
                {
                    Instantiate(pickupEffectPrefab, transform.position, transform.rotation);
                }

                // 3. 处理拾取后的物体状态
                if (destroyOnInteract)
                {
                    Destroy(gameObject);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[FeaturePickup] 交互主体 {interactor.name} 缺失 PlayerStateBridge，无法装备特性！"
                );
            }
        }

        private void Reset()
        {
            // 自动配置碰撞体为 Trigger
            if (TryGetComponent<Collider>(out var col))
            {
                col.isTrigger = true;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 编辑器下绘制精美的高亮框体与拾取物图标
            Gizmos.color = new Color(0, 1, 1, 0.4f);
            Vector3 pos = transform.position;
            Gizmos.DrawWireCube(pos, Vector3.one * 0.6f);

            // 绘制拾取点专属的 Gizmo 图标，统一使用 0.8 米向上偏移
            Gizmos.DrawIcon(pos + Vector3.up * 0.8f, "icon_gzm_loot_1.png", true);
        }
#endif
    }
}
