using System;
using Object = UnityEngine.Object;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// 引用计数安全的资源句柄 (Reference-Counted Resource Handle)
    /// 包装了加载出来的底层资源，支持 C# standard using 块自动析构。
    /// </summary>
    public class ResourceHandle<T> : IDisposable where T : Object
    {
        private readonly string _key;
        private readonly T _asset;
        private bool _isDisposed;

        public string Key => _key;

        /// <summary>
        /// 底层实际资源。如果句柄已被释放，获取时将抛出异常保护内存安全。
        /// </summary>
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

            // 回收机制：通知 ResourceManager 释放对该 Key 的一次引用占用
            ResourceManager.Release(_key);
        }
    }
}
