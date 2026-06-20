using System.Threading;
using Cysharp.Threading.Tasks;
using ResourceManagerModule.Runtime;
using UnityEngine;

namespace Mainboard.Runtime.Integrations
{
    public interface IAssetService
    {
        UniTask<AssetHandle<T>> LoadAsync<T>(string key, CancellationToken cancellationToken)
            where T : Object;
    }

    public sealed class AssetHandle<T> : System.IDisposable where T : Object
    {
        private readonly System.IDisposable _releaseHandle;

        public AssetHandle(string key, T asset, System.IDisposable releaseHandle)
        {
            Key = key;
            Asset = asset;
            _releaseHandle = releaseHandle;
        }

        public string Key { get; }
        public T Asset { get; }

        public void Dispose()
        {
            _releaseHandle?.Dispose();
        }
    }

    internal sealed class ResourceManagerAssetService : IAssetService
    {
        public async UniTask<AssetHandle<T>> LoadAsync<T>(
            string key,
            CancellationToken cancellationToken
        ) where T : Object
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = await ResourceManager.LoadAsync<T>(key);
            cancellationToken.ThrowIfCancellationRequested();
            return new AssetHandle<T>(key, handle.Asset, handle);
        }
    }

    internal static class AssetServiceBootstrap
    {
        public static IAssetService Ensure(MainboardContext context)
        {
            if (context.TryResolve<IAssetService>(out var assetService))
                return assetService;

            assetService = new ResourceManagerAssetService();
            context.RegisterService(assetService);
            return assetService;
        }
    }
}
