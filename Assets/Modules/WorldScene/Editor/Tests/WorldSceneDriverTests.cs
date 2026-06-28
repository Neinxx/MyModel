using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using WorldSceneModule.Editor;
using WorldSceneModule.Runtime;

namespace WorldSceneModule.Tests
{
    [TestFixture]
    public class WorldSceneDriverTests
    {
        private List<GameObject> _spawnedObjects;
        private List<ScriptableObject> _spawnedAssets;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
            _spawnedAssets = new List<ScriptableObject>();
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy runtime instances
            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();

            // Destroy scriptable assets created in memory
            foreach (var asset in _spawnedAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _spawnedAssets.Clear();

        }

        private LevelRegistry CreateMockRegistry()
        {
            var registry = ScriptableObject.CreateInstance<LevelRegistry>();
            _spawnedAssets.Add(registry);

            registry.levels = new List<LevelConfig>
            {
                new LevelConfig
                {
                    levelName = "LevelA",
                    sceneAsset = null,
                    moduleData = null,
                    cachedScenePath = "Assets/Scenes/LevelA.unity",
                },
                new LevelConfig
                {
                    levelName = "LevelB",
                    sceneAsset = null,
                    moduleData = null,
                    cachedScenePath = "Assets/Scenes/LevelB.unity",
                },
            };
            registry.defaultSubLevel = "LevelA";
            registry.cachedPersistentWorldPath = "Assets/Scenes/PersistentWorld.unity";

            return registry;
        }

        [Test]
        public void LevelRegistry_GetConfig_ReturnsCorrectConfig()
        {
            // 1. Setup
            var registry = CreateMockRegistry();

            // 2. Execute
            var config = registry.GetConfig("LevelB");

            // 3. Verify
            Assert.IsNotNull(config, "Registry failed to find registered level");
            Assert.AreEqual("LevelB", config.levelName, "Registry found wrong config name");
        }

        [Test]
        public void LevelRegistry_UsesCachedPathsAtRuntimeBoundary()
        {
            var registry = CreateMockRegistry();

            Assert.AreEqual("Assets/Scenes/PersistentWorld.unity", registry.GetPersistentWorldPath());
            Assert.AreEqual("Assets/Scenes/LevelA.unity", registry.GetScenePath("LevelA"));
        }

        [Test]
        public void BuildSettingsValidator_ReportsMissingRegisteredScenes()
        {
            var registry = CreateMockRegistry();
            var enabledScenes = new[]
            {
                "Assets/Scenes/PersistentWorld.unity",
                "Assets/Scenes/LevelA.unity",
            };

            var missingPaths = WorldSceneBuildSettingsValidator.FindMissingScenePaths(
                registry,
                enabledScenes
            );

            CollectionAssert.AreEqual(new[] { "Assets/Scenes/LevelB.unity" }, missingPaths);
        }

        [Test]
        public void BuildSettingsValidator_DeduplicatesMissingRegisteredScenes()
        {
            var registry = CreateMockRegistry();
            registry.levels.Add(
                new LevelConfig
                {
                    levelName = "LevelB_Copy",
                    cachedScenePath = "Assets/Scenes/LevelB.unity",
                }
            );

            var missingPaths = WorldSceneBuildSettingsValidator.FindMissingScenePaths(
                registry,
                new[] { "Assets/Scenes/PersistentWorld.unity", "Assets/Scenes/LevelA.unity" }
            );

            CollectionAssert.AreEqual(new[] { "Assets/Scenes/LevelB.unity" }, missingPaths);
        }

        [Test]
        public void RegistrySyncPlan_SortsAndDerivesCompanionAssetPaths()
        {
            var plan = WorldSceneRegistrySyncPlan.FromScenePaths(new[]
            {
                "Assets/Scenes/World/LevelB.unity",
                "Assets/Scenes/World/LevelA.unity",
                "Assets/Scenes/World/LevelB.unity",
            });

            Assert.AreEqual(2, plan.Items.Count);
            Assert.AreEqual("LevelA", plan.Items[0].LevelName);
            Assert.AreEqual("Assets/Scenes/World/LevelA_LevelModuleData.asset", plan.Items[0].ModuleDataPath);
            Assert.AreEqual("Assets/Scenes/World/LevelA_DecalLevelData.asset", plan.Items[0].DecalDataPath);
            Assert.AreEqual("LevelB", plan.Items[1].LevelName);
        }

        [Test]
        public void RegistrySyncPlan_IgnoresEmptyScenePaths()
        {
            var plan = WorldSceneRegistrySyncPlan.FromScenePaths(new[]
            {
                string.Empty,
                "Assets/Scenes/World/LevelA.unity",
                null,
            });

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual("Assets/Scenes/World/LevelA.unity", plan.Items[0].ScenePath);
        }

        [Test]
        public void WorldSceneDriver_LevelLoadedEvent_CanNotifySubscribers()
        {
            var go = new GameObject("WorldSceneDriver");
            _spawnedObjects.Add(go);
            var driver = go.AddComponent<WorldSceneDriver>();
            var targetConfig = new LevelConfig { levelName = "BroadcastLevelTest" };
            LevelConfig receivedConfig = default;
            bool received = false;

            driver.LevelLoaded += (config, scene, cancellationToken) =>
            {
                received = true;
                receivedConfig = config;
                return UniTask.CompletedTask;
            };

            var eventInfo = typeof(WorldSceneDriver).GetField(
                "LevelLoaded",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            var handler = eventInfo?.GetValue(driver) as WorldSceneLifecycleHandler;
            handler?.Invoke(targetConfig, default, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(received, "LevelLoaded event was not received");
            Assert.AreEqual(
                "BroadcastLevelTest",
                receivedConfig.levelName,
                "LevelLoaded did not deliver correct config data"
            );
        }
    }
}
