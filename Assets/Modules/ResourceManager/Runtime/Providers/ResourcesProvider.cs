using System.Threading.Tasks;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    public class ResourcesProvider : IResourceProvider
    {
        public async Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            var request = Resources.LoadAsync<T>(key);
            var tcs = new TaskCompletionSource<T>();

            request.completed += op =>
            {
                var result = request.asset as T;
                if (result == null)
                {
                    Debug.LogWarning($"[ResourcesProvider] Resources.LoadAsync returned null for key: '{key}'.");
                }
                tcs.SetResult(result);
            };

            return await tcs.Task;
        }

        public void UnloadAsset(string key)
        {
        }

        public bool CanLoad(string key)
        {
            return ResourceManager.EnableResourcesFallback && !string.IsNullOrEmpty(key);
        }
    }
}
