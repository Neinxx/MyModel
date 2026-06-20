using UnityEngine.SceneManagement;

namespace Mainboard.Runtime
{
    public readonly struct LevelContext
    {
        public LevelContext(string levelName, Scene scene, LevelScope scope = null, object config = null)
        {
            LevelName = levelName;
            Scene = scene;
            Scope = scope;
            Config = config;
        }

        public string LevelName { get; }
        public Scene Scene { get; }
        public LevelScope Scope { get; }
        public object Config { get; }

        public bool TryGetConfig<T>(out T config)
        {
            if (Config is T typedConfig)
            {
                config = typedConfig;
                return true;
            }

            config = default;
            return false;
        }
    }
}
