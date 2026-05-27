using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldSceneModule.Runtime
{
    [System.Serializable]
    public struct LevelConfig
    {
        [Tooltip("The display name of the level.")]
        public string levelName;

        [Tooltip("Direct reference to the Scene Asset.")]
        public Object sceneAsset;

        [Tooltip("Optional: Level-specific aggregated module data.")]
        public LevelModuleData moduleData;

        [HideInInspector]
        public string cachedScenePath;
    }

    [CreateAssetMenu(fileName = "LevelRegistry", menuName = "World Scene/Level Registry")]
    public class LevelRegistry : ScriptableObject
    {
        [Header("Discovery Settings")]
        public Object scenesRootFolder;
        public Object persistentWorldAsset;
        public Object launcherSceneAsset;

        [Tooltip(
            "The name of the level to automatically load after the Persistent World boots (leave empty to show UI)."
        )]
        public string defaultSubLevel = "Level1";

        [Header("Registered Levels")]
        public List<LevelConfig> levels = new List<LevelConfig>();

        [HideInInspector]
        public string cachedPersistentWorldPath;

        public LevelConfig GetConfig(string sceneName)
        {
            return levels.Find(l => l.levelName == sceneName);
        }

        public string GetPersistentWorldPath()
        {
#if UNITY_EDITOR
            if (persistentWorldAsset != null)
                return AssetDatabase.GetAssetPath(persistentWorldAsset);
#endif
            return !string.IsNullOrEmpty(cachedPersistentWorldPath)
                ? cachedPersistentWorldPath
                : (persistentWorldAsset != null ? persistentWorldAsset.name : "");
        }

        public string GetScenePath(string sceneName)
        {
            var config = GetConfig(sceneName);
#if UNITY_EDITOR
            if (config.sceneAsset != null)
                return AssetDatabase.GetAssetPath(config.sceneAsset);
#endif
            return !string.IsNullOrEmpty(config.cachedScenePath) 
                ? config.cachedScenePath 
                : config.levelName;
        }
    }
}
