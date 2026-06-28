using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace ResourceManagerModule.Runtime
{
    public class AddressablesProvider : IResourceProvider
    {
        private const string DefaultCatalogResourcePath = "AddressablesResourceCatalog";

        private static AddressablesProvider _defaultProvider;

        private readonly Dictionary<string, AsyncOperationHandle> _activeHandles = new Dictionary<string, AsyncOperationHandle>();
        private readonly HashSet<string> _knownKeys = new HashSet<string>();
        private readonly HashSet<string> _unknownKeys = new HashSet<string>();

        private readonly object _initializeLock = new object();
        private AddressablesResourceCatalog _catalog;
        private Task _initializeTask;
        private bool _isInitialized;

        public AddressablesProvider()
            : this(null)
        {
        }

        public AddressablesProvider(AddressablesResourceCatalog catalog)
        {
            _catalog = catalog;
            UseCatalogAsAuthority = catalog != null;
        }

        public bool UseCatalogAsAuthority { get; set; }

        public static AddressablesProvider RegisterDefault()
        {
            if (_defaultProvider == null)
            {
                _defaultProvider = new AddressablesProvider(LoadDefaultCatalog());
            }
            else if (_defaultProvider._catalog == null)
            {
                _defaultProvider.SetCatalog(LoadDefaultCatalog());
            }

            ResourceManager.RegisterProviderBefore<ResourcesProvider>(_defaultProvider);
            return _defaultProvider;
        }

        public void SetCatalog(AddressablesResourceCatalog catalog)
        {
            _catalog = catalog;
            UseCatalogAsAuthority = catalog != null;
            _knownKeys.Clear();
            _unknownKeys.Clear();
        }

        public Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return Task.CompletedTask;
            }

            lock (_initializeLock)
            {
                if (_initializeTask == null)
                {
                    _initializeTask = InitializeInternalAsync();
                }

                return _initializeTask;
            }
        }

        public async Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            }

            await InitializeAsync();

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);

            lock (_activeHandles)
            {
                _activeHandles[key] = handle;
            }

            try
            {
                return await handle.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesProvider] Exception occurred while loading Addressable key '{key}': {ex.Message}");
                lock (_activeHandles)
                {
                    _activeHandles.Remove(key);
                }

                if (handle.IsValid())
                {
                    Addressables.Release(handle);
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
                    }
                }
            }
        }

        public bool CanLoad(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (_knownKeys.Contains(key)) return true;

            if (_unknownKeys.Contains(key)) return false;

            if (_catalog != null)
            {
                if (_catalog.Contains(key))
                {
                    _knownKeys.Add(key);
                    return true;
                }

                if (UseCatalogAsAuthority)
                {
                    _unknownKeys.Add(key);
                    return false;
                }
            }

            try
            {
                if (!TryLocate(key, out var locations))
                {
                    if (_isInitialized)
                    {
                        _unknownKeys.Add(key);
                    }

                    return false;
                }

                bool canLoad = locations != null && locations.Count > 0;
                if (canLoad)
                {
                    _knownKeys.Add(key);
                    return true;
                }

                if (_isInitialized)
                {
                    _unknownKeys.Add(key);
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesProvider] CanLoad check failed for key '{key}': {ex.Message}");
                return false;
            }
        }

        private async Task InitializeInternalAsync()
        {
            try
            {
                AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync();
                await handle.Task;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                lock (_initializeLock)
                {
                    _initializeTask = null;
                }

                throw new InvalidOperationException($"[AddressablesProvider] Initialization failed: {ex.Message}", ex);
            }
        }

        private bool TryLocate(string key, out IList<IResourceLocation> locations)
        {
            locations = null;

            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(key, typeof(Object), out locations) && locations != null && locations.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static AddressablesResourceCatalog LoadDefaultCatalog()
        {
            return Resources.Load<AddressablesResourceCatalog>(DefaultCatalogResourcePath);
        }
    }
}
