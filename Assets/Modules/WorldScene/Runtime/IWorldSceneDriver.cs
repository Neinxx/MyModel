using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace WorldSceneModule.Runtime
{
    public interface IWorldSceneDriver
    {
        LevelRegistry Registry { get; set; }
        WorldSceneState State { get; }
        string CurrentStep { get; }
        float LoadingProgress { get; }
        LevelConfig ActiveLevel { get; }
        WorldSceneRuntimeStatus RuntimeStatus { get; }

        event WorldSceneLoadingHandler LevelLoading;
        event WorldSceneLifecycleHandler LevelLoaded;
        event WorldSceneLifecycleHandler LevelUnloading;

        UniTask InitializeWorldAsync(CancellationToken cancellationToken);
        UniTask<WorldSceneLoadResult> LoadLevelAsync(
            string levelName,
            CancellationToken cancellationToken
        );
        UniTask UnloadCurrentLevelAsync(CancellationToken cancellationToken);
    }

    public delegate UniTask WorldSceneLoadingHandler(
        LevelConfig config,
        CancellationToken cancellationToken
    );

    public delegate UniTask WorldSceneLifecycleHandler(
        LevelConfig config,
        Scene scene,
        CancellationToken cancellationToken
    );
}
