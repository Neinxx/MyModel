using System.Collections.Generic;
using System.IO;

namespace WorldSceneModule.Editor
{
    public readonly struct WorldSceneRegistrySyncItem
    {
        public WorldSceneRegistrySyncItem(string scenePath)
        {
            ScenePath = scenePath;
            LevelName = string.IsNullOrEmpty(scenePath)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(scenePath);

            string sceneDirectory = Path.GetDirectoryName(scenePath);
            sceneDirectory = string.IsNullOrEmpty(sceneDirectory)
                ? string.Empty
                : sceneDirectory.Replace("\\", "/");

            ModuleDataPath = string.IsNullOrEmpty(sceneDirectory) || string.IsNullOrEmpty(LevelName)
                ? string.Empty
                : $"{sceneDirectory}/{LevelName}_LevelModuleData.asset";
            DecalDataPath = string.IsNullOrEmpty(sceneDirectory) || string.IsNullOrEmpty(LevelName)
                ? string.Empty
                : $"{sceneDirectory}/{LevelName}_DecalLevelData.asset";
        }

        public string ScenePath { get; }
        public string LevelName { get; }
        public string ModuleDataPath { get; }
        public string DecalDataPath { get; }
    }

    public sealed class WorldSceneRegistrySyncPlan
    {
        private readonly List<WorldSceneRegistrySyncItem> _items;

        private WorldSceneRegistrySyncPlan(List<WorldSceneRegistrySyncItem> items)
        {
            _items = items;
        }

        public IReadOnlyList<WorldSceneRegistrySyncItem> Items => _items;

        public static WorldSceneRegistrySyncPlan FromScenePaths(IEnumerable<string> scenePaths)
        {
            var sortedScenePaths = new List<string>();
            if (scenePaths != null)
            {
                foreach (var path in scenePaths)
                {
                    if (string.IsNullOrEmpty(path) || sortedScenePaths.Contains(path))
                    {
                        continue;
                    }

                    sortedScenePaths.Add(path);
                }
            }

            sortedScenePaths.Sort(System.StringComparer.Ordinal);

            var items = new List<WorldSceneRegistrySyncItem>(sortedScenePaths.Count);
            foreach (var path in sortedScenePaths)
            {
                items.Add(new WorldSceneRegistrySyncItem(path));
            }

            return new WorldSceneRegistrySyncPlan(items);
        }
    }
}
