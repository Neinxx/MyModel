using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// Lightweight resource facade with provider dispatch, request coalescing, and reference-counted handles.
    /// </summary>
    public static class ResourceManager
    {
        private class CachedAssetEntry
        {
            public Object Asset;
            public int ReferenceCount;
            public IResourceProvider Provider;
        }

        private readonly struct LoadResult
        {
            public LoadResult(Object asset, IResourceProvider provider)
            {
                Asset = asset;
                Provider = provider;
            }

            public Object Asset { get; }
            public IResourceProvider Provider { get; }
        }

        private static readonly List<IResourceProvider> _providers = new List<IResourceProvider>();
        private static readonly Dictionary<string, CachedAssetEntry> _cache = new Dictionary<string, CachedAssetEntry>();
        private static readonly Dictionary<string, Task<LoadResult>> _loadingTasks = new Dictionary<string, Task<LoadResult>>();

        static ResourceManager()
        {
            RegisterProvider(new DirectRefProvider());
            RegisterProvider(new ResourcesProvider());
        }

        public static bool VerboseLogging { get; set; }
        public static bool EnableResourcesFallback { get; set; } = true;

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

        public static void RegisterProviderBefore<TFallback>(IResourceProvider provider)
            where TFallback : IResourceProvider
        {
            if (provider == null) return;

            lock (_providers)
            {
                if (_providers.Contains(provider))
                {
                    return;
                }

                int fallbackIndex = _providers.FindIndex(existingProvider => existingProvider is TFallback);
                if (fallbackIndex >= 0)
                {
                    _providers.Insert(fallbackIndex, provider);
                    return;
                }

                _providers.Add(provider);
            }
        }

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

        public static async Task<ResourceHandle<T>> LoadAsync<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Asset key cannot be null or empty.", nameof(key));
            }

            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (entry.Asset == null)
                    {
                        _cache.Remove(key);
                    }
                    else
                    {
                        if (!(entry.Asset is T typedAsset))
                        {
                            throw new InvalidCastException(
                                $"[ResourceManager] Type mismatch for asset key '{key}'. Cached asset type is '{entry.Asset.GetType().Name}', but requested type was '{typeof(T).Name}'."
                            );
                        }

                        entry.ReferenceCount++;
                        LogVerbose($"Cache hit for '{key}'. Ref count: {entry.ReferenceCount}");
                        return new ResourceHandle<T>(key, typedAsset);
                    }
                }
            }

            Task<LoadResult> ongoingTask;
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
                    LogVerbose($"Coalescing load request for '{key}'.");
                }
            }

            try
            {
                LoadResult result = await ongoingTask;
                T asset = result.Asset as T;

                if (asset == null)
                {
                    throw new NullReferenceException($"[ResourceManager] Loaded asset for key '{key}' is null or cannot be cast to '{typeof(T).Name}'.");
                }

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
                            Provider = result.Provider
                        };
                        LogVerbose($"Loaded and cached '{key}'. Ref count: 1");
                    }
                }

                return new ResourceHandle<T>(key, asset);
            }
            finally
            {
                if (isOriginator)
                {
                    lock (_loadingTasks)
                    {
                        _loadingTasks.Remove(key);
                    }
                }
            }
        }

        private static async Task<LoadResult> ExecutePhysicalLoadAsync(string key)
        {
            IResourceProvider matchedProvider = GetProviderForKey(key);
            Object asset = await matchedProvider.LoadAssetAsync<Object>(key);
            return new LoadResult(asset, matchedProvider);
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

        [Obsolete("Use LoadAsync<T>(string) and await the returned task.")]
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

        public static void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    entry.ReferenceCount--;
                    LogVerbose($"Released '{key}'. Ref count: {entry.ReferenceCount}");

                    if (entry.ReferenceCount <= 0)
                    {
                        _cache.Remove(key);
                        entry.Provider.UnloadAsset(key);
                        LogVerbose($"Unloaded '{key}' via {entry.Provider.GetType().Name}.");
                    }
                }
            }
        }

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
            LogVerbose("Cleared cached assets.");
        }

        private static void LogVerbose(string message)
        {
            if (VerboseLogging)
            {
                Debug.Log($"[ResourceManager] {message}");
            }
        }
    }
}
