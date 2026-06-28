using System.Threading.Tasks;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    public interface IResourceProvider
    {
        Task<T> LoadAssetAsync<T>(string key) where T : Object;

        void UnloadAsset(string key);

        bool CanLoad(string key);
    }
}
