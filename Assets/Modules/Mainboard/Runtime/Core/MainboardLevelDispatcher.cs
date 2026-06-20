using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mainboard.Runtime
{
    internal sealed class MainboardLevelDispatcher
    {
        private readonly IMainboardStateSink _stateSink;
        private readonly MainboardLogger _logger;

        public MainboardLevelDispatcher(IMainboardStateSink stateSink, MainboardLogger logger)
        {
            _stateSink = stateSink;
            _logger = logger;
        }

        public async UniTask NotifyLevelLoadedAsync(
            MainboardContext context,
            System.Collections.Generic.IEnumerable<IGameFeature> features,
            LevelContext level,
            CancellationToken cancellationToken
        )
        {
            var scope = context.BeginLevelScope(level.LevelName);
            var scopedLevel = new LevelContext(level.LevelName, level.Scene, scope, level.Config);
            var handlers = features.OfType<ILevelLoadHandler>().ToList();

            _stateSink.SetPhase(MainboardPhase.LevelLoading, $"Initializing level {scopedLevel.LevelName}", 0f);
            _logger.Info($"Broadcasting OnLevelLoaded to {handlers.Count} features for level: {scopedLevel.LevelName}...");

            for (var i = 0; i < handlers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var handler = handlers[i];
                _stateSink.SetPhase(
                    MainboardPhase.LevelLoading,
                    $"{MainboardLifecycleRunner.GetFeatureName(handler)}: {scopedLevel.LevelName}",
                    handlers.Count == 0 ? 1f : (float)i / handlers.Count
                );

                await handler.OnLevelLoadedAsync(scopedLevel, scope.CancellationToken);
            }

            context.Signals.Publish(new MainboardLevelLoadedSignal(scopedLevel));
            _stateSink.SetPhase(MainboardPhase.Running, "Running", 1f);
        }

        public async UniTask NotifyLevelUnloadingAsync(
            MainboardContext context,
            System.Collections.Generic.IEnumerable<IGameFeature> features,
            LevelContext level,
            CancellationToken cancellationToken
        )
        {
            var scope = context.CurrentLevelScope;
            var scopedLevel = new LevelContext(level.LevelName, level.Scene, scope, level.Config);
            var handlers = features.OfType<ILevelUnloadHandler>().Reverse().ToList();

            _stateSink.SetPhase(MainboardPhase.LevelUnloading, $"Unloading level {scopedLevel.LevelName}", 0f);
            _logger.Info($"Broadcasting OnLevelUnloading to {handlers.Count} features for level: {scopedLevel.LevelName}...");

            for (var i = 0; i < handlers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var handler = handlers[i];
                _stateSink.SetPhase(
                    MainboardPhase.LevelUnloading,
                    $"{MainboardLifecycleRunner.GetFeatureName(handler)}: {scopedLevel.LevelName}",
                    handlers.Count == 0 ? 1f : (float)i / handlers.Count
                );

                await handler.OnLevelUnloadingAsync(scopedLevel, cancellationToken);
            }

            context.Signals.Publish(new MainboardLevelUnloadingSignal(scopedLevel));
            context.DisposeLevelScope();
        }
    }
}
