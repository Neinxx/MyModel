using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CharacterController.Runtime;

namespace Tests.Runtime
{
    [TestFixture]
    public class CharacterMotorPlayModeTests
    {
        private GameObject _playerGo;
        private GameObject _floorGo;
        private CharacterMotor _motor;
        private UnityEngine.CharacterController _cc;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // 1. Create a physical floor so character controller can interact and be grounded
            _floorGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floorGo.name = "TestFloor";
            _floorGo.transform.position = Vector3.zero;
            _floorGo.transform.localScale = new Vector3(10f, 1f, 10f);

            // 2. Create the player gameObject
            _playerGo = new GameObject("TestPlayer");
            _playerGo.transform.position = new Vector3(0f, 2f, 0f); // Spawn slightly above floor

            _cc = _playerGo.AddComponent<UnityEngine.CharacterController>();
            _cc.height = 2f;
            _cc.radius = 0.5f;

            _motor = _playerGo.AddComponent<CharacterMotor>();
            _motor.gravity = -9.81f;

            // Wait 2 frames for physics initial snaps and positioning
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_playerGo != null)
            {
                Object.Destroy(_playerGo);
            }
            if (_floorGo != null)
            {
                Object.Destroy(_floorGo);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CharacterMotor_MoveForward_PhysicallyTranslatesCharacter()
        {
            Vector3 startPos = _playerGo.transform.position;
            float duration = 0.5f;
            float elapsed = 0f;
            float speed = 5f;

            // Apply movement input over half a second
            while (elapsed < duration)
            {
                _motor.Move(Vector3.forward, speed);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Vector3 endPos = _playerGo.transform.position;
            float distanceMoved = endPos.z - startPos.z;

            // Ensure the character has physically moved in the Z direction
            Assert.Greater(distanceMoved, 0.5f, "Player failed to physically translate forward under Move call.");
        }

        [UnityTest]
        public IEnumerator CharacterMotor_JumpAndFall_CalculatesCorrectKinematics()
        {
            // 1. Wait until grounded
            float elapsed = 0f;
            while (!_motor.IsGrounded && elapsed < 2.0f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(_motor.IsGrounded, "Player failed to ground on the floor primitive.");

            // Record grounded position
            float groundedHeight = _playerGo.transform.position.y;

            // 2. Execute Jump
            _motor.Jump(10f);
            
            // Wait 5 frames to start ascension
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            float midJumpHeight = _playerGo.transform.position.y;
            Assert.Greater(midJumpHeight, groundedHeight + 0.1f, "Player failed to initiate upward vertical jump displacement.");

            // 3. Wait for gravity to pull the player back down
            elapsed = 0f;
            while (!_motor.IsGrounded && elapsed < 2.0f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(_motor.IsGrounded, "Player did not fall back to the ground after jumping.");
        }
    }
}
