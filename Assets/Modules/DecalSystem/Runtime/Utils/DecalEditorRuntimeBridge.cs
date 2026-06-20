using System;

namespace DecalMini
{
    public static class DecalEditorRuntimeBridge
    {
        public static event Action SceneRepaintRequested;
        private static float _previewActiveUntil;

        public static bool IsPreviewActive => _previewActiveUntil > UnityEngine.Time.realtimeSinceStartup;

        public static void RequestSceneRepaint()
        {
            _previewActiveUntil = UnityEngine.Time.realtimeSinceStartup + 0.5f;
            SceneRepaintRequested?.Invoke();
        }
    }
}
