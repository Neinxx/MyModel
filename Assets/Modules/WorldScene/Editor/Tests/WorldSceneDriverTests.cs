using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
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
                },
                new LevelConfig
                {
                    levelName = "LevelB",
                    sceneAsset = null,
                    moduleData = null,
                },
            };
            registry.defaultSubLevel = "LevelA";

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
