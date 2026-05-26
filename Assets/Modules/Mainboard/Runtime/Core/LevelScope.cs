using System;
using System.Threading;

namespace Mainboard.Runtime
{
    public sealed class LevelScope : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ServiceRegistry _parentServices;
        private bool _disposed;

        public LevelScope(string levelName, ServiceRegistry parentServices, CancellationToken parentToken)
        {
            LevelName = levelName;
            _parentServices = parentServices;
            Services = new ServiceRegistry();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        }

        public string LevelName { get; }
        public ServiceRegistry Services { get; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void RegisterService<T>(T service) where T : class
        {
            Services.Register(service);
        }

        public T Resolve<T>() where T : class
        {
            if (TryResolve(out T service))
                return service;

            throw new InvalidOperationException($"Service '{typeof(T).Name}' is not registered.");
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (Services.TryResolve(out service))
                return true;

            return _parentServices != null && _parentServices.TryResolve(out service);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            Services.Clear();
        }
    }
}
