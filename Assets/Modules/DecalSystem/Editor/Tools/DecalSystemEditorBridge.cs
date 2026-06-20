using UnityEditor;

namespace DecalMini
{
    [InitializeOnLoad]
    public static class DecalSystemEditorBridge
    {
        private const double PreviewFrameInterval = 1.0 / 30.0;
        private static double _nextPreviewRepaintTime;

        static DecalSystemEditorBridge()
        {
            LoadSettings();

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            DecalEditorRuntimeBridge.SceneRepaintRequested -= RepaintSceneViews;
            DecalEditorRuntimeBridge.SceneRepaintRequested += RepaintSceneViews;
        }

        private static void OnEditorUpdate()
        {
            DecalParticlePoolMini.TickEditorSimulation();
            TickScenePreview();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                DecalSystemMini.ReleaseAll();
                DecalParticlePoolMini.ClearAll();
            }
        }

        private static void RepaintSceneViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static void TickScenePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.timeSinceStartup < _nextPreviewRepaintTime)
                return;
            if (!NeedsScenePreview())
                return;

            _nextPreviewRepaintTime = EditorApplication.timeSinceStartup + PreviewFrameInterval;
            RepaintSceneViews();
        }

        private static bool NeedsScenePreview()
        {
            if (DecalEditorRuntimeBridge.IsPreviewActive)
                return true;
            if (DecalParticlePoolMini.ActiveCount > 0)
                return true;

            return DecalProjectorMini.HasAnimatedPreviewProjectors;
        }

        private static void LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:DecalSystemSettings");
            if (guids.Length == 0)
                return;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<DecalSystemSettings>(path);
            DecalSystemSettings.SetInstance(settings);
        }
    }
}
