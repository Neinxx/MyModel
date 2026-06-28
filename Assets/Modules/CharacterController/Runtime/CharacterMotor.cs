using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

[assembly: InternalsVisibleTo("CharacterController.Editor.Tests")]

namespace CharacterController.Runtime
{
    /// <summary>
    /// 工业级角色马达 (Modular Character Motor)
    /// 纯粹的物理与位移实现层。
    /// </summary>
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public class CharacterMotor : MonoBehaviour, ICharacterMotor
    {
        [Header("Physics Settings")]
        [FormerlySerializedAs("gravity")]
        [SerializeField] private float _gravity = -9.81f;
        [FormerlySerializedAs("slopeLimit")]
        [Range(0f, 90f)]
        [SerializeField] private float _slopeLimit = 45f;
        [FormerlySerializedAs("stepOffset")]
        [Min(0f)]
        [SerializeField] private float _stepOffset = 0.3f;

        private UnityEngine.CharacterController _cc;
        private Vector3 _verticalVelocity;
        private Vector3 _horizontalMove;
        private bool _isGrounded;

        public bool IsGrounded => _isGrounded;
        public Vector3 Velocity => _cc != null ? _cc.velocity : Vector3.zero;
        public float Gravity => _gravity;
        public float SlopeLimit => _slopeLimit;
        public float StepOffset => _stepOffset;

        [System.Obsolete("Use Gravity for reading and SetGravity for writing.")]
        public float gravity
        {
            get => _gravity;
            set => _gravity = value;
        }

        [System.Obsolete("Use SlopeLimit for reading and SetSlopeLimit for writing.")]
        public float slopeLimit
        {
            get => _slopeLimit;
            set => SetSlopeLimit(value);
        }

        [System.Obsolete("Use StepOffset for reading and SetStepOffset for writing.")]
        public float stepOffset
        {
            get => _stepOffset;
            set => SetStepOffset(value);
        }

        private void Awake()
        {
            _cc = GetComponent<UnityEngine.CharacterController>();
            ApplyControllerSettings();
        }

        public void SetGravity(float value)
        {
            _gravity = value;
        }

        public void SetSlopeLimit(float value)
        {
            _slopeLimit = Mathf.Clamp(value, 0f, 90f);
            ApplyControllerSettings();
        }

        public void SetStepOffset(float value)
        {
            _stepOffset = Mathf.Max(0f, value);
            ApplyControllerSettings();
        }

        private void Update()
        {
            ApplyPhysicsAndMove();
        }

        public void Move(Vector3 direction, float speed)
        {
            // 缓存水平移动向量，在 Update 帧末合并执行，规避多次 Move 带来的碰撞开销与贴地抖动
            _horizontalMove = direction.normalized * speed;
        }

        public void Rotate(Vector3 targetDirection, float rotationSpeed)
        {
            if (targetDirection.magnitude < 0.1f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        public void Jump(float force)
        {
            if (_isGrounded)
            {
                _verticalVelocity.y = Mathf.Sqrt(force * -2f * _gravity);
            }
        }

        internal Vector3 HorizontalMoveForTests => _horizontalMove;
        internal Vector3 VerticalVelocityForTests => _verticalVelocity;

        internal void SetGroundedForTests(bool value)
        {
            _isGrounded = value;
        }

        private void ApplyPhysicsAndMove()
        {
            if (_cc == null)
                _cc = GetComponent<UnityEngine.CharacterController>();

            _isGrounded = _cc.isGrounded;
            if (_isGrounded && _verticalVelocity.y < 0)
            {
                _verticalVelocity.y = -2f; // 保持贴地
            }
            else
            {
                _verticalVelocity.y += _gravity * Time.deltaTime;
            }

            // 合并水平位移与重力垂直位移，单次调用 _cc.Move，极致性能且物理精准
            Vector3 combinedMove = _horizontalMove + _verticalVelocity;
            _cc.Move(combinedMove * Time.deltaTime);

            // 每帧末尾清除上一帧的水平移动输入，防止不间断漂移
            _horizontalMove = Vector3.zero;
        }

        private void ApplyControllerSettings()
        {
            if (_cc == null)
                return;

            _cc.slopeLimit = _slopeLimit;
            _cc.stepOffset = _stepOffset;
        }
    }
}
