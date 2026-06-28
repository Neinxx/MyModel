using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    public class DirectRefProvider : IResourceProvider
    {
        private static readonly Dictionary<string, Object> _assetsMap = new Dictionary<string, Object>();

        public static void RegisterAsset(string key, Object asset)
        {
            if (string.IsNullOrEmpty(key) || asset == null) return;
            lock (_assetsMap)
            {
                _assetsMap[key] = asset;
            }
        }

        public static void UnregisterAsset(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (_assetsMap)
            {
                _assetsMap.Remove(key);
            }
        }

        public static void Clear()
        {
            lock (_assetsMap)
            {
                _assetsMap.Clear();
            }
        }

        public static void InvalidateKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (_assetsMap)
            {
                _assetsMap.Remove(key);
            }
        }

        public Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            lock (_assetsMap)
            {
                if (_assetsMap.TryGetValue(key, out var asset))
                {
                    return Task.FromResult(asset as T);
                }
            }

            return Task.FromResult<T>(null);
        }

        public void UnloadAsset(string key)
        {
        }

        public bool CanLoad(string key)
        {
            lock (_assetsMap)
            {
                return _assetsMap.ContainsKey(key);
            }
        }
    }
}
