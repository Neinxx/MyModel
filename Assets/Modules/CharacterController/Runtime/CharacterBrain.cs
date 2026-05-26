using UnityEngine;

namespace CharacterController.Runtime
{
    /// <summary>
    /// 角色大脑基类 (Abstract Character Brain)
    /// 负责接收输入并将指令下发给 Motor。
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public abstract class CharacterBrain : MonoBehaviour
    {
        protected ICharacterMotor motor;

        protected virtual void Awake()
        {
            motor = GetComponent<CharacterMotor>();
        }

        protected abstract void ProcessInput();

        protected virtual void Update()
        {
            motor ??= GetComponent<CharacterMotor>();
            if (motor != null)
                ProcessInput();
        }
    }
}
