using UnityEngine;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// 通用交互接口 (Universal Interaction Interface)
    /// 任何可以被玩家或 NPC 触发的物体都应实现此接口。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 当交互发生时调用
        /// </summary>
        /// <param name="interactor">发起交互的主体</param>
        void OnInteract(GameObject interactor);

        /// <summary>
        /// 交互优先级 (用于多个物体重叠时筛选最佳目标)
        /// </summary>
        int InteractionPriority { get; }

        /// <summary>
        /// 该交互目前是否可用
        /// </summary>
        bool IsInteractable { get; }
    }
}
