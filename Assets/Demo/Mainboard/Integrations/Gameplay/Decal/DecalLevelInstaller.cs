using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DecalMini;
using Mainboard.Runtime;
using UnityEngine;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime.Integrations
{
    public interface IDecalLevelRuntime
    {
        DecalLevelDataMini LevelData { get; }
        bool HasLevelData { get; }
    }

    public interface IDecalSystemRuntime
    {
        void ClearRuntimePool();
        void LoadLevelData(DecalLevelDataMini levelData);
        void UnloadLevelData(DecalLevelDataMini levelData);
    }

    [CreateAssetMenu(fileName = "DecalFeature", menuName = "Demo/Mainboard/Features/Decal")]
    public sealed class DecalLevelInstaller : MainboardInstaller
    {
        [SerializeField] private bool clearRuntimePoolOnLevelLoad = true;
        [SerializeField] private bool useSceneFallbackData = true;

        public override IGameFeature CreateFeature()
        {
            return new DecalFeature(clearRuntimePoolOnLevelLoad, useSceneFallbackData);
        }

        private sealed class DecalFeature :
            IGameFeature,
            IFeatureInstaller,
            ILevelLoadHandler,
            ILevelUnloadHandler,
            IFeatureShutdown
        {
            private readonly bool _clearRuntimePoolOnLevelLoad;
            private readonly bool _useSceneFallbackData;
            private MainboardContext _context;
            private IDecalSystemRuntime _decalSystem;
            private bool _ownsDecalSystem;
            private readonly List<DecalLevelDataMini> _loadedLevelData = new List<DecalLevelDataMini>();
            private IDecalLevelRuntime _runtime;

            public DecalFeature(bool clearRuntimePoolOnLevelLoad, bool useSceneFallbackData)
            {
                _clearRuntimePoolOnLevelLoad = clearRuntimePoolOnLevelLoad;
                _useSceneFallbackData = useSceneFallbackData;
            }

            public string Name => "Decal";

            public UniTask InstallAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                _context = context;
                if (!context.TryResolve(out _decalSystem))
                {
                    _decalSystem = new DecalSystemRuntimeAdapter();
                    context.RegisterService<IDecalSystemRuntime>(_decalSystem);
                    _ownsDecalSystem = true;
                }

                return UniTask.CompletedTask;
            }

            public UniTask OnLevelLoadedAsync(LevelContext level, CancellationToken cancellationToken)
            {
                UnloadCurrentData();

                if (_clearRuntimePoolOnLevelLoad)
                    _decalSystem.ClearRuntimePool();

                var hasLevelConfig = level.TryGetConfig<LevelConfig>(out var levelConfig);
                if (hasLevelConfig && levelConfig.moduleData != null)
                {
                    var decalData = levelConfig.moduleData.GetSubData<DecalLevelDataMini>();
                    if (decalData != null)
                    {
                        LoadLevelData(decalData);
                        RegisterRuntime(level, decalData);
                        _context.Signals.Publish(new DecalLevelDataLoadedSignal(levelConfig));
                        return UniTask.CompletedTask;
                    }
                }

                if (_useSceneFallbackData)
                    LoadFallbackSceneData(level);

                RegisterRuntime(level, _loadedLevelData.Count > 0 ? _loadedLevelData[0] : null);
                _context.Signals.Publish(new DecalLevelDataLoadedSignal(levelConfig));
                return UniTask.CompletedTask;
            }

            public UniTask OnLevelUnloadingAsync(LevelContext level, CancellationToken cancellationToken)
            {
                UnloadCurrentData();
                return UniTask.CompletedTask;
            }

            public UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                UnloadCurrentData();
                if (_ownsDecalSystem)
                    context.Services.Unregister<IDecalSystemRuntime>();

                return UniTask.CompletedTask;
            }

            private void LoadFallbackSceneData(LevelContext level)
            {
                var roots = level.Scene.IsValid()
                    ? level.Scene.GetRootGameObjects()
                    : Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

                foreach (var root in roots)
                {
                    var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var behaviour in behaviours)
                    {
                        if (behaviour is not IDecalLevelDataProvider provider)
                            continue;

                        LoadLevelData(provider.LevelData);
                    }
                }
            }

            private void LoadLevelData(DecalLevelDataMini levelData)
            {
                if (levelData == null || _loadedLevelData.Contains(levelData))
                    return;

                _decalSystem.LoadLevelData(levelData);
                _loadedLevelData.Add(levelData);
            }

            private void UnloadCurrentData()
            {
                if (_loadedLevelData.Count == 0)
                    return;

                for (var i = _loadedLevelData.Count - 1; i >= 0; i--)
                    _decalSystem.UnloadLevelData(_loadedLevelData[i]);

                _loadedLevelData.Clear();
                _runtime = null;
            }

            private void RegisterRuntime(LevelContext level, DecalLevelDataMini levelData)
            {
                _runtime = new DecalLevelRuntime(levelData);
                level.Scope?.RegisterService<IDecalLevelRuntime>(_runtime);
            }

        }

        private sealed class DecalSystemRuntimeAdapter : IDecalSystemRuntime
        {
            public void ClearRuntimePool()
            {
                DecalSystemMini.ClearRuntimePool();
            }

            public void LoadLevelData(DecalLevelDataMini levelData)
            {
                levelData?.LoadIntoKernel();
            }

            public void UnloadLevelData(DecalLevelDataMini levelData)
            {
                levelData?.UnloadFromKernel();
            }
        }

        private sealed class DecalLevelRuntime : IDecalLevelRuntime
        {
            public DecalLevelRuntime(DecalLevelDataMini levelData)
            {
                LevelData = levelData;
            }

            public DecalLevelDataMini LevelData { get; }
            public bool HasLevelData => LevelData != null;
        }
    }
}
