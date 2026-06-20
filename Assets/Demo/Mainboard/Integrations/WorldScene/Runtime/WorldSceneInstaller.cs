using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using WorldSceneModule.Runtime;
using UnityEngine.SceneManagement;

namespace Mainboard.Runtime
{
    [CreateAssetMenu(fileName = "WorldSceneFeature", menuName = "Demo/Mainboard/Features/World Scene")]
    public class WorldSceneInstaller : MainboardInstaller
    {
        [SerializeField] private WorldRuntimeConfig config;

        public override IGameFeature CreateFeature()
        {
            return config != null ? new WorldFeature(config) : null;
        }

        private sealed class WorldFeature :
            IGameFeature,
            IFeatureInstaller,
            IFeatureBoot,
            IFeatureShutdown
        {
            private readonly WorldRuntimeConfig _config;
            private MainboardContext _context;
            private WorldRuntimeHandle _runtime;
            private IWorldNavigator _navigator;
            private WorldBootSequence _bootSequence;
            private IWorldSceneDriver _driver;

            public WorldFeature(WorldRuntimeConfig config)
            {
                _config = config;
            }

            public string Name => "World Scene";

            public UniTask InstallAsync(
                MainboardContext context,
                CancellationToken cancellationToken
            )
            {
                _context = context;
                _runtime = WorldRuntimeResolver.Resolve(context, _config);
                _driver = _runtime.Driver;

                if (_driver == null)
                {
                    Debug.LogWarning("[Mainboard] WorldSceneDriver is not available.");
                    return UniTask.CompletedTask;
                }

                _driver.LevelLoaded += HandleLevelLoaded;
                _driver.LevelUnloading += HandleLevelUnloading;
                _navigator = new WorldNavigator(_driver);
                _bootSequence = new WorldBootSequence(_config, _driver, _navigator);
                context.RegisterService(_driver);
                context.RegisterService(_navigator);
                return UniTask.CompletedTask;
            }

            public async UniTask BootAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                if (_bootSequence != null)
                    await _bootSequence.RunAsync(context, cancellationToken);
            }

            public UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                if (_driver != null)
                {
                    _driver.LevelLoaded -= HandleLevelLoaded;
                    _driver.LevelUnloading -= HandleLevelUnloading;
                }

                context.Services.Unregister<IWorldNavigator>();
                context.Services.Unregister<IWorldSceneDriver>();

                _runtime?.Dispose();
                _runtime = null;
                _bootSequence = null;
                _navigator = null;
                _driver = null;

                return UniTask.CompletedTask;
            }

            private UniTask HandleLevelLoaded(
                LevelConfig config,
                Scene scene,
                CancellationToken cancellationToken
            )
            {
                if (_context?.Board == null)
                    return UniTask.CompletedTask;

                return _context.Board.NotifyLevelLoadedAsync(
                    new LevelContext(config.levelName, scene, null, config)
                );
            }

            private UniTask HandleLevelUnloading(
                LevelConfig config,
                Scene scene,
                CancellationToken cancellationToken
            )
            {
                if (_context?.Board == null)
                    return UniTask.CompletedTask;

                return _context.Board.NotifyLevelUnloadingAsync(
                    new LevelContext(config.levelName, scene, null, config)
                );
            }
        }
    }

    public enum WorldBootMode
    {
        UseWorldSceneDefaults,
        LoadExplicitLevel,
        ShowStartUI,
        DoNothing,
    }

}
