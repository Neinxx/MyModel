using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using CharacterController.Runtime;

namespace CharacterController.Tests
{
    [TestFixture]
    public class CharacterControllerTests
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
        public void CharacterMotor_Move_CachesHorizontalMoveVector()
        {
            // 1. Setup
            var go = new GameObject("CharacterActor");
            _spawnedObjects.Add(go);
            go.AddComponent<UnityEngine.CharacterController>();
            
            var motor = go.AddComponent<CharacterMotor>();

            // 2. Execute
            motor.Move(Vector3.forward, 6f);

            // 3. Verify
            Assert.AreEqual(Vector3.forward * 6f, motor.HorizontalMoveForTests, "Move direction or speed was not cached correctly.");
        }

        [Test]
        public void CharacterMotor_Jump_SetsVerticalVelocityCorrectlyWhenGrounded()
        {
            // 1. Setup
            var go = new GameObject("CharacterActor");
            _spawnedObjects.Add(go);
            go.AddComponent<UnityEngine.CharacterController>();
            
            var motor = go.AddComponent<CharacterMotor>();
            motor.SetGravity(-10f);
            motor.SetGroundedForTests(true);

            // 2. Execute: Jump with force 5
            motor.Jump(5f);

            // 3. Verify
            // Expected = Mathf.Sqrt(5 * -2 * -10) = Mathf.Sqrt(100) = 10f
            Assert.AreEqual(10f, motor.VerticalVelocityForTests.y, 0.01f, "Jump vertical velocity was not calculated correctly");
        }
    }
}
