using System.Threading.Tasks;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// 资源加载提供者接口 (Resource Provider Contract)
    /// 用于抽象不同的底层加载技术（如 Resources, Addressables, Direct References）。
    /// </summary>
    public interface IResourceProvider
    {
        /// <summary>
        /// 异步加载指定键值的资源
        /// </summary>
        Task<T> LoadAssetAsync<T>(string key) where T : Object;

        /// <summary>
        /// 卸载/物理释放指定键值的资源
        /// </summary>
        void UnloadAsset(string key);

        /// <summary>
        /// 检查当前提供者是否支持加载该 Key
        /// </summary>
        bool CanLoad(string key);
    }
}
