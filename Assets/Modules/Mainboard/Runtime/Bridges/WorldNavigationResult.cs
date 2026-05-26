using WorldSceneModule.Runtime;

namespace Mainboard.Runtime
{
    public readonly struct WorldNavigationResult
    {
        private WorldNavigationResult(bool success, WorldSceneLoadResult sceneResult, string error)
        {
            Success = success;
            SceneResult = sceneResult;
            Error = error;
        }

        public bool Success { get; }
        public WorldSceneLoadResult SceneResult { get; }
        public string Error { get; }

        public static WorldNavigationResult Completed(WorldSceneLoadResult sceneResult)
        {
            return new WorldNavigationResult(true, sceneResult, null);
        }

        public static WorldNavigationResult Failed(string error)
        {
            return new WorldNavigationResult(false, default, error);
        }
    }
}
