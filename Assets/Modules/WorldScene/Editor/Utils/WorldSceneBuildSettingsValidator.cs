using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WorldSceneModule.Runtime;

namespace WorldSceneModule.Editor
{
    public static class WorldSceneBuildSettingsValidator
    {
        private const string RegistrySearchFilter = "t:LevelRegistry";

        [MenuItem("Tools/World Scene/Add Missing Scenes To Build Settings")]
        public static void AddMissingScenesToBuildSettings()
        {
            var registry = FindFirstRegistryAsset();
            if (registry == null)
            {
                Debug.LogWarning("[WorldScene] LevelRegistry asset was not found.");
                return;
            }

            var missingPaths = FindMissingScenePaths(registry, GetEnabledBuildScenePaths());
            int addedCount = AddScenePathsToBuildSettings(missingPaths);
            if (addedCount == 0)
            {
                Debug.Log("[WorldScene] Build Settings already contain all registered scenes.");
                return;
            }

            Debug.Log($"[WorldScene] Added {addedCount} scenes to Build Settings.");
        }

        public static List<string> FindMissingScenePaths(
            LevelRegistry registry,
            IReadOnlyCollection<string> enabledBuildScenePaths
        )
        {
            var missingPaths = new List<string>();
            if (registry == null || enabledBuildScenePaths == null)
            {
                return missingPaths;
            }

            AddMissingPath(registry.GetPersistentWorldPath(), enabledBuildScenePaths, missingPaths);

            foreach (var level in registry.levels)
            {
                AddMissingPath(level.cachedScenePath, enabledBuildScenePaths, missingPaths);
            }

            return missingPaths;
        }

        public static int AddScenePathsToBuildSettings(IReadOnlyList<string> scenePaths)
        {
            if (scenePaths == null || scenePaths.Count == 0)
            {
                return 0;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int addedCount = 0;
            foreach (var path in scenePaths)
            {
                if (string.IsNullOrEmpty(path) || ContainsBuildScene(scenes, path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
                addedCount++;
            }

            if (addedCount > 0)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            return addedCount;
        }

        public static List<string> GetEnabledBuildScenePaths()
        {
            var paths = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            return paths;
        }

        private static LevelRegistry FindFirstRegistryAsset()
        {
            string[] guids = AssetDatabase.FindAssets(RegistrySearchFilter);
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            System.Array.Sort(guids, System.StringComparer.Ordinal);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(path);
                if (registry != null)
                {
                    return registry;
                }
            }

            return null;
        }

        private static void AddMissingPath(
            string path,
            IReadOnlyCollection<string> enabledBuildScenePaths,
            List<string> missingPaths
        )
        {
            if (string.IsNullOrEmpty(path) || ContainsPath(enabledBuildScenePaths, path) || missingPaths.Contains(path))
            {
                return;
            }

            missingPaths.Add(path);
        }

        private static bool ContainsPath(IReadOnlyCollection<string> paths, string targetPath)
        {
            foreach (var path in paths)
            {
                if (path == targetPath)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBuildScene(IReadOnlyList<EditorBuildSettingsScene> scenes, string targetPath)
        {
            foreach (var scene in scenes)
            {
                if (scene != null && scene.path == targetPath)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
