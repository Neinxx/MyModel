using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerState.Runtime
{
    /// <summary>
    /// 绝对独立的响应式角色状态容器 (Pure State Engine)
    /// 不包含任何特定系统的逻辑（如贴花）。通过 BaseFeatureSO 实现扩展。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerState", menuName = "Player System/Player State")]
    public class PlayerStateSO : ScriptableObject
    {
        [Header("Identity")]
        public string playerName = "Player";

        [Header("Base Stats")]
        public float baseMaxHP = 100f;
        public float baseAttack = 10f;

        [Header("World Persistence")]
        public Vector3 lastPosition;
        public string lastSceneName;
        public string lastSpawnPointID;

        [Header("Runtime Status")]
        public float currentHP = 100f;

        [Header("Final Stats (Read Only)")]
        [SerializeField]
        private float _finalMaxHP;
        public float FinalMaxHP => _finalMaxHP;

        [SerializeField]
        private float _finalAttack;
        public float FinalAttack => _finalAttack;

        // 使用 string 作为 SlotID，实现极致的解耦
        private Dictionary<string, BaseFeatureSO> _equippedFeatures =
            new Dictionary<string, BaseFeatureSO>();

        public event Action<string, BaseFeatureSO, BaseFeatureSO> OnFeatureChanged;
        public event Action OnStatsChanged;

        public void Equip(BaseFeatureSO feature)
        {
            if (feature == null || string.IsNullOrEmpty(feature.slotID))
                return;

            _equippedFeatures.TryGetValue(feature.slotID, out var oldFeature);
            if (oldFeature == feature)
                return;

            _equippedFeatures[feature.slotID] = feature;
            OnFeatureChanged?.Invoke(feature.slotID, oldFeature, feature);
            RefreshStats();
        }

        public void Unequip(string slotID)
        {
            if (_equippedFeatures.TryGetValue(slotID, out var oldFeature))
            {
                _equippedFeatures.Remove(slotID);
                OnFeatureChanged?.Invoke(slotID, oldFeature, null);
                RefreshStats();
            }
        }

        public BaseFeatureSO GetFeature(string slotID)
        {
            _equippedFeatures.TryGetValue(slotID, out var f);
            return f;
        }

        public IEnumerable<BaseFeatureSO> EquippedFeatures => _equippedFeatures.Values;

        public void RefreshStats()
        {
            float bonusHP = 0;
            float bonusAttack = 0;

            foreach (var f in _equippedFeatures.Values)
            {
                bonusHP += f.hpModifier;
                bonusAttack += f.attackModifier;
            }

            _finalMaxHP = baseMaxHP + bonusHP;
            _finalAttack = baseAttack + bonusAttack;
            OnStatsChanged?.Invoke();
        }

        public void ResetToDefault()
        {
            currentHP = baseMaxHP;
            lastPosition = Vector3.zero;
            lastSceneName = string.Empty;
            lastSpawnPointID = string.Empty;

            var slots = new List<string>(_equippedFeatures.Keys);
            foreach (var slot in slots)
                Unequip(slot);

            _equippedFeatures.Clear();
            RefreshStats();
        }
    }
}
