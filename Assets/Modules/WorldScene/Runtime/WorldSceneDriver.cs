using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldSceneModule.Runtime
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public class WorldSceneDriver : MonoBehaviour, IWorldSceneDriver
    {
        [Header("Assets")]
        [SerializeField] private LevelRegistry registry;

        [Header("Runtime State")]
        [SerializeField] private WorldSceneState state = WorldSceneState.Idle;
        [SerializeField] private string currentStep = "Ready";
        [SerializeField] private float loadingProgress;

        private bool _isLoading;
        private string _activeLevelPath;
        private LevelConfig _activeLevelConfig;

        public LevelRegistry Registry
        {
            get => registry;
            set => registry = value;
        }

        public WorldSceneState State => state;
        public string CurrentStep => currentStep;
        public float LoadingProgress => loadingProgress;
        public LevelConfig ActiveLevel => _activeLevelConfig;
        public WorldSceneRuntimeStatus RuntimeStatus =>
            new WorldSceneRuntimeStatus(
                state,
                currentStep,
                loadingProgress,
                _isLoading,
                _activeLevelConfig.levelName,
                registry != null
            );

        public event WorldSceneLoadingHandler LevelLoading;
        public event WorldSceneLifecycleHandler LevelLoaded;
        public event WorldSceneLifecycleHandler LevelUnloading;

        protected virtual void Awake()
        {
            if (Application.isPlaying && transform.parent != null)
                transform.SetParent(null, true);

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        public async UniTask InitializeWorldAsync(CancellationToken cancellationToken)
        {
            if (_isLoading)
                return;

            _isLoading = true;
            try
            {
                SetState(WorldSceneState.Loading, "Loading Persistent World...", 0f);

                if (registry != null && registry.persistentWorldAsset != null)
                {
                    string path = registry.GetPersistentWorldPath();
                    if (!string.IsNullOrEmpty(path))
                        await LoadSceneAsync(path, LoadSceneMode.Single, 0f, 1f, cancellationToken);
                }

                SetState(WorldSceneState.Ready, "World Ready", 1f);
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async UniTask<WorldSceneLoadResult> LoadLevelAsync(
            string levelName,
            CancellationToken cancellationToken
        )
        {
            if (_isLoading)
                return Fail("World scene driver is already loading.");

            if (registry == null)
                return Fail("Level registry is not assigned.");

            if (string.IsNullOrWhiteSpace(levelName))
                return Fail("Level name is empty.");

            var config = registry.GetConfig(levelName);
            if (string.IsNullOrEmpty(config.levelName))
                return Fail($"Level '{levelName}' is not registered.");

            string path = registry.GetScenePath(config.levelName);
            if (string.IsNullOrEmpty(path))
                return Fail($"Scene path for level '{config.levelName}' is empty.");

            _isLoading = true;
            try
            {
                SetState(WorldSceneState.Loading, "Unloading Previous Level...", 0f);
                await UnloadCurrentLevelCoreAsync(cancellationToken);
                loadingProgress = 0.2f;

                currentStep = $"Loading Scene: {config.levelName}...";
                await InvokeLevelLoadingAsync(config, cancellationToken);

                await LoadSceneAsync(path, LoadSceneMode.Additive, 0.2f, 0.6f, cancellationToken);

                var loadedScene = GetSceneByPathOrName(path);
                if (loadedScene.IsValid())
                    SceneManager.SetActiveScene(loadedScene);

                _activeLevelPath = path;
                _activeLevelConfig = config;

                SetState(WorldSceneState.Ready, "Operational", 1f);
                await InvokeLevelLifecycleAsync(LevelLoaded, config, loadedScene, cancellationToken);
                return WorldSceneLoadResult.Loaded(config, loadedScene);
            }
            catch (OperationCanceledException)
            {
                SetState(WorldSceneState.Idle, "Cancelled", loadingProgress);
                throw;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async UniTask UnloadCurrentLevelAsync(CancellationToken cancellationToken)
        {
            if (_isLoading)
                return;

            _isLoading = true;
            try
            {
                SetState(WorldSceneState.Loading, "Unloading Current Level...", 0f);
                await UnloadCurrentLevelCoreAsync(cancellationToken);
                SetState(WorldSceneState.Ready, "World Ready", 1f);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async UniTask UnloadCurrentLevelCoreAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_activeLevelPath))
                return;

            var scene = GetSceneByPathOrName(_activeLevelPath);
            await InvokeLevelLifecycleAsync(LevelUnloading, _activeLevelConfig, scene, cancellationToken);

            if (scene.isLoaded)
                await AwaitSceneOperationAsync(
                    SceneManager.UnloadSceneAsync(scene),
                    0f,
                    1f,
                    cancellationToken
                );

            _activeLevelPath = null;
            _activeLevelConfig = default;
        }

        private async UniTask LoadSceneAsync(
            string scenePath,
            LoadSceneMode mode,
            float progressOffset,
            float progressScale,
            CancellationToken cancellationToken
        )
        {
            var operation = LoadSceneAsyncHelper(scenePath, mode);
            await AwaitSceneOperationAsync(operation, progressOffset, progressScale, cancellationToken);
        }

        private async UniTask AwaitSceneOperationAsync(
            AsyncOperation operation,
            float progressOffset,
            float progressScale,
            CancellationToken cancellationToken
        )
        {
            if (operation == null)
                throw new InvalidOperationException("Scene operation could not be started.");

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                loadingProgress = progressOffset + (operation.progress * progressScale);
                await UniTask.Yield(cancellationToken);
            }
        }

        private AsyncOperation LoadSceneAsyncHelper(string scenePath, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scenePath,
                    new LoadSceneParameters(mode)
                );
            }
#endif
            return SceneManager.LoadSceneAsync(scenePath, mode);
        }

        private Scene GetSceneByPathOrName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return default;

            return path.Contains(".unity")
                ? SceneManager.GetSceneByPath(path)
                : SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(path));
        }

        private async UniTask InvokeLevelLoadingAsync(
            LevelConfig config,
            CancellationToken cancellationToken
        )
        {
            var handlers = LevelLoading;
            if (handlers == null)
                return;

            foreach (WorldSceneLoadingHandler handler in handlers.GetInvocationList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler(config, cancellationToken);
            }
        }

        private async UniTask InvokeLevelLifecycleAsync(
            WorldSceneLifecycleHandler handlers,
            LevelConfig config,
            Scene scene,
            CancellationToken cancellationToken
        )
        {
            if (handlers == null)
                return;

            foreach (WorldSceneLifecycleHandler handler in handlers.GetInvocationList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler(config, scene, cancellationToken);
            }
        }

        private WorldSceneLoadResult Fail(string error)
        {
            SetState(WorldSceneState.Error, error, loadingProgress);
            Debug.LogWarning($"[WorldSceneDriver] {error}", this);
            return WorldSceneLoadResult.Failed(error);
        }

        private void SetState(WorldSceneState nextState, string step, float progress)
        {
            state = nextState;
            currentStep = step;
            loadingProgress = Mathf.Clamp01(progress);
        }
    }

    public enum WorldSceneState
    {
        Idle,
        Loading,
        Ready,
        Error,
    }
}
