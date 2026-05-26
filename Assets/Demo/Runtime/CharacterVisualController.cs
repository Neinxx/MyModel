using UnityEngine;
using PlayerState.Runtime;
using CharacterController.Runtime;

namespace ModularDemo.Runtime
{
    public class CharacterVisualController : MonoBehaviour
    {
        public PlayerStateSO playerState;
        private GameObject _featureTarget;

        private void OnEnable()
        {
            if (playerState == null) return;
            _featureTarget = ResolveFeatureTarget();
            playerState.OnFeatureChanged += OnFeatureChanged;
            
            // 初始同步
            foreach (var feature in playerState.EquippedFeatures)
            {
                ApplyFeature(feature);
            }
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.OnFeatureChanged -= OnFeatureChanged;
        }

        private void OnFeatureChanged(string slotID, BaseFeatureSO oldF, BaseFeatureSO newF)
        {
            // 🌟 极致 O(1) 零内存分配重用：如果新旧特性都是贴花特性，则跳过销毁流程，直接执行覆写更新！
            if (oldF is DemoDecalFeature && newF is DemoDecalFeature)
            {
                ApplyFeature(newF);
            }
            else
            {
                if (oldF != null) RemoveFeature(oldF);
                if (newF != null) ApplyFeature(newF);
            }
        }

        private void ApplyFeature(BaseFeatureSO feature)
        {
            if (feature is DemoDecalFeature demoFeature)
            {
                demoFeature.Apply(_featureTarget != null ? _featureTarget : gameObject);
            }
        }

        private void RemoveFeature(BaseFeatureSO feature)
        {
            if (feature is DemoDecalFeature demoFeature)
            {
                demoFeature.Remove(_featureTarget != null ? _featureTarget : gameObject);
            }
        }

        private GameObject ResolveFeatureTarget()
        {
            var registry = GetComponentInParent<CharacterSocketRegistry>();
            return registry != null ? registry.gameObject : gameObject;
        }
    }
}
