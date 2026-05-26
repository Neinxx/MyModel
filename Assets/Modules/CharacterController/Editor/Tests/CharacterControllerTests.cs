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
            motor.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            // 2. Execute
            motor.Move(Vector3.forward, 6f);

            // 3. Verify via reflection
            var moveField = typeof(CharacterMotor).GetField("_horizontalMove", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(moveField, "CharacterMotor should have a private _horizontalMove field");

            Vector3 horizontalMove = (Vector3)moveField.GetValue(motor);
            Assert.AreEqual(Vector3.forward * 6f, horizontalMove, "Move direction or speed was not cached correctly in _horizontalMove");
        }

        [Test]
        public void CharacterMotor_Jump_SetsVerticalVelocityCorrectlyWhenGrounded()
        {
            // 1. Setup
            var go = new GameObject("CharacterActor");
            _spawnedObjects.Add(go);
            go.AddComponent<UnityEngine.CharacterController>();
            
            var motor = go.AddComponent<CharacterMotor>();
            motor.gravity = -10f;
            motor.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            // Force _isGrounded to true
            var groundedField = typeof(CharacterMotor).GetField("_isGrounded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(groundedField, "CharacterMotor should have a private _isGrounded field");
            groundedField.SetValue(motor, true);

            // 2. Execute: Jump with force 5
            motor.Jump(5f);

            // 3. Verify via reflection
            var velocityField = typeof(CharacterMotor).GetField("_verticalVelocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(velocityField, "CharacterMotor should have a private _verticalVelocity field");

            Vector3 verticalVel = (Vector3)velocityField.GetValue(motor);
            
            // Expected = Mathf.Sqrt(5 * -2 * -10) = Mathf.Sqrt(100) = 10f
            Assert.AreEqual(10f, verticalVel.y, 0.01f, "Jump vertical velocity was not calculated correctly");
        }
    }
}
