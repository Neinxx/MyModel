using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// Addressables 异步加载提供者 (Addressables Async Provider)
    /// 高效桥接 Unity 官方 Addressables 寻址加载管线，支持物理依赖解析与自动化引用计数解耦释放。
    /// </summary>
    public class AddressablesProvider : IResourceProvider
    {
        // 🚀 记录活跃的加载句柄，以便 UnloadAsset 时物理调用 Addressables.Release 递减引用计数
        private readonly Dictionary<string, AsyncOperationHandle> _activeHandles = new Dictionary<string, AsyncOperationHandle>();

        // 🚀 性能优化：缓存已探测为「可加载」的 key，避免每次 CanLoad 都全量遍历所有 ResourceLocators
        private readonly HashSet<string> _knownKeys = new HashSet<string>();
        // 缓存已探测为「不可加载」的 key，快速拒绝，避免重复探测
        private readonly HashSet<string> _unknownKeys = new HashSet<string>();

        private bool _isInitialized = false;

        /// <summary>
        /// 确保 Addressables 系统已完成底层初始化并成功加载目录定位器
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            try
            {
                var handle = Addressables.InitializeAsync();
                if (!handle.IsDone)
                {
                    handle.WaitForCompletion();
                }
                _isInitialized = true;
                Debug.Log("<color=#3FB950>[AddressablesProvider]</color> Addressables initialized synchronously via WaitForCompletion.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesProvider] Synchronous initialization failed, falling back: {ex.Message}");
            }
        }

        public async Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            }

            // 🚀 强制在加载前执行自愈初始化
            EnsureInitialized();

            // 发起 Addressables 异步加载
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);

            lock (_activeHandles)
            {
                _activeHandles[key] = handle;
            }

            try
            {
                // 桥接转换为现代化 Task 异步等待
                return await handle.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesProvider] Exception occurred while loading Addressable key '{key}': {ex.Message}");
                lock (_activeHandles)
                {
                    _activeHandles.Remove(key);
                }
                throw;
            }
        }

        public void UnloadAsset(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            lock (_activeHandles)
            {
                if (_activeHandles.TryGetValue(key, out var handle))
                {
                    _activeHandles.Remove(key);
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                        Debug.Log($"<color=#FF5252>[AddressablesProvider]</color> Physics unloaded Addressable handle for key: '{key}'");
                    }
                }
            }
        }

        public bool CanLoad(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            // 🚀 第一道快速路：已知可加载，直接命中
            if (_knownKeys.Contains(key)) return true;

            // 🚀 第二道快速路：已知不可加载，直接拒绝
            if (_unknownKeys.Contains(key)) return false;

            // 🚀 强制在检查可用性前执行自愈初始化，确保定位器目录已被读入内存
            EnsureInitialized();

            try
            {
                // 🚀 使用官方 API 智能检测 key 在当前 Addressables 系统中是否存在对应的 Resource Location
                var handle = Addressables.LoadResourceLocationsAsync(key);
                var locations = handle.IsDone ? handle.Result : handle.WaitForCompletion();

                bool canLoad = locations != null && locations.Count > 0;
                Addressables.Release(handle);

                if (canLoad)
                {
                    _knownKeys.Add(key); // 缓存命中结果，下次 O(1) 返回
                    return true;
                }
                else
                {
                    bool hasLocators = false;
                    if (Addressables.ResourceLocators != null)
                    {
                        foreach (var _ in Addressables.ResourceLocators)
                        {
                            hasLocators = true;
                            break;
                        }
                    }

                    if (hasLocators)
                    {
                        _unknownKeys.Add(key); // 缓存未命中结果
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesProvider] CanLoad check failed for key '{key}': {ex.Message}");
                return false;
            }
        }
    }
}
