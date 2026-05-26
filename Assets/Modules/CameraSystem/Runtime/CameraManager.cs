using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// 全局摄像机管理器 (Universal Camera Manager)
    /// 负责管理 CinemachineBrain 以及全局摄像机状态。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CinemachineBrain))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        public static Transform PlayerTransform { get; private set; }
        public static Transform CameraTargetTransform { get; private set; }
        public static Transform PostProcessTargetTransform { get; private set; }
        public static event System.Action<Transform> OnPlayerRegistered;
        public static event System.Action OnPlayerUnregistered;
        public static event System.Action<Transform> OnPostProcessTargetRegistered;
        private static readonly int PostProcessTargetPositionId = Shader.PropertyToID("_CameraPostProcessTargetPosition");
        private static readonly int HasPostProcessTargetId = Shader.PropertyToID("_CameraHasPostProcessTarget");

        public static Camera CameraUI { get; private set; }
        public static event System.Action<Camera> OnCameraUIRegistered;
        public static event System.Action OnCameraUIUnregistered;

        public static void RegisterPlayer(Transform player)
        {
            if (player == null) return;
            PlayerTransform = player;
            if (Instance != null)
            {
                Instance.SetAllTargets(player);
            }
            OnPlayerRegistered?.Invoke(player);
            Debug.Log($"<color=#3FB950><b>[CameraManager]</b></color> Player character registered: '{player.name}'");
        }

        public static void UnregisterPlayer(Transform player)
        {
            if (PlayerTransform == player)
            {
                PlayerTransform = null;
                CameraTargetTransform = null;
                PostProcessTargetTransform = null;
                OnPlayerUnregistered?.Invoke();
                Debug.Log($"<color=#8A8A8A><b>[CameraManager]</b></color> Player character unregistered.");
            }
        }

        public static void RegisterCameraUI(Camera uiCamera)
        {
            if (uiCamera == null) return;
            CameraUI = uiCamera;
            OnCameraUIRegistered?.Invoke(uiCamera);
            Debug.Log($"<color=#3FB950><b>[CameraManager]</b></color> UI Camera registered: '{uiCamera.name}'");
        }

        public static void UnregisterCameraUI(Camera uiCamera)
        {
            if (CameraUI == uiCamera)
            {
                CameraUI = null;
                OnCameraUIUnregistered?.Invoke();
                Debug.Log($"<color=#8A8A8A><b>[CameraManager]</b></color> UI Camera unregistered.");
            }
        }

        private CinemachineBrain _brain;

        [Header("Orbit Settings")]
        public float mouseSensitivity = 0.15f;
        public float gamepadSensitivity = 150f;
        public float minimumPitch = -35f;
        public float maximumPitch = 70f;

        private Transform _cameraTargetPivot;
        private float _currentYaw;
        private float _currentPitch;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying)
            {
                var persistentRoot = transform.root;
                if (persistentRoot.parent != null)
                    persistentRoot.SetParent(null, true);

                DontDestroyOnLoad(persistentRoot.gameObject);
            }

            // 🌟 黄金级自愈修复：寻找真正附带 Unity Camera 组件的 GameObject 并挂载 CinemachineBrain
            Camera targetCamera = GetComponentInChildren<Camera>(true);
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                _brain = targetCamera.GetComponent<CinemachineBrain>();
                if (_brain == null)
                {
                    _brain = targetCamera.gameObject.AddComponent<CinemachineBrain>();
                }
                Debug.Log($"<color=#3FB950><b>[CameraManager]</b></color> CinemachineBrain successfully bound to Camera on GameObject: '{targetCamera.gameObject.name}'.");

                // 🌟 Auto-attach URPCameraStackLinker to the Main Camera
                if (targetCamera.GetComponent<URPCameraStackLinker>() == null)
                {
                    targetCamera.gameObject.AddComponent<URPCameraStackLinker>();
                }
            }
            else
            {
                // Fallback locally
                _brain = GetComponent<CinemachineBrain>();
                if (_brain == null)
                    _brain = gameObject.AddComponent<CinemachineBrain>();
                Debug.LogWarning("<color=#FFCC00><b>[CameraManager]</b></color> No Camera component found in children. Attached CinemachineBrain locally.");
            }

            // 核心功能：接管 Cinemachine 所有轴向输入，完美融合鼠标左右键拖拽与手柄右摇杆
            CinemachineCore.GetInputAxis = CustomInputAxisProvider;
        }

        private void Update()
        {
            // 获取鼠标滚轮增量
            float scroll = 0f;
            if (Mouse.current != null)
            {
                scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
            }

            if (Mathf.Abs(scroll) > 0.001f)
            {
                HandleCameraZoom(scroll);
            }
        }

        /// <summary>
        /// 劫持 Cinemachine 对 Mouse/Gamepad 轴的查询，实现高度定制化的 UX
        /// 支持鼠标按住左右键拖拽旋转相机，并完美融合手柄右摇杆（Right Stick）直接推动旋转相机。
        /// 🌟 遵循架构设计：作为核心通用模块，本类与高层 Demo 键位资源解耦，零依赖编译。
        /// </summary>
        private float CustomInputAxisProvider(string axisName)
        {
            float value = 0f;

            // 1. 处理 X 轴水平旋转（融合鼠标 Delta 与手柄右摇杆）
            if (
                axisName == "Mouse X"
                || axisName == "Gamepad X"
                || axisName == "Joystick X"
                || axisName.Contains("Right Stick X")
            )
            {
                if (Mouse.current != null)
                {
                    // Elden Ring 风格：只有在鼠标指针被锁定时才进行自由环绕旋转（防止呼出UI时相机旋转）
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        value += Mouse.current.delta.x.ReadValue() * 0.05f;
                    }
                }
                if (Gamepad.current != null)
                {
                    // 🌟 黄金级物理修复：手柄右摇杆为持续率输入，必须乘以 Time.deltaTime 进行帧率无关的平滑传动
                    value += Gamepad.current.rightStick.x.ReadValue() * 150f * Time.deltaTime;
                }
                return value;
            }

            // 2. 处理 Y 轴垂直旋转（融合鼠标 Delta 与手柄右摇杆）
            if (
                axisName == "Mouse Y"
                || axisName == "Gamepad Y"
                || axisName == "Joystick Y"
                || axisName.Contains("Right Stick Y")
            )
            {
                if (Mouse.current != null)
                {
                    // Elden Ring 风格：只有在鼠标指针被锁定时才进行自由环绕旋转
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        value += Mouse.current.delta.y.ReadValue() * 0.05f;
                    }
                }
                if (Gamepad.current != null)
                {
                    // 🌟 黄金级物理修复：手柄右摇杆为持续率输入，必须乘以 Time.deltaTime 进行帧率无关的平滑传动
                    value += Gamepad.current.rightStick.y.ReadValue() * 150f * Time.deltaTime;
                }
                return value;
            }

            return 0f;
        }

        /// <summary>
        /// 类型安全地进行视角拉远拉近 (Zoom)，不再依赖反射。
        /// 支持 Cinemachine3rdPersonFollow, CinemachineFramingTransposer, 以及 CinemachineTransposer 组件。
        /// </summary>
        private void HandleCameraZoom(float scroll)
        {
            var vCams = Object.FindObjectsByType<CinemachineVirtualCamera>(
                FindObjectsSortMode.None
            );
            foreach (var cam in vCams)
            {
                if (cam == null) continue;

                // 1. 适配 Cinemachine3rdPersonFollow (v3 风格或 3rdPerson Orbit)
                var thirdPersonFollow = cam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                if (thirdPersonFollow != null)
                {
                    float dist = thirdPersonFollow.CameraDistance;
                    thirdPersonFollow.CameraDistance = Mathf.Clamp(dist - scroll * 1.5f, 2f, 30f);
                }

                // 2. 适配 CinemachineFramingTransposer (2D/3D Framing)
                var framingTransposer = cam.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (framingTransposer != null)
                {
                    float dist = framingTransposer.m_CameraDistance;
                    framingTransposer.m_CameraDistance = Mathf.Clamp(dist - scroll * 1.5f, 2f, 30f);
                }

                // 3. 适配 CinemachineTransposer (Elden Ring 风格常用，如 cm 预制体配置)
                var transposer = cam.GetCinemachineComponent<CinemachineTransposer>();
                if (transposer != null)
                {
                    Vector3 offset = transposer.m_FollowOffset;
                    float currentDist = offset.magnitude;
                    if (currentDist > 0.001f)
                    {
                        float newDist = Mathf.Clamp(currentDist - scroll * 1.5f, 2f, 30f);
                        transposer.m_FollowOffset = offset * (newDist / currentDist);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            UpdatePostProcessTargetGlobals();

            if (_cameraTargetPivot == null) return;

            float yawInput = 0f;
            float pitchInput = 0f;

            // 1. 读取鼠标输入
            if (Mouse.current != null)
            {
                // Elden Ring 风格：支持鼠标锁定模式或按住鼠标左右键拖拽旋转
                bool canRotate = Cursor.lockState == CursorLockMode.Locked 
                                 || Mouse.current.leftButton.isPressed 
                                 || Mouse.current.rightButton.isPressed;

                if (canRotate)
                {
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                    yawInput += mouseDelta.x * mouseSensitivity;
                    pitchInput += mouseDelta.y * mouseSensitivity;
                }
            }

            // 2. 读取手柄输入
            if (Gamepad.current != null)
            {
                Vector2 stickInput = Gamepad.current.rightStick.ReadValue();
                if (stickInput.sqrMagnitude > 0.001f)
                {
                    // 手柄右摇杆为持续率输入，必须乘以 Time.deltaTime 进行帧率无关平滑
                    yawInput += stickInput.x * gamepadSensitivity * Time.deltaTime;
                    pitchInput += stickInput.y * gamepadSensitivity * Time.deltaTime;
                }
            }

            // 3. 累加角度并限位
            _currentYaw += yawInput;
            _currentPitch -= pitchInput; // Y轴反向
            _currentPitch = Mathf.Clamp(_currentPitch, minimumPitch, maximumPitch);

            // 4. 应用到枢纽的绝对世界旋转，保持相对父物体的旋转独立性 (防止角色转动时相机跟着乱转)
            _cameraTargetPivot.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        }

        private static void UpdatePostProcessTargetGlobals()
        {
            if (PostProcessTargetTransform == null)
            {
                Shader.SetGlobalFloat(HasPostProcessTargetId, 0f);
                return;
            }

            var position = PostProcessTargetTransform.position;
            Shader.SetGlobalFloat(HasPostProcessTargetId, 1f);
            Shader.SetGlobalVector(PostProcessTargetPositionId, new Vector4(position.x, position.y, position.z, 1f));
        }

        /// <summary>
        /// 强制设置所有虚拟相机的目标，目标必须由 CharacterSocketRegistry 提供。
        /// </summary>
        public void SetAllTargets(Transform target)
        {
            if (target == null) return;

            var pivot = CameraTargetResolver.ResolveCameraTarget(target);
            if (pivot == null)
            {
                _cameraTargetPivot = null;
                CameraTargetTransform = null;
                PostProcessTargetTransform = CameraTargetResolver.ResolvePostProcessTarget(target);
                OnPostProcessTargetRegistered?.Invoke(PostProcessTargetTransform);
                return;
            }

            _cameraTargetPivot = pivot;
            CameraTargetTransform = pivot;
            PostProcessTargetTransform = CameraTargetResolver.ResolvePostProcessTarget(target);
            OnPostProcessTargetRegistered?.Invoke(PostProcessTargetTransform);

            // 初始化角度
            Vector3 angles = pivot.eulerAngles;
            _currentYaw = angles.y;
            _currentPitch = angles.x;

            var vCams = Object.FindObjectsByType<CinemachineVirtualCamera>(
                FindObjectsSortMode.None
            );
            foreach (var cam in vCams)
            {
                cam.Follow = _cameraTargetPivot;
                cam.LookAt = _cameraTargetPivot;
            }
            
            Debug.Log($"<color=#3FB950><b>[CameraManager]</b></color> All cameras linked to character socket target: '{_cameraTargetPivot.name}' at local {pivot.localPosition}. PostProcessTarget='{PostProcessTargetTransform?.name}'.");
        }
    }
}
