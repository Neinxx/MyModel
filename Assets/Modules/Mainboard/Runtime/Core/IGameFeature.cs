using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mainboard.Runtime
{
    public interface IGameFeature
    {
        string Name { get; }
    }

    public interface IFeatureInstaller
    {
        UniTask InstallAsync(MainboardContext context, CancellationToken cancellationToken);
    }

    public interface IFeatureBoot
    {
        UniTask BootAsync(MainboardContext context, CancellationToken cancellationToken);
    }

    public interface ILevelUnloadHandler
    {
        UniTask OnLevelUnloadingAsync(LevelContext level, CancellationToken cancellationToken);
    }

    public interface ILevelLoadHandler
    {
        UniTask OnLevelLoadedAsync(LevelContext level, CancellationToken cancellationToken);
    }

    public interface IFeatureShutdown
    {
        UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken);
    }
}
