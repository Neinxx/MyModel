using System;
using Object = UnityEngine.Object;

namespace ResourceManagerModule.Runtime
{
    public class ResourceHandle<T> : IDisposable where T : Object
    {
        private readonly string _key;
        private readonly T _asset;
        private bool _isDisposed;

        public string Key => _key;

        public T Asset
        {
            get
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(ResourceHandle<T>), $"Handle for asset '{_key}' has already been disposed and released from memory.");
                }
                return _asset;
            }
        }

        public ResourceHandle(string key, T asset)
        {
            _key = key;
            _asset = asset;
            _isDisposed = false;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            ResourceManager.Release(_key);
        }
    }
}
