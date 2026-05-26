using UnityEngine.SceneManagement;

namespace WorldSceneModule.Runtime
{
    public readonly struct WorldSceneLoadResult
    {
        private WorldSceneLoadResult(
            bool success,
            LevelConfig level,
            Scene scene,
            string error
        )
        {
            Success = success;
            Level = level;
            Scene = scene;
            Error = error;
        }

        public bool Success { get; }
        public LevelConfig Level { get; }
        public Scene Scene { get; }
        public string Error { get; }

        public static WorldSceneLoadResult Loaded(LevelConfig level, Scene scene)
        {
            return new WorldSceneLoadResult(true, level, scene, null);
        }

        public static WorldSceneLoadResult Failed(string error)
        {
            return new WorldSceneLoadResult(false, default, default, error);
        }
    }
}
