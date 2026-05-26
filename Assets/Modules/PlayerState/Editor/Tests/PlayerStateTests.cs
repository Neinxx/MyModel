using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using PlayerState.Runtime;
using ModularDemo.Runtime;

namespace PlayerState.Tests
{
    // A concrete implementation of the abstract BaseFeatureSO for testing purposes
    public class MockFeatureSO : BaseFeatureSO
    {
        public static MockFeatureSO CreateInstance(string id, string slot, float hpMod, float atkMod)
        {
            var feature = ScriptableObject.CreateInstance<MockFeatureSO>();
            feature.featureID = id;
            feature.slotID = slot;
            feature.displayName = id;
            feature.hpModifier = hpMod;
            feature.attackModifier = atkMod;
            return feature;
        }
    }

    [TestFixture]
    public class PlayerStateTests
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
            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();

            foreach (var asset in _spawnedAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _spawnedAssets.Clear();
        }

        [Test]
        public void PlayerStateSO_Equip_CorrectlyEquipsFeatureAndModifiesStats()
        {
            // 1. Setup
            var playerState = ScriptableObject.CreateInstance<PlayerStateSO>();
            _spawnedAssets.Add(playerState);
            playerState.baseMaxHP = 100f;
            playerState.baseAttack = 10f;
            playerState.ResetToDefault();

            var feature = MockFeatureSO.CreateInstance("TestFeature", "AuraSlot", 25f, 5f);
            _spawnedAssets.Add(feature);

            // 2. Execute
            playerState.Equip(feature);

            // 3. Verify
            Assert.AreEqual(feature, playerState.GetFeature("AuraSlot"), "Feature was not equipped into correct slot");
            Assert.AreEqual(125f, playerState.FinalMaxHP, "Max HP modifier not applied correctly");
            Assert.AreEqual(15f, playerState.FinalAttack, "Attack modifier not applied correctly");
        }

        [Test]
        public void PlayerStateSO_Unequip_CorrectlyRemovesFeatureAndResetsStats()
        {
            // 1. Setup
            var playerState = ScriptableObject.CreateInstance<PlayerStateSO>();
            _spawnedAssets.Add(playerState);
            playerState.baseMaxHP = 100f;
            playerState.baseAttack = 10f;
            playerState.ResetToDefault();

            var feature = MockFeatureSO.CreateInstance("TestFeature", "AuraSlot", 25f, 5f);
            _spawnedAssets.Add(feature);

            playerState.Equip(feature);
            Assert.AreEqual(125f, playerState.FinalMaxHP);

            // 2. Execute
            playerState.Unequip("AuraSlot");

            // 3. Verify
            Assert.IsNull(playerState.GetFeature("AuraSlot"), "Feature was not removed from slot after Unequip");
            Assert.AreEqual(100f, playerState.FinalMaxHP, "Stats did not return to default after Unequip");
            Assert.AreEqual(10f, playerState.FinalAttack, "Stats did not return to default after Unequip");
        }

        [Test]
        public void PlayerStateSO_DuplicateSlotEquip_OverwritesPreviousFeature()
        {
            // 1. Setup
            var playerState = ScriptableObject.CreateInstance<PlayerStateSO>();
            _spawnedAssets.Add(playerState);
            playerState.baseMaxHP = 100f;
            playerState.baseAttack = 10f;
            playerState.ResetToDefault();

            var feature1 = MockFeatureSO.CreateInstance("Feature1", "AuraSlot", 25f, 5f);
            var feature2 = MockFeatureSO.CreateInstance("Feature2", "AuraSlot", 50f, 12f);
            _spawnedAssets.Add(feature1);
            _spawnedAssets.Add(feature2);

            playerState.Equip(feature1);
            Assert.AreEqual(125f, playerState.FinalMaxHP);

            // 2. Execute
            playerState.Equip(feature2);

            // 3. Verify
            Assert.AreEqual(feature2, playerState.GetFeature("AuraSlot"), "Registry did not overwrite previous slot occupant");
            Assert.AreEqual(150f, playerState.FinalMaxHP, "Max HP modifier was not recalculated for the new feature");
            Assert.AreEqual(22f, playerState.FinalAttack, "Attack modifier was not recalculated for the new feature");
        }

        [Test]
        public void FeaturePickupComponent_OnInteract_SuccessfullyEquipsFeature()
        {
            // 1. Setup: Create player with bridge and state
            var playerGo = new GameObject("PlayerActor");
            _spawnedObjects.Add(playerGo);
            var bridge = playerGo.AddComponent<PlayerStateBridge>();
            
            var playerState = ScriptableObject.CreateInstance<PlayerStateSO>();
            _spawnedAssets.Add(playerState);
            playerState.baseMaxHP = 100f;
            playerState.baseAttack = 10f;
            playerState.ResetToDefault();
            bridge.playerState = playerState;

            // Create FeaturePickup object
            var pickupGo = new GameObject("FeaturePickup");
            _spawnedObjects.Add(pickupGo);
            var pickup = pickupGo.AddComponent<FeaturePickupComponent>();
            pickup.destroyOnInteract = false; // Set to false to verify setActive behavior easily

            var feature = MockFeatureSO.CreateInstance("LootFeature", "AuraSlot", 30f, 8f);
            _spawnedAssets.Add(feature);
            pickup.featureToEquip = feature;

            // 2. Execute
            pickup.OnInteract(playerGo);

            // 3. Verify: Feature equipped and pickup object deactivated
            Assert.AreEqual(feature, playerState.GetFeature("AuraSlot"), "Picked up feature failed to equip on interactor's state SO");
            Assert.AreEqual(130f, playerState.FinalMaxHP, "Picked up feature stats were not registered on player state SO");
            Assert.IsFalse(pickupGo.activeSelf, "Pickup GameObject should have been deactivated on interaction");
        }
    }
}
