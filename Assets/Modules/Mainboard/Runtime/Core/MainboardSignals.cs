using System;

namespace Mainboard.Runtime
{
    public readonly struct MainboardBootStartedSignal
    {
        public MainboardBootStartedSignal(GameMainboard board) => Board = board;
        public GameMainboard Board { get; }
    }

    public readonly struct MainboardBootCompletedSignal
    {
        public MainboardBootCompletedSignal(GameMainboard board) => Board = board;
        public GameMainboard Board { get; }
    }

    public readonly struct MainboardShutdownStartedSignal
    {
        public MainboardShutdownStartedSignal(GameMainboard board) => Board = board;
        public GameMainboard Board { get; }
    }

    public readonly struct MainboardShutdownCompletedSignal
    {
        public MainboardShutdownCompletedSignal(GameMainboard board) => Board = board;
        public GameMainboard Board { get; }
    }

    public readonly struct MainboardFaultedSignal
    {
        public MainboardFaultedSignal(GameMainboard board, Exception exception)
        {
            Board = board;
            Exception = exception;
        }

        public GameMainboard Board { get; }
        public Exception Exception { get; }
    }

    public readonly struct MainboardPhaseChangedSignal
    {
        public MainboardPhaseChangedSignal(MainboardPhase phase, string step, float progress)
        {
            Phase = phase;
            Step = step;
            Progress = progress;
        }

        public MainboardPhase Phase { get; }
        public string Step { get; }
        public float Progress { get; }
    }

    public readonly struct MainboardModuleInstalledSignal
    {
        public MainboardModuleInstalledSignal(MainboardInstaller installer, IGameFeature feature)
        {
            Installer = installer;
            Feature = feature;
        }

        public MainboardInstaller Installer { get; }
        public IGameFeature Feature { get; }
    }

    public readonly struct MainboardModuleReadySignal
    {
        public MainboardModuleReadySignal(IGameFeature feature) => Feature = feature;
        public IGameFeature Feature { get; }
    }

    public readonly struct MainboardLevelLoadedSignal
    {
        public MainboardLevelLoadedSignal(LevelContext level) => Level = level;
        public LevelContext Level { get; }
    }

    public readonly struct MainboardLevelUnloadingSignal
    {
        public MainboardLevelUnloadingSignal(LevelContext level) => Level = level;
        public LevelContext Level { get; }
    }

    public readonly struct WorldStartUIRequestedSignal
    {
        public WorldStartUIRequestedSignal(string viewID) => ViewID = viewID;
        public string ViewID { get; }
    }
}
