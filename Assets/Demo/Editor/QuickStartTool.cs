using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ModularDemo.Editor
{
    [InitializeOnLoad]
    public static class QuickStartTool
    {
        private const string EnabledPrefKey = "ModularDemo.QuickStart.Enabled";
        private const string LegacyEnabledPrefKey = "QuickStart_Enabled";
        private const string SettingsPath = "Assets/Demo/Editor/QuickStartSettings.asset";
        private const string FallbackLaunchScenePath = "Assets/Modules/DecalSystem/Samples/Scenes/Launch.unity";
        private const string ToggleMenu = "Tools/Quick Start/Play From Launch Scene";

        private static bool missingSceneWarningShown;

        static QuickStartTool()
        {
            MigrateLegacyPrefs();
            EditorApplication.delayCall += ApplyState;
        }

        [MenuItem(ToggleMenu)]
        public static void ToggleQuickStart()
        {
            var isEnabled = IsEnabled();
            EditorPrefs.SetBool(EnabledPrefKey, !isEnabled);
            missingSceneWarningShown = false;
            ApplyState();
        }

        [MenuItem(ToggleMenu, true)]
        public static bool ToggleQuickStartValidate()
        {
            Menu.SetChecked(ToggleMenu, IsEnabled());
            return true;
        }

        [MenuItem("Tools/Quick Start/Select Launch Scene...", false, 20)]
        public static void SelectLaunchScene()
        {
            var settings = GetOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Quick Start/Ping Launch Scene", false, 21)]
        public static void PingLaunchScene()
        {
            var scene = GetLaunchScene();
            if (scene == null)
            {
                EditorUtility.DisplayDialog("Quick Start", "Launch scene is not assigned.", "OK");
                return;
            }

            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
        }

        [MenuItem("Tools/Quick Start/Reset", false, 40)]
        public static void ResetQuickStart()
        {
            EditorPrefs.DeleteKey(EnabledPrefKey);
            EditorSceneManager.playModeStartScene = null;
            missingSceneWarningShown = false;
            Debug.Log("[QuickStart] Reset. Play will start from the current scene.");
        }

        private static void ApplyState()
        {
            if (!IsEnabled())
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var launchScene = GetLaunchScene();
            if (launchScene == null)
            {
                EditorSceneManager.playModeStartScene = null;
                ShowMissingSceneWarningOnce();
                return;
            }

            EditorSceneManager.playModeStartScene = launchScene;
        }

        private static bool IsEnabled()
        {
            return EditorPrefs.GetBool(EnabledPrefKey, false);
        }

        private static void MigrateLegacyPrefs()
        {
            if (!EditorPrefs.HasKey(LegacyEnabledPrefKey) || EditorPrefs.HasKey(EnabledPrefKey))
                return;

            EditorPrefs.SetBool(EnabledPrefKey, EditorPrefs.GetBool(LegacyEnabledPrefKey, false));
            EditorPrefs.DeleteKey(LegacyEnabledPrefKey);
        }

        private static SceneAsset GetLaunchScene()
        {
            var settings = GetOrCreateSettings();
            if (settings.LaunchScene != null)
                return settings.LaunchScene;

            var fallbackScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FallbackLaunchScenePath);
            if (fallbackScene == null)
                return null;

            settings.LaunchScene = fallbackScene;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return fallbackScene;
        }

        private static QuickStartSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<QuickStartSettings>(SettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<QuickStartSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void ShowMissingSceneWarningOnce()
        {
            if (missingSceneWarningShown)
                return;

            missingSceneWarningShown = true;
            Debug.LogWarning("[QuickStart] Launch scene is missing. Use Tools/Quick Start/Select Launch Scene... to assign it.");
        }
    }

    public sealed class QuickStartSettings : ScriptableObject
    {
        [SerializeField] private SceneAsset launchScene;

        public SceneAsset LaunchScene
        {
            get { return launchScene; }
            set { launchScene = value; }
        }
    }
}
