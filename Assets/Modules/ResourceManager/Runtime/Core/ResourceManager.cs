using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// 全局高性能、并发安全资源管理器 (Central Industrial-Grade Decoupled Resource Manager Engine)
    /// 负责多提供者分发、强缓存机制、异步加载挂起队列排合并及引用计数全生命周期监控。
    /// 完美防御并发加载竞态条件与多类型同 Key 转换隐患。
    /// </summary>
    public static class ResourceManager
    {
        private class CachedAssetEntry
        {
            public Object Asset;
            public int ReferenceCount;
            public IResourceProvider Provider;
        }

        private static readonly List<IResourceProvider> _providers = new List<IResourceProvider>();
        private static readonly Dictionary<string, CachedAssetEntry> _cache = new Dictionary<string, CachedAssetEntry>();
        
        // 🚀 核心升级：并发加载承诺锁追踪字典，合并同一帧发起的重复异步加载请求，彻底杜绝重复 IO
        private static readonly Dictionary<string, Task<object>> _loadingTasks = new Dictionary<string, Task<object>>();

        static ResourceManager()
        {
            // 静态构造器：默认自愈注册一个 ResourcesProvider 和 DirectRefProvider
            RegisterProvider(new DirectRefProvider());
            RegisterProvider(new AddressablesProvider());
            RegisterProvider(new ResourcesProvider());
        }

        /// <summary>
        /// 注册一个资源加载提供者 (例如 Addressables 运行时或 AB 包模块)
        /// </summary>
        public static void RegisterProvider(IResourceProvider provider)
        {
            if (provider == null) return;
            lock (_providers)
            {
                if (!_providers.Contains(provider))
                {
                    _providers.Add(provider);
                }
            }
        }

        /// <summary>
        /// 注销一个提供者
        /// </summary>
        public static void UnregisterProvider(IResourceProvider provider)
        {
            if (provider != null)
            {
                lock (_providers)
                {
                    _providers.Remove(provider);
                }
            }
        }

        /// <summary>
        /// 【异步核心】异步加载指定资源并返回引用计数句柄 (并发安全，合并加载)
        /// </summary>
        public static async Task<ResourceHandle<T>> LoadAsync<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Asset key cannot be null or empty.", nameof(key));
            }

            // 1. 检查高速缓存
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (entry.Asset == null)
                    {
                        _cache.Remove(key); // 自动清理失效的缓存
                    }
                    else
                    {
                        // 🚀 核心防呆：类型不匹配时抛出精确定位异常，杜绝静默返回空指针
                        if (!(entry.Asset is T typedAsset))
                        {
                            throw new InvalidCastException(
                                $"[ResourceManager] Type mismatch for asset key '{key}'. Cached asset type is '{entry.Asset.GetType().Name}', but requested type was '{typeof(T).Name}'."
                            );
                        }

                        entry.ReferenceCount++;
                        Debug.Log($"<color=#7C8CFF>[ResourceManager]</color> Cache Hit for '{key}'. Reference Count incremented to: {entry.ReferenceCount}");
                        return new ResourceHandle<T>(key, typedAsset);
                    }
                }
            }

            // 2. 🚀 并发请求合并拦截锁 (Task Coalescing)
            Task<object> ongoingTask;
            bool isOriginator = false;

            lock (_loadingTasks)
            {
                if (!_loadingTasks.TryGetValue(key, out ongoingTask))
                {
                    isOriginator = true;
                    ongoingTask = ExecutePhysicalLoadAsync(key);
                    _loadingTasks[key] = ongoingTask;
                }
                else
                {
                    Debug.Log($"<color=#7C8CFF>[ResourceManager]</color> Coalescing concurrent load request for key: '{key}'. Waiting for ongoing task...");
                }
            }

            try
            {
                // 等待真正的物理异步加载完成
                object rawAsset = await ongoingTask;
                T asset = rawAsset as T;

                if (asset == null)
                {
                    throw new NullReferenceException($"[ResourceManager] Loaded asset for key '{key}' is null or cannot be cast to '{typeof(T).Name}'.");
                }

                // 3. 只有发起加载的起源者或首先完成者，负责将结果刷入 Cache，其他人共享实例并安全累加计数
                lock (_cache)
                {
                    if (_cache.TryGetValue(key, out var existingEntry))
                    {
                        existingEntry.ReferenceCount++;
                        return new ResourceHandle<T>(key, existingEntry.Asset as T);
                    }
                    else
                    {
                        _cache[key] = new CachedAssetEntry
                        {
                            Asset = asset,
                            ReferenceCount = 1,
                            Provider = GetProviderForKey(key)
                        };
                        Debug.Log($"<color=#3FB950>[ResourceManager]</color> Successfully loaded and cached '{key}'. Initial Reference Count: 1");
                    }
                }

                return new ResourceHandle<T>(key, asset);
            }
            finally
            {
                // 4. 加载生命周期结束，擦除任务记录，确保下一次独立加载周期完备
                if (isOriginator)
                {
                    lock (_loadingTasks)
                    {
                        _loadingTasks.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        /// 封装底层提供者物理加载
        /// </summary>
        private static async Task<object> ExecutePhysicalLoadAsync(string key)
        {
            IResourceProvider matchedProvider = GetProviderForKey(key);
            Object asset = await matchedProvider.LoadAssetAsync<Object>(key);
            return asset;
        }

        private static IResourceProvider GetProviderForKey(string key)
        {
            lock (_providers)
            {
                foreach (var p in _providers)
                {
                    if (p.CanLoad(key))
                    {
                        return p;
                    }
                }
            }
            throw new KeyNotFoundException($"No registered Resource Provider is capable of loading key: '{key}'. Please check your config.");
        }

        /// <summary>
        /// 【传统回调兼容】提供 Action 回调形式的异步加载接口
        /// </summary>
        public static async void LoadAsync<T>(string key, Action<ResourceHandle<T>> callback) where T : Object
        {
            try
            {
                var handle = await LoadAsync<T>(key);
                callback?.Invoke(handle);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceManager] Failed to load asset '{key}' asynchronously: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        /// <summary>
        /// 释放指定资源的占用 (引用计数递减)。
        /// 通常由 ResourceHandle.Dispose() 自动调用，也可以手动调用。
        /// </summary>
        public static void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    entry.ReferenceCount--;
                    Debug.Log($"<color=#FFB443>[ResourceManager]</color> Released reference for '{key}'. Remaining Reference Count: {entry.ReferenceCount}");

                    if (entry.ReferenceCount <= 0)
                    {
                        // 引用计数归 0，触发物理垃圾卸载回收内存
                        _cache.Remove(key);
                        entry.Provider.UnloadAsset(key);
                        Debug.Log($"<color=#FF5252>[ResourceManager]</color> No active references. Decoupled provider '{entry.Provider.GetType().Name}' physics unloaded key: '{key}'.");
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前缓存中某 Key 的活跃引用计数值 (用于单元/集成测试断言)
        /// </summary>
        public static int GetDebugRefCount(string key)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    return entry.ReferenceCount;
                }
            }
            return 0;
        }

        /// <summary>
        /// 全量强制清理与内存爆破 (Teardown/Purge)
        /// 物理强制销毁所有缓存资源并重置，用于关卡硬重置、单元测试环境销毁。
        /// </summary>
        public static void ClearAll()
        {
            lock (_cache)
            {
                foreach (var pair in _cache)
                {
                    pair.Value.Provider.UnloadAsset(pair.Key);
                }
                _cache.Clear();
            }
            lock (_loadingTasks)
            {
                _loadingTasks.Clear();
            }
            Debug.Log("<color=#FF5252>[ResourceManager]</color> Full cache purged. All allocated assets unloaded physically.");
        }
    }
}
