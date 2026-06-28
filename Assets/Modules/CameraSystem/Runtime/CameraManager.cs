using Cinemachine;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[assembly: InternalsVisibleTo("CameraSystem.Editor.Tests")]

namespace CameraSystem.Runtime
{
    /// <summary>
        /// Coordinates the active camera rig, registered targets, and Cinemachine input ownership.
    /// </summary>
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

        internal static bool IsVerboseLoggingEnabled => Instance != null && Instance._verboseLogging;

        public static void RegisterPlayer(Transform player)
        {
            if (player == null) return;
            PlayerTransform = player;
            if (Instance != null)
            {
                Instance.SetAllTargets(player);
            }
            OnPlayerRegistered?.Invoke(player);
            LogVerbose($"Player character registered: '{player.name}'");
        }

        public static void UnregisterPlayer(Transform player)
        {
            if (PlayerTransform == player)
            {
                PlayerTransform = null;
                CameraTargetTransform = null;
                PostProcessTargetTransform = null;
                OnPlayerUnregistered?.Invoke();
                LogVerbose("Player character unregistered.");
            }
        }

        public static void RegisterCameraUI(Camera uiCamera)
        {
            if (uiCamera == null) return;
            CameraUI = uiCamera;
            OnCameraUIRegistered?.Invoke(uiCamera);
            LogVerbose($"UI Camera registered: '{uiCamera.name}'");
        }

        public static void UnregisterCameraUI(Camera uiCamera)
        {
            if (CameraUI == uiCamera)
            {
                CameraUI = null;
                OnCameraUIUnregistered?.Invoke();
                LogVerbose("UI Camera unregistered.");
            }
        }

        private CinemachineBrain _brain;
        private CinemachineCore.AxisInputDelegate _previousInputAxisProvider;
        private CinemachineCore.AxisInputDelegate _activeInputAxisProvider;
        private bool _ownsInputAxisProvider;

        [Header("Runtime Ownership")]
        [Tooltip("When enabled, CameraManager owns CinemachineCore.GetInputAxis while this component is enabled.")]
        [SerializeField] private bool _overrideCinemachineInput = true;
        [Tooltip("When enabled, CameraManager may add missing Cinemachine/URP helper components to the resolved Camera at runtime.")]
        [SerializeField] private bool _autoConfigureCameraComponents = true;
        [Tooltip("When enabled, CameraSystem writes non-warning lifecycle and binding diagnostics to the Console.")]
        [SerializeField] private bool _verboseLogging;

        [Header("Orbit Settings")]
        [FormerlySerializedAs("mouseSensitivity")]
        [SerializeField] private float _mouseSensitivity = 0.15f;
        [FormerlySerializedAs("gamepadSensitivity")]
        [SerializeField] private float _gamepadSensitivity = 150f;
        [FormerlySerializedAs("minimumPitch")]
        [SerializeField] private float _minimumPitch = -35f;
        [FormerlySerializedAs("maximumPitch")]
        [SerializeField] private float _maximumPitch = 70f;

        private Transform _cameraTargetPivot;
        private float _currentYaw;
        private float _currentPitch;

        public float MouseSensitivity => _mouseSensitivity;
        public float GamepadSensitivity => _gamepadSensitivity;
        public float MinimumPitch => _minimumPitch;
        public float MaximumPitch => _maximumPitch;
        public bool OverrideCinemachineInput => _overrideCinemachineInput;
        public bool AutoConfigureCameraComponents => _autoConfigureCameraComponents;
        public bool VerboseLogging => _verboseLogging;

        [System.Obsolete("Use MouseSensitivity for reading and SetMouseSensitivity for writing.")]
        public float mouseSensitivity
        {
            get => _mouseSensitivity;
            set => SetMouseSensitivity(value);
        }

        [System.Obsolete("Use GamepadSensitivity for reading and SetGamepadSensitivity for writing.")]
        public float gamepadSensitivity
        {
            get => _gamepadSensitivity;
            set => SetGamepadSensitivity(value);
        }

        [System.Obsolete("Use MinimumPitch for reading and SetPitchLimits for writing.")]
        public float minimumPitch
        {
            get => _minimumPitch;
            set => SetPitchLimits(value, _maximumPitch);
        }

        [System.Obsolete("Use MaximumPitch for reading and SetPitchLimits for writing.")]
        public float maximumPitch
        {
            get => _maximumPitch;
            set => SetPitchLimits(_minimumPitch, value);
        }

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

