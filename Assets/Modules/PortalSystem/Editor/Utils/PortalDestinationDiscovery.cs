using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PortalSystem.Editor
{
    public static class PortalDestinationDiscovery
    {
        private const string DefaultSpawnId = "Start";

        public static List<string> GetLevelNames()
        {
            var names = new List<string> { string.Empty };
            var seen = new HashSet<string> { string.Empty };
            string[] guids = AssetDatabase.FindAssets("t:LevelRegistry");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                AddLevelNames(registry, names, seen);
            }

            return names;
        }

        public static List<string> GetSpawnIds(string levelName)
        {
            var spawnIds = new List<string> { DefaultSpawnId };
            var seen = new HashSet<string> { DefaultSpawnId };

            if (string.IsNullOrEmpty(levelName))
            {
                return spawnIds;
            }

            string scenePath = FindScenePath(levelName);
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                return spawnIds;
            }

            try
            {
                string content = File.ReadAllText(scenePath);
                MatchCollection matches = Regex.Matches(content, @"_hubID:\s*([^\s\r\n]+)");
                foreach (Match match in matches)
                {
                    if (match.Groups.Count <= 1)
                    {
                        continue;
                    }

                    string id = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(id) && seen.Add(id))
                    {
                        spawnIds.Add(id);
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PortalDestinationDiscovery] Failed to scan scene file: {exception.Message}");
            }

            return spawnIds;
        }

        private static void AddLevelNames(ScriptableObject registry, List<string> names, HashSet<string> seen)
        {
            if (registry == null)
            {
                return;
            }

            var serializedRegistry = new SerializedObject(registry);
            SerializedProperty levelsProperty = serializedRegistry.FindProperty("levels");
            if (levelsProperty == null || !levelsProperty.isArray)
            {
                return;
            }

            for (int i = 0; i < levelsProperty.arraySize; i++)
            {
                SerializedProperty element = levelsProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = element.FindPropertyRelative("levelName");
                if (nameProperty != null &&
                    !string.IsNullOrEmpty(nameProperty.stringValue) &&
                    seen.Add(nameProperty.stringValue))
                {
                    names.Add(nameProperty.stringValue);
                }
            }
        }

        private static string FindScenePath(string levelName)
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelRegistry");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                string scenePath = FindScenePath(registry, levelName);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    return scenePath;
                }
            }

            return string.Empty;
        }

        private static string FindScenePath(ScriptableObject registry, string levelName)
        {
            if (registry == null)
            {
                return string.Empty;
            }

            var serializedRegistry = new SerializedObject(registry);
            SerializedProperty levelsProperty = serializedRegistry.FindProperty("levels");
            if (levelsProperty == null || !levelsProperty.isArray)
            {
                return string.Empty;
            }

            for (int i = 0; i < levelsProperty.arraySize; i++)
            {
                SerializedProperty element = levelsProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = element.FindPropertyRelative("levelName");
                if (nameProperty == null || nameProperty.stringValue != levelName)
                {
                    continue;
                }

                SerializedProperty assetProperty = element.FindPropertyRelative("sceneAsset");
                if (assetProperty != null && assetProperty.objectReferenceValue != null)
                {
                    return AssetDatabase.GetAssetPath(assetProperty.objectReferenceValue);
                }
            }

            return string.Empty;
        }
    }
}
