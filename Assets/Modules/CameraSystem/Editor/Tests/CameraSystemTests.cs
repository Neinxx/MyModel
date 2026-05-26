using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Cinemachine;
using CameraSystem.Runtime;
using CharacterController.Runtime;

namespace CameraSystem.Tests
{
    [TestFixture]
    public class CameraSystemTests
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

            // Reset singleton reference
            var instanceField = typeof(CameraManager).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceField != null)
            {
                instanceField.SetValue(null, null);
            }
        }

        [Test]
        public void CameraManager_SingletonPattern_EnforcesUniqueInstance()
        {
            // 1. Setup: First instance
            var go1 = new GameObject("CameraManager1");
            _spawnedObjects.Add(go1);
            var manager1 = go1.AddComponent<CameraManager>();
            
            manager1.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            // Verify first instance became singleton
            Assert.AreEqual(manager1, CameraManager.Instance, "First instance failed to set Singleton reference");

            // 2. Execute: Create second instance
            var go2 = new GameObject("CameraManager2");
            _spawnedObjects.Add(go2);
            var manager2 = go2.AddComponent<CameraManager>();
            
            manager2.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            // 3. Verify: Second instance destroyed itself
            Assert.IsTrue(go2 == null || !go2.activeInHierarchy || go2.GetComponent<CameraManager>() == null, 
                "Second CameraManager instance should have destroyed itself or its component");
        }

        [Test]
        public void CameraManager_SetAllTargets_SetsTargetOnAllVirtualCameras()
        {
            // 1. Setup: Initialize singleton camera manager
            var managerGo = new GameObject("CameraManager");
            _spawnedObjects.Add(managerGo);
            var manager = managerGo.AddComponent<CameraManager>();
            manager.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            // Create target transform
            var targetGo = new GameObject("CameraTarget");
            _spawnedObjects.Add(targetGo);
            var targetTransform = targetGo.transform;
            targetGo.AddComponent<CharacterSocketRegistry>();
            var socketGo = new GameObject("CameraTargetSocket");
            _spawnedObjects.Add(socketGo);
            socketGo.transform.SetParent(targetTransform, false);
            socketGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            socketGo.AddComponent<CharacterSocket>().Configure(CharacterSocketId.CameraTarget);

            // Create virtual cameras
            var camGo1 = new GameObject("VirtualCam1");
            _spawnedObjects.Add(camGo1);
            var cam1 = camGo1.AddComponent<CinemachineVirtualCamera>();

            var camGo2 = new GameObject("VirtualCam2");
            _spawnedObjects.Add(camGo2);
            var cam2 = camGo2.AddComponent<CinemachineVirtualCamera>();

            // 2. Execute
            manager.SetAllTargets(targetTransform);

            // 3. Verify: Follow and LookAt use the character socket protocol.
            var registry = targetGo.GetComponent<CharacterSocketRegistry>();
            Assert.IsNotNull(registry, "CharacterSocketRegistry is required");
            Assert.IsTrue(registry.TryGet(CharacterSocketId.CameraTarget, out var expectedTarget), "CameraTarget socket was not registered");
            Assert.AreEqual(expectedTarget, cam1.Follow, "cam1.Follow target was not set correctly");
            Assert.AreEqual(expectedTarget, cam1.LookAt, "cam1.LookAt target was not set correctly");
            Assert.AreEqual(expectedTarget, cam2.Follow, "cam2.Follow target was not set correctly");
            Assert.AreEqual(expectedTarget, cam2.LookAt, "cam2.LookAt target was not set correctly");
        }
    }
}
