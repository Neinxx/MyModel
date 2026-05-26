using CharacterController.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModularDemo.Runtime
{
    /// <summary>
    /// 玩家大脑实现 (Elden Ring Style Player Character Brain)
    /// 集成 Unity 新版 Input System，负责将玩家的硬件输入转化为 Motor 的物理指令。
    /// 实现了类似《艾尔登法环》的自由相机视角相对移动与角色朝向控制，并自适应支持键鼠和手柄。
    /// </summary>
    public class PlayerCharacterBrain : CharacterBrain, ICharacterAnimatorReceiver
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 10f;
        public float jumpForce = 5f;

        [Header("Optional Modules")]
        [Tooltip("如果绑定了动画模块，Brain 会在特定动作时触发动画表现。")]
        public CharacterAnimator characterAnimator;

        [Header("Input Action Asset")]
        public InputActionAsset inputAsset;
        private PlayerInputControls _controls;
        private InputAction _moveAction;
        private InputAction _jumpAction;

        private Vector2 _moveInput;

        protected override void Awake()
        {
            base.Awake();

            // 支持逻辑与美术分离：尝试在子物体中寻找动画层
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<CharacterAnimator>();
            }

            SetupInput();
        }

        private void Start()
        {
            // 自动隐藏并锁定鼠标指针（动作RPG标准游玩模式）
            LockCursor();
        }

        /// <summary>
        /// 动态绑定美术表现层
        /// </summary>
        public void BindAnimator(CharacterAnimator animator)
        {
            characterAnimator = animator;
            Debug.Log(
                "<color=#BC8CFF><b>[PlayerBrain]</b></color> Visual Animator dynamically bound."
            );
        }

        private void SetupInput()
        {
            // 1. 优先尝试使用生成后的 C# Wrapper (高效率、强类型方案)
            if (inputAsset == null)
            {
                try
                {
                    _controls = new PlayerInputControls();
                    _controls.Enable();
                    _moveAction = _controls.Player.Move;
                    _jumpAction = _controls.Player.Jump;
                    if (_jumpAction != null)
                        _jumpAction.performed += OnJumpPerformed;

                    Debug.Log(
                        "<color=#3FB950><b>[PlayerBrain]</b></color> Input initialized via type-safe PlayerInputControls wrapper."
                    );
                    return;
                }
                catch (System.Exception)
                {
                    Debug.Log(
                        "<color=#FFCC00><b>[PlayerCharacterBrain]</b></color> InputActionAsset is not assigned. Falling back to direct hardware input (compatible with both legacy and new Input Systems)."
                    );
                    return;
                }
            }

            // 2. 尝试使用 Inspector 分配 of Asset
            var playerMap = inputAsset.FindActionMap("Player");
            if (playerMap != null)
            {
                _moveAction = playerMap.FindAction("Move");
                _jumpAction = playerMap.FindAction("Jump");

                if (_jumpAction != null)
                {
                    _jumpAction.performed += OnJumpPerformed;
                }

                playerMap.Enable();
                Debug.Log(
                    "<color=#3FB950><b>[PlayerBrain]</b></color> Input initialized via assigned InputActionAsset."
                );
            }
        }

        private void OnDestroy()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJumpPerformed;
            }

            if (_controls != null)
            {
                _controls.Disable();
                _controls.Dispose();
            }
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            // 向物理马达下发跳跃指令
            motor.Jump(jumpForce);

            // 向视觉表现层下发跳跃表现指令 (如果存在)
            if (characterAnimator != null)
            {
                characterAnimator.TriggerJump();
            }
        }

        protected override void Update()
        {
            // 处理鼠标指针锁定与释放逻辑
            HandleCursorLocking();

            base.Update();
        }

        private void HandleCursorLocking()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // 按下 Escape 释放鼠标指针（便于调试、操作UI菜单等）
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                UnlockCursor();
            }

            // 点击游戏窗口时重新锁定并隐藏鼠标指针
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        protected override void ProcessInput()
        {
            Vector2 keyboardInput = Vector2.zero;

            // 1. 读取键鼠或手柄 Action Map 输入
            if (_moveAction != null)
            {
                keyboardInput = _moveAction.ReadValue<Vector2>();
            }
            else
            {
                // 自愈型输入自适应 fallback
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    var move = Vector2.zero;
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                        move.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                        move.y -= 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                        move.x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                        move.x += 1f;
                    keyboardInput = move;

                    if (keyboard.spaceKey.wasPressedThisFrame)
                    {
                        OnJumpPerformed(default);
                    }
                }
            }

            _moveInput = keyboardInput;

            // 2. 模拟手柄输入 Fallback 逻辑 (如果不使用 C# Action Wrapper)
            if (_moveAction == null && Gamepad.current != null)
            {
                var padInput = Gamepad.current.leftStick.ReadValue();
                if (padInput.sqrMagnitude > 0.01f)
                {
                    _moveInput = padInput;
                }

                if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    OnJumpPerformed(default);
                }
            }

            // 3. 计算以相机视角为基准的水平移动方向
            Vector3 moveDirection = Vector3.zero;
            float inputMagnitude = Mathf.Clamp01(_moveInput.magnitude);

            if (_moveInput.sqrMagnitude > 0.01f)
            {
                // 标准动作游戏相对相机位移计算模式：
                // W/LStickUp 向相机视口前方跑，S/LStickDown 向相机视口后方跑，A/D 向相机视口左右横向跑
                Vector3 camForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                Vector3 camRight = Camera.main != null ? Camera.main.transform.right : transform.right;

                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                moveDirection = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
            }

            // 4. 向物理马达下发移动和旋转指令
            motor.Move(moveDirection, moveSpeed * inputMagnitude);

            // 埃尔登法环风格核心细节：
            // 只有当存在实质性移动输入时，角色才平滑转向目标运动方向；
            // 当角色静止（无移动输入）时，不允许调用 Rotate 转向，让摄像机在角色周围进行完全自由的环绕旋转而不会带动角色转动。
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                motor.Rotate(moveDirection, rotationSpeed);
            }
        }
    }
}
