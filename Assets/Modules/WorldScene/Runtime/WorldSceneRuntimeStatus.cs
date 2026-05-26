namespace WorldSceneModule.Runtime
{
    public readonly struct WorldSceneRuntimeStatus
    {
        public WorldSceneRuntimeStatus(
            WorldSceneState state,
            string currentStep,
            float loadingProgress,
            bool isLoading,
            string activeLevelName,
            bool hasRegistry
        )
        {
            State = state;
            CurrentStep = currentStep;
            LoadingProgress = loadingProgress;
            IsLoading = isLoading;
            ActiveLevelName = activeLevelName;
            HasRegistry = hasRegistry;
        }

        public WorldSceneState State { get; }
        public string CurrentStep { get; }
        public float LoadingProgress { get; }
        public bool IsLoading { get; }
        public string ActiveLevelName { get; }
        public bool HasRegistry { get; }

        public override string ToString()
        {
            string level = string.IsNullOrEmpty(ActiveLevelName) ? "<none>" : ActiveLevelName;
            return $"WorldScene(State={State}, Loading={IsLoading}, Step='{CurrentStep}', Progress={LoadingProgress:P0}, ActiveLevel='{level}', Registry={HasRegistry})";
        }
    }
}
