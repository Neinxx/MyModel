using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using InteractionSystem.Runtime;

namespace InteractionSystem.Tests
{
    public class MockInteractable : MonoBehaviour, IInteractable
    {
        public bool HasInteracted { get; private set; } = false;
        public GameObject Interactor { get; private set; } = null;
        public int PriorityValue = 50;
        public bool CanInteract = true;

        public int InteractionPriority => PriorityValue;
        public bool IsInteractable => CanInteract;

        public void OnInteract(GameObject interactor)
        {
            HasInteracted = true;
            Interactor = interactor;
        }
    }

    [TestFixture]
    public class InteractionSystemTests
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
        public void ProximityInteractor_FindBestInteractable_CorrectlySelectsHighestPriority()
        {
            // 1. Setup
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();

            // Create target interactables
            var targetLowGo = new GameObject("TargetLow");
            _spawnedObjects.Add(targetLowGo);
            var lowCol = targetLowGo.AddComponent<BoxCollider>();
            var lowInteractable = targetLowGo.AddComponent<MockInteractable>();
            lowInteractable.PriorityValue = 10;

            var targetHighGo = new GameObject("TargetHigh");
            _spawnedObjects.Add(targetHighGo);
            var highCol = targetHighGo.AddComponent<BoxCollider>();
            var highInteractable = targetHighGo.AddComponent<MockInteractable>();
            highInteractable.PriorityValue = 99;

            var targetUninteractableGo = new GameObject("TargetUninteractable");
            _spawnedObjects.Add(targetUninteractableGo);
            var unCol = targetUninteractableGo.AddComponent<BoxCollider>();
            var unInteractable = targetUninteractableGo.AddComponent<MockInteractable>();
            unInteractable.PriorityValue = 200;
            unInteractable.CanInteract = false;

            // Use reflection to mock the private hits array
            var hitsField = typeof(ProximityInteractor).GetField("_hits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(hitsField, "ProximityInteractor should have a private _hits field");
            
            Collider[] hits = (Collider[])hitsField.GetValue(interactor);
            hits[0] = lowCol;
            hits[1] = highCol;
            hits[2] = unCol;

            // 2. Execute: Call the private FindBestInteractable method using reflection
            var findMethod = typeof(ProximityInteractor).GetMethod("FindBestInteractable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(findMethod, "ProximityInteractor should have a private FindBestInteractable method");

            var best = (IInteractable)findMethod.Invoke(interactor, new object[] { 3 });

            // 3. Verify: Check that the highest priority interactable (highInteractable) is selected, and uninteractable is ignored
            Assert.AreEqual(highInteractable, best, "ProximityInteractor failed to select the highest priority active interactable");
        }

        [Test]
        public void ProximityInteractor_OnScanAndInteract_ExecutesInteractionOnBestTarget()
        {
            // 1. Setup
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();

            var targetGo = new GameObject("TargetActive");
            _spawnedObjects.Add(targetGo);
            var col = targetGo.AddComponent<BoxCollider>();
            var interactable = targetGo.AddComponent<MockInteractable>();
            interactable.PriorityValue = 50;

            // Populate hits array manually via reflection
            var hitsField = typeof(ProximityInteractor).GetField("_hits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Collider[] hits = (Collider[])hitsField.GetValue(interactor);
            hits[0] = col;

            // 2. Execute: Call the private ScanAndInteract method using reflection
            var scanMethod = typeof(ProximityInteractor).GetMethod("ScanAndInteract", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(scanMethod, "ProximityInteractor should have a private ScanAndInteract method");

            scanMethod.Invoke(interactor, null);

            // 3. Verify
            Assert.IsTrue(interactable.HasInteracted, "Best target was not interacted with during proximity scanning");
            Assert.AreEqual(interactorGo, interactable.Interactor, "Interaction was triggered with the wrong interactor reference");
        }
    }
}
