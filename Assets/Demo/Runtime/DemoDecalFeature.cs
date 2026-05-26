using UnityEngine;
using PlayerState.Runtime;

namespace ModularDemo.Runtime
{
    /// <summary>
    /// Demo 特性基类
    /// </summary>
    public abstract class DemoDecalFeature : BaseFeatureSO
    {
        public abstract void Apply(GameObject target);
        public abstract void Remove(GameObject target);
    }
}
