using System.Threading;
using Cysharp.Threading.Tasks;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime
{
    public interface IWorldNavigator
    {
        UniTask<WorldNavigationResult> LoadLevelAsync(
            string levelName,
            CancellationToken cancellationToken
        );
    }

    internal sealed class WorldNavigator : IWorldNavigator
    {
        private readonly IWorldSceneDriver _driver;

        public WorldNavigator(IWorldSceneDriver driver)
        {
            _driver = driver;
        }

        public async UniTask<WorldNavigationResult> LoadLevelAsync(
            string levelName,
            CancellationToken cancellationToken
        )
        {
            if (_driver == null || string.IsNullOrWhiteSpace(levelName))
                return WorldNavigationResult.Failed("World scene driver or level name is not available.");

            var sceneResult = await _driver.LoadLevelAsync(levelName, cancellationToken);
            return sceneResult.Success
                ? WorldNavigationResult.Completed(sceneResult)
                : WorldNavigationResult.Failed(sceneResult.Error);
        }
    }
}
