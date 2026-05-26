using System.Threading.Tasks;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// Resources 异步加载提供者 (Resources Async Provider)
    /// 巧妙采用 TaskCompletionSource 将 Unity 经典的 AsyncOperation 回调机制桥接转换成现代的 C# Task/async/await 模式。
    /// </summary>
    public class ResourcesProvider : IResourceProvider
    {
        public async Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            Debug.Log($"<color=#B0B0B0>[ResourcesProvider]</color> Fallback: attempting Resources.LoadAsync<{typeof(T).Name}> for key: '{key}'. " +
                      "If this was not intended, ensure the asset is registered in DirectRefProvider or Addressables.");

            var request = Resources.LoadAsync<T>(key);
            var tcs = new TaskCompletionSource<T>();

            request.completed += op =>
            {
                var result = request.asset as T;
                if (result == null)
                {
                    Debug.LogWarning($"[ResourcesProvider] Resources.LoadAsync returned null for key: '{key}'. " +
                                     "Ensure the asset exists under a 'Resources/' folder with the exact path, or register it via DirectRefProvider.");
                }
                else
                {
                    Debug.Log($"<color=#B0B0B0>[ResourcesProvider]</color> Successfully loaded '{key}' from Resources.");
                }
                tcs.SetResult(result);
            };

            return await tcs.Task;
        }

        public void UnloadAsset(string key)
        {
            // 由于 Unity Resources 的限制，GameObject 预制件本身不能使用 Resources.UnloadAsset 释放，
            // 此时交由 Unity 底层进行常规的 GC 与资源自动回收，或者也可以按需调用 Resources.UnloadUnusedAssets()。
        }

        public bool CanLoad(string key)
        {
            // Resources 引擎默认充当全能性 Fallback 提供者，接管其他高优先级 Provider 不处理的所有 Key
            return true;
        }
    }
}
