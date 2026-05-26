using UnityEngine;

namespace UISystem.Runtime
{
    /// <summary>
    /// 泛用型 UI 视图 (Generic UI View)
    /// 用于不需要编写专属逻辑代码的纯展示面板（如简单的封面、转场黑屏等）。
    /// 可以直接挂载到 GameObject 上，并由 UIManager 管理。
    /// </summary>
    [AddComponentMenu("UI System/Generic UI View")]
    public class GenericUIView : UIView
    {
        // 继承自 UIView，已包含生命周期、淡入淡出动画、事件派发等所有核心功能。
        // 如果需要在面板打开/关闭时触发逻辑，可以直接在 Inspector 中配置 OnBeforeOpen / OnAfterClose 事件。
    }
}
