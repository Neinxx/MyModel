using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Mainboard.Runtime
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameMainboard : MonoBehaviour, IMainboardStateSink
    {
        public static GameMainboard Instance { get; private set; }

        private static bool s_applicationQuitting;

        [Header("Profile")]
        [SerializeField] private MainboardProfile profile;

        [Header("Runtime State")]
        [SerializeField] private MainboardPhase phase = MainboardPhase.Offline;
        [SerializeField] private float bootProgress;
        [SerializeField] private string currentStep = "Offline";

        private readonly ReactiveProperty<MainboardPhase> _phaseStream =
            new ReactiveProperty<MainboardPhase>(MainboardPhase.Offline);
        private readonly ReactiveProperty<float> _progressStream = new ReactiveProperty<float>(0f);
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly SemaphoreSlim _stateLock = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _lifetimeCts;
        private MainboardContext _context;
        private MainboardLifecycleRunner _lifecycle;
        private MainboardLevelDispatcher _levels;
        private MainboardLogger _logger;
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

            _logger = new MainboardLogger(this);
            _lifecycle = new MainboardLifecycleRunner(this, _logger);
            _levels = new MainboardLevelDispatcher(this, _logger);
            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );

            _logger.Info("Mainboard awake. Waiting for boot command.");
        }

        private void Start()
        {
            if (profile != null && profile.autoBoot)
                BootAsync().Forget();
        }

        public async UniTask BootAsync()
        {
            if (_isDisposed)
                return;

            await _stateLock.WaitAsync();
            try
            {
                if (_booted || _booting)
                {
                    _logger.Warning("Boot request ignored because Mainboard is already active.");
                    return;
                }

                _booting = true;
                _context = CreateContext();

                try
                {
                    await _lifecycle.BootAsync(_context, _lifetimeCts.Token);
                    _booted = true;
                    _logger.Success("Boot sequence completed.");
                }
                catch (OperationCanceledException)
                {
                    SetPhase(MainboardPhase.Shutdown, "Boot cancelled", bootProgress);
                    _logger.Warning("Boot sequence was cancelled.");
                }
                catch (Exception exception)
                {
                    SetPhase(MainboardPhase.Faulted, exception.Message, bootProgress);
                    _context.Signals.Publish(new MainboardFaultedSignal(this, exception));
                    _logger.Error($"Boot failed: {exception.Message}\n{exception.StackTrace}");
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
            if (_isDisposed)
                return;

            await _stateLock.WaitAsync();
            try
            {
                if (!_booted && !_booting)
                    return;

                await _lifecycle.ShutdownAsync(_context);
                _context = null;
                _booted = false;
                _booting = false;
                SetPhase(MainboardPhase.Offline, "Offline", 0f);
                _logger.Info("Shutdown complete.");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public string DescribeRuntimeStatus()
        {
            return $"Mainboard(Phase={phase}, Booted={_booted}, Booting={_booting}, Step='{currentStep}', Progress={bootProgress:P0})";
        }

        public UniTask NotifyLevelLoadedAsync(LevelContext level)
        {
            if ((!_booted && !_booting) || _context == null)
                return UniTask.CompletedTask;

            return _levels.NotifyLevelLoadedAsync(
                _context,
                _lifecycle.Features,
                level,
                _lifetimeCts.Token
            );
        }

        public UniTask NotifyLevelUnloadingAsync(LevelContext level)
        {
            if ((!_booted && !_booting) || _context == null)
                return UniTask.CompletedTask;

            return _levels.NotifyLevelUnloadingAsync(
                _context,
                _lifecycle.Features,
                level,
                _lifetimeCts.Token
            );
        }

        void IMainboardStateSink.SetPhase(MainboardPhase nextPhase, string step, float progress)
        {
            SetPhase(nextPhase, step, progress);
        }

        private MainboardContext CreateContext()
        {
            var signals = new SignalBus();
            var services = new ServiceRegistry();
            _disposables.Add(signals);
            return new MainboardContext(this, profile, signals, services, _lifetimeCts.Token);
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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            _isDisposed = true;
            _lifetimeCts?.Cancel();

            if (_booted || _booting)
                ForceSynchronousTeardown(!s_applicationQuitting && Application.isPlaying);

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
                _logger.Warning("Destroyed before graceful shutdown. Forcing synchronous teardown.");

            _lifecycle?.Clear();
            _context?.DisposeLevelScope();
            _context?.Services.Clear();
            _context = null;
            _booted = false;
            _booting = false;
        }
    }
}
