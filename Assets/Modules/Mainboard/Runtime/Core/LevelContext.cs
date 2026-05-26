using UnityEngine.SceneManagement;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime
{
    public readonly struct LevelContext
    {
        public LevelContext(LevelConfig config, Scene scene, LevelScope scope = null)
        {
            Config = config;
            Scene = scene;
            Scope = scope;
        }

        public LevelConfig Config { get; }
        public Scene Scene { get; }
        public LevelScope Scope { get; }
        public string LevelName => Config.levelName;
    }
}
