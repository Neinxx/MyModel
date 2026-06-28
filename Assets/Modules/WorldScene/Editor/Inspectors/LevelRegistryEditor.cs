using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WorldSceneModule.Runtime;

namespace WorldSceneModule.Editor
{
    [CustomEditor(typeof(LevelRegistry))]
    public class LevelRegistryEditor : UnityEditor.Editor
    {
        private VisualElement _root;
        private Label _scenesStatusLabel;
        private Label _registeredStatusLabel;
        private Label _buildMissingStatusLabel;

        public override VisualElement CreateInspectorGUI()
        {
            var uxml = WorldScenePathUtility.LoadUXML("RegistryEditor");
            if (uxml == null)
                return new Label("UXML Missing");

            _root = uxml.Instantiate();
            var uss = WorldScenePathUtility.LoadUSS("RegistryEditor");
            if (uss != null)
                _root.styleSheets.Add(uss);

            _scenesStatusLabel = _root.Q<Label>("status-scenes");
            _registeredStatusLabel = _root.Q<Label>("status-registered");
            _buildMissingStatusLabel = _root.Q<Label>("status-build-missing");

            var syncBtn = _root.Q<Button>("sync-btn");
            if (syncBtn != null)
            {
                syncBtn.clicked += SmartSync;
            }

            RefreshStatus();
            return _root;
        }

        private void SmartSync()
        {
            var registry = target as LevelRegistry;
            if (registry == null || registry.scenesRootFolder == null)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Please assign a Scenes Root folder first.",
                    "OK"
                );
                return;
            }

            var syncPlan = BuildSyncPlan(registry);
            if (!ConfirmSync(syncPlan))
            {
                return;
            }

            ApplySyncPlan(registry, syncPlan);
            RefreshStatus();
        }

        public static void SyncRegistry(LevelRegistry registry)
        {
            if (registry == null || registry.scenesRootFolder == null) return;

            ApplySyncPlan(registry, BuildSyncPlan(registry));
        }

        public static WorldSceneRegistrySyncPlan BuildSyncPlan(LevelRegistry registry)
        {
            if (registry == null || registry.scenesRootFolder == null)
            {
                return WorldSceneRegistrySyncPlan.FromScenePaths(null);
            }

            string rootPath = AssetDatabase.GetAssetPath(registry.scenesRootFolder);
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { rootPath });
            var scenePaths = new List<string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    scenePaths.Add(path);
                }
            }

            return WorldSceneRegistrySyncPlan.FromScenePaths(scenePaths);
        }

        private static void ApplySyncPlan(LevelRegistry registry, WorldSceneRegistrySyncPlan syncPlan)
        {
            if (registry == null) return;

            registry.levels.Clear();
            if (registry.persistentWorldAsset != null)
            {
                registry.cachedPersistentWorldPath = AssetDatabase.GetAssetPath(registry.persistentWorldAsset);
            }

            foreach (var item in syncPlan.Items)
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(item.ScenePath);
                var moduleData = AssetDatabase.LoadAssetAtPath<LevelModuleData>(item.ModuleDataPath);
                if (moduleData == null)
                {
                    moduleData = ScriptableObject.CreateInstance<LevelModuleData>();
                    AssetDatabase.CreateAsset(moduleData, item.ModuleDataPath);
                }

                var decalData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(item.DecalDataPath);
                if (decalData != null)
                {
                    moduleData.RegisterSubData(decalData);
                    EditorUtility.SetDirty(moduleData);
                }

                registry.levels.Add(
                    new LevelConfig
                    {
                        levelName = item.LevelName,
                        sceneAsset = asset,
                        moduleData = moduleData,
                        cachedScenePath = item.ScenePath,
                    }
                );
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            WarnMissingBuildSettingsScenes(registry);
            Debug.Log($"[WorldScene] Synced registry. Found {registry.levels.Count} scenes.");
        }

        private static bool ConfirmSync(WorldSceneRegistrySyncPlan syncPlan)
        {
            int sceneCount = syncPlan.Items.Count;
            int newModuleDataCount = CountMissingModuleDataAssets(syncPlan);
            string message =
                $"Scenes found: {sceneCount}\n" +
                $"New LevelModuleData assets: {newModuleDataCount}\n\n" +
                "The registry entries will be rebuilt from the selected Scenes Root folder.";

            return EditorUtility.DisplayDialog(
                "Sync World Scene Registry",
                message,
                "Sync",
                "Cancel"
            );
        }

        private static int CountMissingModuleDataAssets(WorldSceneRegistrySyncPlan syncPlan)
        {
            int count = 0;
            foreach (var item in syncPlan.Items)
            {
                if (!string.IsNullOrEmpty(item.ModuleDataPath) &&
                    AssetDatabase.LoadAssetAtPath<LevelModuleData>(item.ModuleDataPath) == null)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshStatus()
        {
            var registry = target as LevelRegistry;
            var syncPlan = BuildSyncPlan(registry);
            int registeredCount = registry?.levels?.Count ?? 0;
            int buildMissingCount = registry == null
                ? 0
                : WorldSceneBuildSettingsValidator.FindMissingScenePaths(
                    registry,
                    WorldSceneBuildSettingsValidator.GetEnabledBuildScenePaths()
                ).Count;

            SetStatusLabel(_scenesStatusLabel, syncPlan.Items.Count.ToString(), false);
            SetStatusLabel(_registeredStatusLabel, registeredCount.ToString(), false);
            SetStatusLabel(_buildMissingStatusLabel, buildMissingCount.ToString(), buildMissingCount > 0);
        }

        private static void SetStatusLabel(Label label, string text, bool isWarning)
        {
            if (label == null)
            {
                return;
            }

            label.text = text;
            label.RemoveFromClassList("registry-status-ok");
            label.RemoveFromClassList("registry-status-warning");
            label.AddToClassList(isWarning ? "registry-status-warning" : "registry-status-ok");
        }

        private static void WarnMissingBuildSettingsScenes(LevelRegistry registry)
        {
            var missingPaths = WorldSceneBuildSettingsValidator.FindMissingScenePaths(
                registry,
                WorldSceneBuildSettingsValidator.GetEnabledBuildScenePaths()
            );
            if (missingPaths.Count == 0)
            {
                return;
            }

            Debug.LogWarning(
                $"[WorldScene] {missingPaths.Count} registered scenes are not enabled in Build Settings:\n" +
                string.Join("\n", missingPaths)
            );
        }
    }
}
