using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameMainboard : MonoBehaviour
    {
        public static GameMainboard Instance { get; private set; }
        private static bool s_applicationQuitting;

        [Header("Profile")]
        [SerializeField] private MainboardProfile profile;

        [Header("Runtime State")]
        [SerializeField] private MainboardPhase phase = MainboardPhase.Offline;
        [SerializeField] private float bootProgress;
        [SerializeField] private string currentStep = "Offline";

        private readonly List<IGameFeature> _features = new List<IGameFeature>();
        private readonly ReactiveProperty<MainboardPhase> _phaseStream = new ReactiveProperty<MainboardPhase>(MainboardPhase.Offline);
        private readonly ReactiveProperty<float> _progressStream = new ReactiveProperty<float>(0f);
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        
        // AAA: 并发状态锁，防止UI狂点或重复触发导致的生命周期击穿
        private readonly SemaphoreSlim _stateLock = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _lifetimeCts;
        private MainboardContext _context;
        private bool _booting;
        private bool _booted;
        private bool _isDisposed;

        public IReadOnlyReactiveProperty<MainboardPhase> Phase => _phaseStream;
        public IReadOnlyReactiveProperty<float> BootProgress => _progressStream;
        public MainboardPhase CurrentPhase => phase;
        public float CurrentProgress => bootProgress;
        public string CurrentStep => currentStep;
        public MainboardContext Context => _context;
        public bool IsBooting => _booting;
        public bool IsBooted => _booted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );

            // AAA: 移除在此处提前创建 Context 的逻辑，确保每次 Boot 都是 100% 干净的环境
            LogInfo("Core orchestration engine awake. Waiting for boot command...");
        }

        private void Start()
        {
            if (profile != null && profile.autoBoot)
            {
                LogInfo("Auto-boot enabled. Initiating sequence...");
                BootAsync().Forget();
            }
        }

        public async UniTask BootAsync()
        {
            if (_isDisposed) return;

            // 获取异步锁，确保 Boot 和 Shutdown 绝对串行，不会产生竞态条件
            await _stateLock.WaitAsync();
            try
            {
                if (_booted || _booting)
                {
                    LogWarning("Mainboard is already booting or booted. Boot request ignored.");
                    return;
                }

                _booting = true;
                LogInfo("Boot phase started...");

                try
                {
                    // AAA: 重新分配干净的运行时环境上下文，防止复用残留 Bug
                    var signals = new SignalBus();
                    var services = new ServiceRegistry();
                    _context = new MainboardContext(this, profile, signals, services, _lifetimeCts.Token);
                    _disposables.Add(signals);

                    SetPhase(MainboardPhase.Booting, "Booting mainboard", 0f);
                    _context.Signals.Publish(new MainboardBootStartedSignal(this));

                    InstallFeatures();
                    await RunInstallPhaseAsync(_lifetimeCts.Token);
                    await RunBootPhaseAsync(_lifetimeCts.Token);

                    SetPhase(MainboardPhase.Running, "Running", 1f);
                    _context.Signals.Publish(new MainboardBootCompletedSignal(this));
                    _booted = true;
                    
                    LogSuccess("System boot sequence completed successfully. Mainboard is fully operational.");
                }
                catch (OperationCanceledException)
                {
                    SetPhase(MainboardPhase.Shutdown, "Boot cancelled", bootProgress);
                    LogWarning("Boot sequence was cancelled mid-flight.");
                }
                catch (Exception ex)
                {
                    SetPhase(MainboardPhase.Faulted, ex.Message, bootProgress);
                    if (_context != null) _context.Signals.Publish(new MainboardFaultedSignal(this, ex));
                    LogError($"CRITICAL FAULT during boot sequence: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    _booting = false;
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async UniTask ShutdownAsync()
        {
            if (_isDisposed) return;

            await _stateLock.WaitAsync();
            try
            {
                if (!_booted && !_booting) return;
                
                LogInfo("Initiating graceful shutdown sequence...");
                SetPhase(MainboardPhase.Shutdown, "Shutting down", 0f);
                
                if (_context != null)
                {
                    _context.Signals.Publish(new MainboardShutdownStartedSignal(this));

                    // 倒序卸载所有 Feature
                    for (int i = _features.Count - 1; i >= 0; i--)
                    {
                        if (_features[i] is IFeatureShutdown shutdown)
                        {
                            try
                            {
                                // 这里不能使用 _lifetimeCts 的 Token，因为关机流程必须完成，不能被外部随意取消
                                await shutdown.ShutdownAsync(_context, CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                LogError($"Exception during Feature '{GetFeatureName(_features[i])}' shutdown: {ex}");
                            }
                        }
                    }

                    _context.DisposeLevelScope();
                    _context.Services.Clear();
                    _context.Signals.Publish(new MainboardShutdownCompletedSignal(this));
                    
                    // 完全丢弃旧 Context 引用，触发 GC
                    _context = null; 
                }

                _features.Clear();
                SetPhase(MainboardPhase.Offline, "Offline", 0f);
                _booted = false;
                
                LogInfo("Graceful shutdown complete. System is now Offline.");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public UniTask<WorldNavigationResult> TransitionToLevelAsync(
            string levelName,
            CancellationToken cancellationToken
        )
        {
            if (_context == null || !_context.TryResolve<IWorldNavigator>(out var navigator))
            {
                LogError("Transition requested but WorldNavigator is missing or Mainboard is offline.");
                return UniTask.FromResult(
                    WorldNavigationResult.Failed("[Mainboard] World navigator is not available.")
                );
            }

            return navigator.LoadLevelAsync(levelName, cancellationToken);
        }

        public bool TryGetWorldSceneStatus(out WorldSceneRuntimeStatus status)
        {
            if (_context != null && _context.TryResolve<IWorldSceneDriver>(out var driver))
            {
                status = driver.RuntimeStatus;
                return true;
            }

            status = default;
            return false;
        }

        public string DescribeRuntimeStatus()
        {
            bool hasNavigator =
                _context != null && _context.TryResolve<IWorldNavigator>(out _);
            string worldStatus = TryGetWorldSceneStatus(out var status)
                ? status.ToString()
                : "WorldScene=<missing>";

            return $"Mainboard(Phase={phase}, Booted={_booted}, Booting={_booting}, Step='{currentStep}', Progress={bootProgress:P0}, Navigator={hasNavigator}) {worldStatus}";
        }

        public async UniTask NotifyLevelLoadedAsync(LevelContext level)
        {
            if (!_booted && !_booting) return;

            var scope = _context.BeginLevelScope(level.LevelName);
            var scopedLevel = new LevelContext(level.Config, level.Scene, scope);
            var handlers = _features.OfType<ILevelLoadHandler>().ToList();
            SetPhase(MainboardPhase.LevelLoading, $"Initializing level {scopedLevel.LevelName}", 0f);
            
            LogInfo($"Broadcasting OnLevelLoaded to {handlers.Count} features for level: {scopedLevel.LevelName}...");

            for (int i = 0; i < handlers.Count; i++)
            {
                _lifetimeCts.Token.ThrowIfCancellationRequested();

                var handler = handlers[i];
                SetPhase(
                    MainboardPhase.LevelLoading,
                    $"{GetFeatureName(handler)}: {scopedLevel.LevelName}",
                    handlers.Count == 0 ? 1f : (float)i / handlers.Count
                );

                await handler.OnLevelLoadedAsync(scopedLevel, scope.CancellationToken);
            }

            _context.Signals.Publish(new MainboardLevelLoadedSignal(scopedLevel));
            SetPhase(MainboardPhase.Running, "Running", 1f);
        }

        public async UniTask NotifyLevelUnloadingAsync(LevelContext level)
        {
            if (!_booted && !_booting) return;

            var scope = _context.CurrentLevelScope;
            var scopedLevel = new LevelContext(level.Config, level.Scene, scope);
            var handlers = _features.OfType<ILevelUnloadHandler>().Reverse().ToList();
            SetPhase(MainboardPhase.LevelUnloading, $"Unloading level {scopedLevel.LevelName}", 0f);
            
            LogInfo($"Broadcasting OnLevelUnloading to {handlers.Count} features for level: {scopedLevel.LevelName}...");

            for (int i = 0; i < handlers.Count; i++)
            {
                _lifetimeCts.Token.ThrowIfCancellationRequested();

                var handler = handlers[i];
                SetPhase(
                    MainboardPhase.LevelUnloading,
                    $"{GetFeatureName(handler)}: {scopedLevel.LevelName}",
                    handlers.Count == 0 ? 1f : (float)i / handlers.Count
                );

                await handler.OnLevelUnloadingAsync(scopedLevel, _lifetimeCts.Token);
            }

            _context.Signals.Publish(new MainboardLevelUnloadingSignal(scopedLevel));
            _context.DisposeLevelScope();
        }

        private void InstallFeatures()
        {
            _features.Clear();

            if (profile == null)
            {
                LogWarning("No profile assigned. Booting with an empty board.");
                return;
            }

            var installers = profile.GetInstallers().ToList();
            LogInfo($"Installing {installers.Count} feature(s) from Profile '{profile.name}'...");

            foreach (var installer in installers)
            {
                if (installer == null) continue;

                var feature = installer.CreateFeature();
                if (feature == null) continue;

                _features.Add(feature);
                _context.Signals.Publish(new MainboardModuleInstalledSignal(installer, feature));
            }
        }

        private async UniTask RunInstallPhaseAsync(CancellationToken cancellationToken)
        {
            if (_features.Count == 0) return;

            var installers = _features.OfType<IFeatureInstaller>().ToList();
            for (int i = 0; i < installers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var installer = installers[i];
                SetPhase(
                    MainboardPhase.Booting,
                    $"Installing {GetFeatureName(installer)}",
                    installers.Count == 0 ? 1f : (float)i / installers.Count
                );

                await installer.InstallAsync(_context, cancellationToken);
            }

            foreach (var feature in _features)
            {
                _context.Signals.Publish(new MainboardModuleReadySignal(feature));
            }
        }

        private async UniTask RunBootPhaseAsync(CancellationToken cancellationToken)
        {
            if (_features.Count == 0) return;

            var boots = _features.OfType<IFeatureBoot>().ToList();
            for (int i = 0; i < boots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var boot = boots[i];
                SetPhase(
                    MainboardPhase.Booting,
                    $"Booting {GetFeatureName(boot)}",
                    boots.Count == 0 ? 1f : (float)i / boots.Count
                );

                await boot.BootAsync(_context, cancellationToken);
            }
        }

        private static string GetFeatureName(object capability)
        {
            return capability is IGameFeature feature ? feature.Name : capability.GetType().Name;
        }

        private void SetPhase(MainboardPhase nextPhase, string step, float progress)
        {
            phase = nextPhase;
            currentStep = step;
            bootProgress = Mathf.Clamp01(progress);
            _phaseStream.Value = phase;
            _progressStream.Value = bootProgress;
            _context?.Signals.Publish(new MainboardPhaseChangedSignal(phase, currentStep, bootProgress));
        }
        
        // --- 高级日志模块 (Telemetry) ---
        private void LogInfo(string msg) => Debug.Log($"<color=#7C8CFF><b>[Mainboard]</b></color> {msg}");
        private void LogSuccess(string msg) => Debug.Log($"<color=#3FB950><b>[Mainboard]</b></color> {msg}");
        private void LogWarning(string msg) => Debug.LogWarning($"<color=#FFB443><b>[Mainboard]</b></color> {msg}", this);
        private void LogError(string msg) => Debug.LogError($"<color=#FF5252><b>[Mainboard]</b></color> {msg}", this);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            
            _isDisposed = true;
            
            // 立即停止所有进行中的异步任务
            _lifetimeCts?.Cancel();

            // AAA: 致命级修复 - Unity 销毁物体时不能等待 await 异步操作，否则继续执行到下一帧会导致 MissingReferenceException。
            // 这里我们拦截并强制进行同步级别的灾难恢复清理。
            if (_booted || _booting)
            {
                ForceSynchronousTeardown(!s_applicationQuitting && Application.isPlaying);
            }

            _lifetimeCts?.Dispose();
            _phaseStream.Dispose();
            _progressStream.Dispose();
            _disposables.Dispose();
            _stateLock.Dispose();
        }

        private void OnApplicationQuit()
        {
            s_applicationQuitting = true;
        }

        private void ForceSynchronousTeardown(bool warn)
        {
            if (warn)
            {
                LogWarning("Unexpected physical OnDestroy triggered before graceful Shutdown! Forcing synchronous teardown to prevent memory leaks.");
            }

            _features.Clear(); 
            if (_context != null)
            {
                _context.DisposeLevelScope();
                _context.Services.Clear();
                _context = null;
            }
            _booted = false;
            _booting = false;
        }
    }
}
