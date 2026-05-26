using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PortalSystem.Runtime;

namespace Tests.Runtime
{
    [TestFixture]
    public class PortalSystemPlayModeTests
    {
        private GameObject _playerGo;
        private GameObject _portalGo;
        private PortalHub _portalHub;
        
        private bool _eventTriggered;
        private string _triggeredLevel;
        private string _triggeredSpawnPoint;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            _eventTriggered = false;
            _triggeredLevel = null;
            _triggeredSpawnPoint = null;

            // 1. Create Portal Hub
            _portalGo = new GameObject("TestPortal");
            _portalGo.transform.position = Vector3.zero;

            var col = _portalGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 2f);

            _portalHub = _portalGo.AddComponent<PortalHub>();
            _portalHub.targetLevelName = "TestTargetLevel";
            _portalHub.targetSpawnPointID = "TestTargetSpawn";
            
            // Subscribe to portal event
            _portalHub.OnPortalTriggeredAction += HandlePortalTriggered;

            // 2. Create Player
            _playerGo = new GameObject("TestPlayer");
            _playerGo.tag = "Player"; // Important: tag must be "Player"
            _playerGo.transform.position = new Vector3(5f, 0f, 0f); // Spawn far away

            // Add rigid body and collider so collision triggers
            var rb = _playerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            
            var playerCol = _playerGo.AddComponent<BoxCollider>();
            playerCol.size = new Vector3(1f, 1f, 1f);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_portalHub != null)
            {
                _portalHub.OnPortalTriggeredAction -= HandlePortalTriggered;
            }
            if (_portalGo != null)
            {
                Object.Destroy(_portalGo);
            }
            if (_playerGo != null)
            {
                Object.Destroy(_playerGo);
            }
            yield return null;
        }

        private void HandlePortalTriggered(string level, string spawnPoint)
        {
            _eventTriggered = true;
            _triggeredLevel = level;
            _triggeredSpawnPoint = spawnPoint;
        }

        [UnityTest]
        public IEnumerator PortalHub_OnPlayerCollision_TriggersTeleportEvent()
        {
            Assert.IsFalse(_eventTriggered, "Portal triggered prior to physical contact.");

            // 1. Physically move the player inside the portal bounds
            _playerGo.transform.position = Vector3.zero;

            // 2. Wait for physics simulation update to register Trigger collision
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // 3. Assert
            Assert.IsTrue(_eventTriggered, "Portal failed to trigger teleport event on physical collision.");
            Assert.AreEqual("TestTargetLevel", _triggeredLevel, "Portal triggered incorrect level name.");
            Assert.AreEqual("TestTargetSpawn", _triggeredSpawnPoint, "Portal triggered incorrect spawn point ID.");
        }

        [UnityTest]
        public IEnumerator PortalHub_OnNonPlayerCollision_DoesNotTriggerTeleport()
        {
            // Set player tag to Untagged to simulate an npc or simple prop collision
            _playerGo.tag = "Untagged";
            
            // Move inside portal bounds
            _playerGo.transform.position = Vector3.zero;

            // Wait for physics frames
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Assert that no event was triggered
            Assert.IsFalse(_eventTriggered, "Portal triggered on a non-Player tag collider overlap.");
        }
    }
}
