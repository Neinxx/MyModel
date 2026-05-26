using System.Collections.Generic;
using NUnit.Framework;
using SpawnPoint.Runtime;
using UnityEngine;

namespace SpawnPoint.Tests
{
    [TestFixture]
    public class SpawnPointTests
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

        private SpawnPointHub CreateSpawnPoint(
            string id,
            int teamIndex,
            int priority,
            bool isBlocked = false
        )
        {
            var go = new GameObject($"SpawnPoint_{id}");
            _spawnedObjects.Add(go);

            var hub = go.AddComponent<SpawnPointHub>();

            // Set private fields via reflection to avoid altering runtime API
            SetPrivateField(hub, "_hubID", id);
            SetPrivateField(hub, "_teamIndex", teamIndex);
            SetPrivateField(hub, "_priority", priority);
            SetPrivateField(hub, "_isBlocked", isBlocked);

            // Trigger OnEnable to register since we created it dynamically
            hub.SendMessage("OnEnable", null, SendMessageOptions.DontRequireReceiver);

            return hub;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType()
                .GetField(
                    fieldName,
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        [Test]
        public void SpawnPoint_Registration_CorrectlyAddsAndRemovesFromRegistry()
        {
            // 1. Setup & Execute
            var hub = CreateSpawnPoint("Test_Register_1", 0, 100);

            // 2. Verify Registration
            Assert.AreEqual(
                hub,
                SpawnPointHub.GetByID("Test_Register_1"),
                "Spawn point failed to register O(1) lookup"
            );

            bool foundInAll = false;
            foreach (var h in SpawnPointHub.AllHubs)
            {
                if (h == hub)
                {
                    foundInAll = true;
                    break;
                }
            }
            Assert.IsTrue(foundInAll, "Spawn point not found in SpawnPointHub.AllHubs");

            // 3. Execute Unregistration (via disabling/destructing)
            hub.SendMessage("OnDisable", null, SendMessageOptions.DontRequireReceiver);

            // 4. Verify Unregistration
            Assert.IsNull(
                SpawnPointHub.GetByID("Test_Register_1"),
                "Spawn point failed to unregister O(1) lookup"
            );
        }

        [Test]
        public void SpawnPoint_Ordering_ReturnsCorrectSortedList()
        {
            // 1. Setup: Create multiple spawn points with different priorities and block status
            var hubLow = CreateSpawnPoint("LowPri", 0, 50);
            var hubHigh = CreateSpawnPoint("HighPri", 0, 200);
            var hubBlocked = CreateSpawnPoint("BlockedHigh", 0, 300, isBlocked: true);
            var hubOtherTeam = CreateSpawnPoint("OtherTeam", 1, 400);

            // 2. Execute: Get ordered hubs for team 0 (excluding blocked by default)
            var ordered = SpawnPointHub.GetOrderedHubsForTeam(0, includeBlocked: false);

            // 3. Verify: Check that count is 2, ordered high to low, and excludes other team/blocked
            Assert.AreEqual(2, ordered.Count, "Team 0 ordered hub count mismatch");
            Assert.AreEqual(hubHigh, ordered[0], "Highest priority point should be first");
            Assert.AreEqual(hubLow, ordered[1], "Lowest priority point should be second");

            // 4. Execute: Get ordered hubs for team 0 including blocked
            var orderedWithBlocked = SpawnPointHub.GetOrderedHubsForTeam(0, includeBlocked: true);

            // 5. Verify: Check that blocked high priority point is now first
            Assert.AreEqual(
                3,
                orderedWithBlocked.Count,
                "Team 0 including blocked hub count mismatch"
            );
            Assert.AreEqual(
                hubBlocked,
                orderedWithBlocked[0],
                "Blocked point with highest priority should be first"
            );
        }

        [Test]
        public void SpawnPoint_DuplicateID_UpdatesLookupSuccessfully()
        {
            // 1. Setup: Create first spawn point
            var hub1 = CreateSpawnPoint("DuplicateID", 0, 100);
            Assert.AreEqual(
                hub1,
                SpawnPointHub.GetByID("DuplicateID"),
                "First point lookup failed"
            );

            // 2. Execute: Create second spawn point with same ID (logs warning, but updates registry)
            var hub2 = CreateSpawnPoint("DuplicateID", 0, 200);

            // 3. Verify: Registry points to second one (latest registration wins)
            Assert.AreEqual(
                hub2,
                SpawnPointHub.GetByID("DuplicateID"),
                "Registry did not update to the latest duplicate ID point"
            );

            // Cleanup hub1 manually since both are in _spawnedObjects
            hub2.SendMessage("OnDisable", null, SendMessageOptions.DontRequireReceiver);
            Assert.AreEqual(
                hub1,
                SpawnPointHub.GetByID("DuplicateID"),
                "Registry did not fall back to previous active point after duplicate unregistration"
            );
        }
    }
}