            BindCameraComponents();

        }

        private void OnEnable()
        {
            AcquireInputAxisProvider();
        }

        private void OnDisable()
        {
            ReleaseInputAxisProvider();
        }

        private void OnDestroy()
        {
            ReleaseInputAxisProvider();

            if (Instance != this)
                return;

            ClearGlobalReferences();
        }

        public void SetMouseSensitivity(float value)
        {
            _mouseSensitivity = Mathf.Max(0f, value);
        }

        public void SetGamepadSensitivity(float value)
        {
            _gamepadSensitivity = Mathf.Max(0f, value);
        }

        public void SetPitchLimits(float minimum, float maximum)
        {
            if (minimum > maximum)
                (minimum, maximum) = (maximum, minimum);

            _minimumPitch = minimum;
            _maximumPitch = maximum;
            _currentPitch = Mathf.Clamp(_currentPitch, _minimumPitch, _maximumPitch);
        }

        public void SetOverrideCinemachineInput(bool value)
        {
            if (_overrideCinemachineInput == value)
                return;

            _overrideCinemachineInput = value;
            if (!isActiveAndEnabled)
                return;

            if (_overrideCinemachineInput)
                AcquireInputAxisProvider();
            else
                ReleaseInputAxisProvider();
        }

        public void SetAutoConfigureCameraComponents(bool value)
        {
            _autoConfigureCameraComponents = value;
        }

        public void SetVerboseLogging(bool value)
        {
            _verboseLogging = value;
        }

        private void BindCameraComponents()
        {
            Camera targetCamera = GetComponentInChildren<Camera>(true);
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
            {
                BindCameraComponents(targetCamera);
                return;
            }

            _brain = GetComponent<CinemachineBrain>();
            if (_brain == null && _autoConfigureCameraComponents)
                _brain = gameObject.AddComponent<CinemachineBrain>();

            if (_brain == null)
                Debug.LogWarning("<color=#FFCC00><b>[CameraManager]</b></color> No Camera component found and no local CinemachineBrain is available.");
            else
                Debug.LogWarning("<color=#FFCC00><b>[CameraManager]</b></color> No Camera component found in children. Bound local CinemachineBrain.");
        }

        private void BindCameraComponents(Camera targetCamera)
        {
            _brain = targetCamera.GetComponent<CinemachineBrain>();
            if (_brain == null && _autoConfigureCameraComponents)
                _brain = targetCamera.gameObject.AddComponent<CinemachineBrain>();

            if (_brain == null)
            {
                Debug.LogWarning($"<color=#FFCC00><b>[CameraManager]</b></color> Camera '{targetCamera.gameObject.name}' has no CinemachineBrain. Enable Auto Configure Camera Components or add it explicitly.");
            }
            else
            {
                LogVerbose($"CinemachineBrain bound to Camera on GameObject: '{targetCamera.gameObject.name}'.");
            }

            if (!_autoConfigureCameraComponents || targetCamera.GetComponent<URPCameraStackLinker>() != null)
                return;

            targetCamera.gameObject.AddComponent<URPCameraStackLinker>();
        }

        internal static void LogVerbose(string message)
        {
            if (!IsVerboseLoggingEnabled)
                return;

            Debug.Log($"<color=#3FB950><b>[CameraSystem]</b></color> {message}");
        }

        private void AcquireInputAxisProvider()
        {
            if (!_overrideCinemachineInput || _ownsInputAxisProvider)
                return;

            _activeInputAxisProvider = CustomInputAxisProvider;
            if (CinemachineCore.GetInputAxis != null && CinemachineCore.GetInputAxis.Equals(_activeInputAxisProvider))
            {
                _ownsInputAxisProvider = true;
                return;
            }

            _previousInputAxisProvider = CinemachineCore.GetInputAxis;
            CinemachineCore.GetInputAxis = _activeInputAxisProvider;
            _ownsInputAxisProvider = true;
        }

        private void ReleaseInputAxisProvider()
        {
            if (!_ownsInputAxisProvider)
                return;

            if (CinemachineCore.GetInputAxis != null && CinemachineCore.GetInputAxis.Equals(_activeInputAxisProvider))
                CinemachineCore.GetInputAxis = _previousInputAxisProvider;

            _previousInputAxisProvider = null;
            _activeInputAxisProvider = null;
            _ownsInputAxisProvider = false;
        }

        internal static void ResetForTests()
        {
            if (Instance != null)
                Instance.ReleaseInputAxisProvider();

            ClearGlobalReferences();
            OnPlayerRegistered = null;
            OnPlayerUnregistered = null;
            OnPostProcessTargetRegistered = null;
            OnCameraUIRegistered = null;
            OnCameraUIUnregistered = null;
        }

        private static void ClearGlobalReferences()
        {
            Instance = null;
            PlayerTransform = null;
            CameraTargetTransform = null;
            PostProcessTargetTransform = null;
            CameraUI = null;
            Shader.SetGlobalFloat(HasPostProcessTargetId, 0f);
        }

        private void Update()
        {
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
        /// Provides mouse and gamepad axis input for Cinemachine without depending on project-level input assets.
        /// </summary>
        private float CustomInputAxisProvider(string axisName)
        {
            float value = 0f;

            if (
                axisName == "Mouse X"
                || axisName == "Gamepad X"
                || axisName == "Joystick X"
                || axisName.Contains("Right Stick X")
            )
            {
                if (Mouse.current != null)
                {
                    // Only rotate from raw mouse delta while the cursor is captured.
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        value += Mouse.current.delta.x.ReadValue() * 0.05f;
                    }
                }
                if (Gamepad.current != null)
                {
                    value += Gamepad.current.rightStick.x.ReadValue() * _gamepadSensitivity * Time.deltaTime;
                }
                return value;
            }

            if (
                axisName == "Mouse Y"
                || axisName == "Gamepad Y"
                || axisName == "Joystick Y"
                || axisName.Contains("Right Stick Y")
            )
            {
                if (Mouse.current != null)
                {
                    // Only rotate from raw mouse delta while the cursor is captured.
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        value += Mouse.current.delta.y.ReadValue() * 0.05f;
                    }
                }
                if (Gamepad.current != null)
                {
                    value += Gamepad.current.rightStick.y.ReadValue() * _gamepadSensitivity * Time.deltaTime;
                }
                return value;
            }

            return 0f;
        }

        /// <summary>
        /// Applies scroll zoom to supported Cinemachine body components without reflection.
        /// </summary>
        private void HandleCameraZoom(float scroll)
        {
            var vCams = Object.FindObjectsByType<CinemachineVirtualCamera>(
                FindObjectsSortMode.None
            );
            foreach (var cam in vCams)
            {
                if (cam == null) continue;

                var thirdPersonFollow = cam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                if (thirdPersonFollow != null)
                {
                    float dist = thirdPersonFollow.CameraDistance;
                    thirdPersonFollow.CameraDistance = Mathf.Clamp(dist - scroll * 1.5f, 2f, 30f);
                }

                var framingTransposer = cam.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (framingTransposer != null)
                {
                    float dist = framingTransposer.m_CameraDistance;
                    framingTransposer.m_CameraDistance = Mathf.Clamp(dist - scroll * 1.5f, 2f, 30f);
                }

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

            if (Mouse.current != null)
            {
                bool canRotate = Cursor.lockState == CursorLockMode.Locked 
                                 || Mouse.current.leftButton.isPressed 
                                 || Mouse.current.rightButton.isPressed;

                if (canRotate)
                {
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                    yawInput += mouseDelta.x * _mouseSensitivity;
                    pitchInput += mouseDelta.y * _mouseSensitivity;
                }
            }

            if (Gamepad.current != null)
            {
                Vector2 stickInput = Gamepad.current.rightStick.ReadValue();
                if (stickInput.sqrMagnitude > 0.001f)
                {
                    yawInput += stickInput.x * _gamepadSensitivity * Time.deltaTime;
                    pitchInput += stickInput.y * _gamepadSensitivity * Time.deltaTime;
                }
            }

            _currentYaw += yawInput;
            _currentPitch -= pitchInput;
            _currentPitch = Mathf.Clamp(_currentPitch, _minimumPitch, _maximumPitch);

            // Use world rotation so character rotation does not feed back into the orbit pivot.
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
        /// Sets all virtual cameras to the target resolved from the character socket registry.
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
            
            LogVerbose($"All cameras linked to character socket target: '{_cameraTargetPivot.name}' at local {pivot.localPosition}. PostProcessTarget='{PostProcessTargetTransform?.name}'.");
        }
    }
}
