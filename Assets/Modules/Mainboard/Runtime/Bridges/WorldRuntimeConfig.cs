using UnityEngine;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime
{
    [CreateAssetMenu(fileName = "WorldRuntimeConfig", menuName = "Mainboard/World Runtime Config")]
    public sealed class WorldRuntimeConfig : ScriptableObject
    {
        public WorldSceneDriver sceneDriverOverride;
        public LevelRegistry registry;
        public bool createSceneDriverIfMissing = true;
        public bool initializePersistentWorld = true;
        public WorldBootMode bootMode = WorldBootMode.UseWorldSceneDefaults;
        public string defaultBootLevel = "Level1";
        public string explicitBootLevel;
        public string startUIViewID = "LevelSelection";
    }
}
