using System;
using System.Collections.Generic;

namespace Mainboard.Runtime
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            _services[typeof(T)] = service;
        }

        public T Resolve<T>() where T : class
        {
            if (TryResolve(out T service))
                return service;

            throw new InvalidOperationException($"Service '{typeof(T).Name}' is not registered.");
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var value))
            {
                service = value as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        public void Clear()
        {
            _services.Clear();
        }
    }
}
