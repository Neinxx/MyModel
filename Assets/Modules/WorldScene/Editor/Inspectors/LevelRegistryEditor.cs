using System.Collections.Generic;
using System.IO;
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

        public override VisualElement CreateInspectorGUI()
        {
            var uxml = WorldScenePathUtility.LoadUXML("RegistryEditor");
            if (uxml == null)
                return new Label("UXML Missing");

            _root = uxml.Instantiate();
            var uss = WorldScenePathUtility.LoadUSS("RegistryEditor");
            if (uss != null)
                _root.styleSheets.Add(uss);

            var syncBtn = _root.Q<Button>("sync-btn");
            if (syncBtn != null)
            {
                syncBtn.clicked += SmartSync;
            }

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

            SyncRegistry(registry);
        }

        /// <summary>
        /// 关卡注册表核心装配与资产绑定引擎 (Headless-safe Sync Engine)
        /// 可从编辑器 UI 或自动化构建管线中无感静默调用
        /// </summary>
        public static void SyncRegistry(LevelRegistry registry)
        {
            if (registry == null || registry.scenesRootFolder == null) return;

            string rootPath = AssetDatabase.GetAssetPath(registry.scenesRootFolder);
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { rootPath });

            registry.levels.Clear();
            if (registry.persistentWorldAsset != null)
            {
                registry.cachedPersistentWorldPath = AssetDatabase.GetAssetPath(registry.persistentWorldAsset);
            }

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

                // 🚀 自动寻获并装配聚合模块数据容器 (LevelModuleData)
                string sceneDir = Path.GetDirectoryName(path).Replace("\\", "/");
                string moduleDataPath = $"{sceneDir}/{name}_LevelModuleData.asset";
                var moduleData = AssetDatabase.LoadAssetAtPath<LevelModuleData>(moduleDataPath);
                if (moduleData == null)
                {
                    moduleData = ScriptableObject.CreateInstance<LevelModuleData>();
                    AssetDatabase.CreateAsset(moduleData, moduleDataPath);
                }

                // 自动寻获当前关场景同目录下的贴花数据，如果存在且未赋值，则自动关联
                string decalDataPath = $"{sceneDir}/{name}_DecalLevelData.asset";
                var decalData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(decalDataPath);
                if (decalData != null)
                {
                    moduleData.RegisterSubData(decalData);
                    EditorUtility.SetDirty(moduleData);
                }

                registry.levels.Add(
                    new LevelConfig
                    {
                        levelName = name,
                        sceneAsset = asset,
                        moduleData = moduleData,
                        cachedScenePath = path,
                    }
                );
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"<color=#3FB950><b>[WorldScene]</b></color> Smart Sync Complete. Found {registry.levels.Count} scenes."
            );
        }
    }
}
