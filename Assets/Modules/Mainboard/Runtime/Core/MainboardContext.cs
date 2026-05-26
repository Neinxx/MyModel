using System.Threading;

namespace Mainboard.Runtime
{
    public sealed class MainboardContext
    {
        public MainboardContext(
            GameMainboard board,
            MainboardProfile profile,
            SignalBus signals,
            ServiceRegistry services,
            CancellationToken cancellationToken
        )
        {
            Board = board;
            Profile = profile;
            Signals = signals;
            Services = services;
            CancellationToken = cancellationToken;
        }

        public GameMainboard Board { get; }
        public MainboardProfile Profile { get; }
        public SignalBus Signals { get; }
        public ServiceRegistry Services { get; }
        public CancellationToken CancellationToken { get; }
        public LevelScope CurrentLevelScope { get; private set; }

        public void RegisterService<T>(T service) where T : class
        {
            Services.Register(service);
        }

        public void RegisterLevelService<T>(T service) where T : class
        {
            if (CurrentLevelScope == null)
                throw new System.InvalidOperationException("No active level scope is available.");

            CurrentLevelScope.RegisterService(service);
        }

        public T Resolve<T>() where T : class
        {
            return Services.Resolve<T>();
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (CurrentLevelScope != null && CurrentLevelScope.TryResolve(out service))
                return true;

            return Services.TryResolve(out service);
        }

        public bool TryResolveLevelService<T>(out T service) where T : class
        {
            if (CurrentLevelScope != null)
                return CurrentLevelScope.Services.TryResolve(out service);

            service = null;
            return false;
        }

        public LevelScope BeginLevelScope(string levelName)
        {
            DisposeLevelScope();
            CurrentLevelScope = new LevelScope(levelName, Services, CancellationToken);
            return CurrentLevelScope;
        }

        public void DisposeLevelScope()
        {
            CurrentLevelScope?.Dispose();
            CurrentLevelScope = null;
        }
    }
}
