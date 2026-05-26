using System.Collections.Generic;
using NUnit.Framework;
using PortalSystem.Runtime;
using UnityEngine;

namespace PortalSystem.Tests
{
    [TestFixture]
    public class PortalHubTests
    {
        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();
        }

        [Test]
        public void PortalHub_Initialization_CorrectlyEnsuresBoxColliderTrigger()
        {
            // 1. Setup
            var go = new GameObject("PortalHubTest");
            _spawnedObjects.Add(go);

            // 2. Execute
            var portal = go.AddComponent<PortalHub>();

            // 3. Verify: Reset should trigger either on component addition or manual Reset
            portal.SendMessage("Reset", null, SendMessageOptions.DontRequireReceiver);

            var col = go.GetComponent<BoxCollider>();
            Assert.IsNotNull(col, "BoxCollider should be automatically added to PortalHub");
            Assert.IsTrue(
                col.isTrigger,
                "Automatically added BoxCollider must be marked as isTrigger"
            );
        }

        [Test]
        public void PortalHub_TriggerTeleport_InvokesEvents()
        {
            // 1. Setup
            var go = new GameObject("PortalHubTest");
            _spawnedObjects.Add(go);
            var portal = go.AddComponent<PortalHub>();
            portal.targetLevelName = "LevelA";
            portal.targetSpawnPointID = "SpawnA";

            string receivedLevel = null;
            string receivedSpawn = null;
            portal.OnPortalTriggeredAction += (level, spawn) =>
            {
                receivedLevel = level;
                receivedSpawn = spawn;
            };

            // 2. Execute
            portal.TriggerTeleport();

            // 3. Verify
            Assert.AreEqual("LevelA", receivedLevel, "Teleport event level name mismatch");
            Assert.AreEqual("SpawnA", receivedSpawn, "Teleport event spawn point ID mismatch");
        }

        [Test]
        public void PortalHub_EmptyLevelName_DoesNotTrigger()
        {
            // 1. Setup
            var go = new GameObject("PortalHubTest");
            _spawnedObjects.Add(go);
            var portal = go.AddComponent<PortalHub>();
            portal.targetLevelName = "";
            portal.targetSpawnPointID = "SpawnA";

            bool isFired = false;
            portal.OnPortalTriggeredAction += (level, spawn) =>
            {
                isFired = true;
            };

            // 2. Execute
            portal.TriggerTeleport();

            // 3. Verify
            Assert.IsFalse(
                isFired,
                "Teleport event should not fire if target level name is null or empty"
            );
        }

        [Test]
        public void PortalHub_PlayerCollision_TriggersTeleport()
        {
            // 1. Setup
            var go = new GameObject("PortalHubTest");
            _spawnedObjects.Add(go);
            var portal = go.AddComponent<PortalHub>();
            portal.targetLevelName = "LevelB";
            portal.targetSpawnPointID = "SpawnB";

            bool isFired = false;
            portal.OnPortalTriggeredAction += (level, spawn) =>
            {
                isFired = true;
            };

            // Create a mock collider tagged as "Player"
            var playerGo = new GameObject("PlayerMock");
            _spawnedObjects.Add(playerGo);
            playerGo.tag = "Player";
            var playerCol = playerGo.AddComponent<BoxCollider>();

            // 2. Execute: Simulate OnTriggerEnter physics callback
            portal.SendMessage("OnTriggerEnter", playerCol, SendMessageOptions.DontRequireReceiver);

            // 3. Verify
            Assert.IsTrue(
                isFired,
                "Teleport event should be triggered when a Player tagged collider collides with the portal trigger"
            );
        }
    }
}
