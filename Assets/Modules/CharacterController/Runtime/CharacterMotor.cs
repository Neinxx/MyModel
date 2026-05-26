using UnityEngine;

namespace CharacterController.Runtime
{
    /// <summary>
    /// 工业级角色马达 (Modular Character Motor)
    /// 纯粹的物理与位移实现层。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public class CharacterMotor : MonoBehaviour, ICharacterMotor
    {
        [Header("Physics Settings")]
        public float gravity = -9.81f;
        public float slopeLimit = 45f;
        public float stepOffset = 0.3f;

        private UnityEngine.CharacterController _cc;
        private Vector3 _verticalVelocity;
        private Vector3 _horizontalMove;
        private bool _isGrounded;

        public bool IsGrounded => _isGrounded;
        public Vector3 Velocity => _cc.velocity;

        private void Awake()
        {
            _cc = GetComponent<UnityEngine.CharacterController>();
            _cc.slopeLimit = slopeLimit;
            _cc.stepOffset = stepOffset;
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
                _verticalVelocity.y = Mathf.Sqrt(force * -2f * gravity);
            }
        }

        private void ApplyPhysicsAndMove()
        {
            _isGrounded = _cc.isGrounded;
            if (_isGrounded && _verticalVelocity.y < 0)
            {
                _verticalVelocity.y = -2f; // 保持贴地
            }
            else
            {
                _verticalVelocity.y += gravity * Time.deltaTime;
            }

            // 合并水平位移与重力垂直位移，单次调用 _cc.Move，极致性能且物理精准
            Vector3 combinedMove = _horizontalMove + _verticalVelocity;
            _cc.Move(combinedMove * Time.deltaTime);

            // 每帧末尾清除上一帧的水平移动输入，防止不间断漂移
            _horizontalMove = Vector3.zero;
        }
    }
}
