using UnityEngine;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// Registers this camera as the active UI camera while the component is enabled.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-190)]
    public class UICameraRegisterHook : MonoBehaviour
    {
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_camera != null)
            {
                CameraManager.RegisterCameraUI(_camera);
            }
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                CameraManager.UnregisterCameraUI(_camera);
            }
        }
    }
}
