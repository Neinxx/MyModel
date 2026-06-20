using System.Threading;
using Cysharp.Threading.Tasks;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime
{
    internal sealed class WorldBootSequence
    {
        private readonly WorldRuntimeConfig _config;
        private readonly IWorldSceneDriver _driver;
        private readonly IWorldNavigator _navigator;

        public WorldBootSequence(
            WorldRuntimeConfig config,
            IWorldSceneDriver driver,
            IWorldNavigator navigator
        )
        {
            _config = config;
            _driver = driver;
            _navigator = navigator;
        }

        public async UniTask RunAsync(
            MainboardContext context,
            CancellationToken cancellationToken
        )
        {
            if (_driver == null)
                return;

            if (_config.initializePersistentWorld)
                await _driver.InitializeWorldAsync(cancellationToken);

            await RunBootModeAsync(context, cancellationToken);
        }

        private async UniTask RunBootModeAsync(
            MainboardContext context,
            CancellationToken cancellationToken
        )
        {
            switch (_config.bootMode)
            {
                case WorldBootMode.UseWorldSceneDefaults:
                    await LoadDefaultConfiguredLevelAsync(context, cancellationToken);
                    break;
                case WorldBootMode.LoadExplicitLevel:
                    await _navigator.LoadLevelAsync(_config.explicitBootLevel, cancellationToken);
                    break;
                case WorldBootMode.ShowStartUI:
                    RequestStartUI(context);
                    break;
                case WorldBootMode.DoNothing:
                    break;
            }
        }

        private async UniTask LoadDefaultConfiguredLevelAsync(
            MainboardContext context,
            CancellationToken cancellationToken
        )
        {
            string targetLevel = !string.IsNullOrEmpty(_config.defaultBootLevel)
                ? _config.defaultBootLevel
                : _driver.Registry?.defaultSubLevel;

            if (!string.IsNullOrEmpty(targetLevel))
            {
                await _navigator.LoadLevelAsync(targetLevel, cancellationToken);
                return;
            }

            RequestStartUI(context);
        }

        private void RequestStartUI(MainboardContext context)
        {
            if (!string.IsNullOrEmpty(_config.startUIViewID))
                context.Signals.Publish(new WorldStartUIRequestedSignal(_config.startUIViewID));
        }
    }
}
