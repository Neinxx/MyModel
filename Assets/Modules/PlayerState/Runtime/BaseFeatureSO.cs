using UnityEngine;

namespace PlayerState.Runtime
{
    /// <summary>
    /// 抽象玩家特性基类 (Universal Feature Base)
    /// 允许不同模块（贴花、装备、技能）扩展自己的数据结构。
    /// </summary>
    public abstract class BaseFeatureSO : ScriptableObject
    {
        [Header("Common Metadata")]
        public string featureID;
        public string slotID = "Default";

        [Tooltip("The human-readable name of the feature.")]
        public string displayName;

        [Header("Base Modifiers")]
        public float hpModifier = 0f;
        public float attackModifier = 0f;
    }
}
