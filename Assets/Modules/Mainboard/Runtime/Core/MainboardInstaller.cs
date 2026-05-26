using UnityEngine;

namespace Mainboard.Runtime
{
    public abstract class MainboardInstaller : ScriptableObject
    {
        [SerializeField] private int priority;

        public int Priority => priority;
        public virtual string DisplayName => name;

        public abstract IGameFeature CreateFeature();
    }
}
