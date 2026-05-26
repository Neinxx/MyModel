using UnityEngine;

namespace CharacterController.Runtime
{
    /// <summary>
    /// 角色马达接口 (Modular Motor Interface)
    /// 定义了角色如何移动，但不关心移动是由谁（玩家/AI）触发的。
    /// </summary>
    public interface ICharacterMotor
    {
        void Move(Vector3 direction, float speed);
        void Rotate(Vector3 targetDirection, float rotationSpeed);
        void Jump(float force);
        
        bool IsGrounded { get; }
        Vector3 Velocity { get; }
    }
}
