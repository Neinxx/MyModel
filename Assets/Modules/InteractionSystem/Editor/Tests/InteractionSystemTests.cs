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

            var hits = new[] { lowCol, highCol, unCol };

            // 2. Execute
            var best = interactor.FindBestInteractable(hits, hits.Length);

            // 3. Verify: Check that the highest priority interactable (highInteractable) is selected, and uninteractable is ignored
            Assert.AreEqual(highInteractable, best, "ProximityInteractor failed to select the highest priority active interactable");
        }

        [Test]
        public void ProximityInteractor_OnScanAndInteract_ExecutesInteractionOnBestTarget()
        {
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();

            var targetGo = new GameObject("TargetActive");
            _spawnedObjects.Add(targetGo);
            var col = targetGo.AddComponent<BoxCollider>();
            var interactable = targetGo.AddComponent<MockInteractable>();
            interactable.PriorityValue = 50;

            var hits = new[] { col };

            bool didInteract = interactor.TryInteract(hits, hits.Length);

            Assert.IsTrue(didInteract, "Interactor did not report a successful interaction");
            Assert.IsTrue(interactable.HasInteracted, "Best target was not interacted with during proximity scanning");
            Assert.AreEqual(interactorGo, interactable.Interactor, "Interaction was triggered with the wrong interactor reference");
        }

        [Test]
        public void ProximityInteractor_InteractCurrent_UsesScannedTarget()
        {
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();

            var targetGo = new GameObject("TargetActive");
            _spawnedObjects.Add(targetGo);
            var col = targetGo.AddComponent<BoxCollider>();
            var interactable = targetGo.AddComponent<MockInteractable>();

            var target = interactor.FindBestInteractable(new[] { col }, 1);
            interactor.TryInteract(new[] { col }, 1);

            Assert.AreEqual(interactable, target);
            Assert.AreEqual(interactable, interactor.CurrentInteractable);
            Assert.IsTrue(interactable.HasInteracted);
        }

        [Test]
        public void ProximityInteractor_AutoTriggerDisabled_DoesNotBlockManualInteraction()
        {
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();
            interactor.SetAutoTrigger(false);

            var targetGo = new GameObject("TargetActive");
            _spawnedObjects.Add(targetGo);
            var col = targetGo.AddComponent<BoxCollider>();
            var interactable = targetGo.AddComponent<MockInteractable>();

            bool didInteract = interactor.TryInteract(new[] { col }, 1);

            Assert.IsTrue(didInteract);
            Assert.IsTrue(interactable.HasInteracted);
        }

        [Test]
        public void ProximityInteractor_TargetChanged_FiresWhenCurrentTargetChanges()
        {
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();

            var targetGo = new GameObject("TargetActive");
            _spawnedObjects.Add(targetGo);
            var col = targetGo.AddComponent<BoxCollider>();
            var interactable = targetGo.AddComponent<MockInteractable>();

            IInteractable receivedTarget = null;
            interactor.TargetChanged += target => receivedTarget = target;

            interactor.TryInteract(new[] { col }, 1);

            Assert.AreEqual(interactable, receivedTarget);
            Assert.IsTrue(interactor.HasTarget);
        }

        [Test]
        public void ProximityInteractor_FindBestInteractable_ReturnsNullForEmptyHits()
        {
            var interactorGo = new GameObject("InteractorActor");
            _spawnedObjects.Add(interactorGo);
            var interactor = interactorGo.AddComponent<ProximityInteractor>();

            var best = interactor.FindBestInteractable(System.Array.Empty<Collider>(), 0);

            Assert.IsNull(best);
        }
    }
}
