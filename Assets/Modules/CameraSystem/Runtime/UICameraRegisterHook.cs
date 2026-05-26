using UnityEngine;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// UI 摄像机自发现注册钩子 (UI Camera Register Hook)
    /// 挂载于 UI 摄像机上，在启用/禁用时自动向 CameraManager 进行注册/注销，实现 100% 优雅去耦。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-190)] // 在常规 UIManager 或 Canvas 唤醒前，提早完成摄像机注册
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
