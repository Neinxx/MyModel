using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mainboard.Runtime
{
    internal sealed class MainboardLifecycleRunner
    {
        private readonly List<IGameFeature> _features = new List<IGameFeature>();
        private readonly IMainboardStateSink _stateSink;
        private readonly MainboardLogger _logger;

        public MainboardLifecycleRunner(IMainboardStateSink stateSink, MainboardLogger logger)
        {
            _stateSink = stateSink;
            _logger = logger;
        }

        public IReadOnlyList<IGameFeature> Features => _features;

        public async UniTask BootAsync(
            MainboardContext context,
            CancellationToken cancellationToken
        )
        {
            _stateSink.SetPhase(MainboardPhase.Booting, "Booting mainboard", 0f);
            context.Signals.Publish(new MainboardBootStartedSignal(context.Board));

            InstallFeatures(context);
            await RunInstallPhaseAsync(context, cancellationToken);
            await RunBootPhaseAsync(context, cancellationToken);

            _stateSink.SetPhase(MainboardPhase.Running, "Running", 1f);
            context.Signals.Publish(new MainboardBootCompletedSignal(context.Board));
        }

        public async UniTask ShutdownAsync(MainboardContext context)
        {
            if (context == null)
                return;

            _stateSink.SetPhase(MainboardPhase.Shutdown, "Shutting down", 0f);
            context.Signals.Publish(new MainboardShutdownStartedSignal(context.Board));

            for (var i = _features.Count - 1; i >= 0; i--)
            {
                if (_features[i] is not IFeatureShutdown shutdown)
                    continue;

                try
                {
                    await shutdown.ShutdownAsync(context, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.Error($"Exception during Feature '{GetFeatureName(_features[i])}' shutdown: {exception}");
                }
            }

            context.DisposeLevelScope();
            context.Services.Clear();
            context.Signals.Publish(new MainboardShutdownCompletedSignal(context.Board));
            _features.Clear();
        }

        public void Clear()
        {
            _features.Clear();
        }

        private void InstallFeatures(MainboardContext context)
        {
            _features.Clear();

            if (context.Profile == null)
            {
                _logger.Warning("No profile assigned. Booting with an empty board.");
                return;
            }

            var installers = context.Profile.GetInstallers().ToList();
            _logger.Info($"Installing {installers.Count} feature(s) from Profile '{context.Profile.name}'...");

            foreach (var installer in installers)
            {
                if (installer == null)
                    continue;

                var feature = installer.CreateFeature();
                if (feature == null)
                    continue;

                _features.Add(feature);
                context.Signals.Publish(new MainboardModuleInstalledSignal(installer, feature));
            }
        }

        private async UniTask RunInstallPhaseAsync(
            MainboardContext context,
            CancellationToken cancellationToken
        )
        {
            var installers = _features.OfType<IFeatureInstaller>().ToList();
            for (var i = 0; i < installers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var installer = installers[i];
                _stateSink.SetPhase(
                    MainboardPhase.Booting,
                    $"Installing {GetFeatureName(installer)}",
                    installers.Count == 0 ? 1f : (float)i / installers.Count
                );

                await installer.InstallAsync(context, cancellationToken);
            }

            foreach (var feature in _features)
                context.Signals.Publish(new MainboardModuleReadySignal(feature));
        }

        private async UniTask RunBootPhaseAsync(
            MainboardContext context,
            CancellationToken cancellationToken
        )
        {
            var boots = _features.OfType<IFeatureBoot>().ToList();
            for (var i = 0; i < boots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var boot = boots[i];
                _stateSink.SetPhase(
                    MainboardPhase.Booting,
                    $"Booting {GetFeatureName(boot)}",
                    boots.Count == 0 ? 1f : (float)i / boots.Count
                );

                await boot.BootAsync(context, cancellationToken);
            }
        }

        public static string GetFeatureName(object capability)
        {
            return capability is IGameFeature feature ? feature.Name : capability.GetType().Name;
        }
    }
}
