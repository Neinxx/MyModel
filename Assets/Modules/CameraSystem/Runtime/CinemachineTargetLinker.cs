using UnityEngine;
using Cinemachine;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// 自动目标链接器 (Automatic Target Linker)
    /// 用于将虚拟相机自动关联到场景中的特定标签物体（如 Player）。
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class CinemachineTargetLinker : MonoBehaviour
    {
        private CinemachineVirtualCamera _vCam;

        private void Awake()
        {
            _vCam = GetComponent<CinemachineVirtualCamera>();
        }

        private void OnEnable()
        {
            CameraManager.OnPlayerRegistered += OnPlayerRegistered;
            CameraManager.OnPlayerUnregistered += OnPlayerUnregistered;

            // 🌟 黄金自愈：如果玩家已经提早被注册，则立即吸附绑定
            if (CameraManager.PlayerTransform != null)
            {
                LinkTarget(CameraManager.PlayerTransform);
            }
        }

        private void OnDisable()
        {
            CameraManager.OnPlayerRegistered -= OnPlayerRegistered;
            CameraManager.OnPlayerUnregistered -= OnPlayerUnregistered;
        }

        private void OnPlayerRegistered(Transform player)
        {
            LinkTarget(player);
        }

        private void OnPlayerUnregistered()
        {
            if (_vCam != null)
            {
                _vCam.Follow = null;
                _vCam.LookAt = null;
            }
        }

        public void LinkTarget(Transform target)
        {
            if (target == null) return;
            if (_vCam == null) _vCam = GetComponent<CinemachineVirtualCamera>();

            var pivot = CameraTargetResolver.ResolveCameraTarget(target);
            if (pivot == null)
            {
                _vCam.Follow = null;
                _vCam.LookAt = null;
                return;
            }

            _vCam.Follow = pivot;
            _vCam.LookAt = pivot;
            Debug.Log($"<color=#FF8B8B><b>[Camera]</b></color> Elegant and decoupled target linking: '{_vCam.name}' bound to registered pivot on '{target.name}'.");
        }

        /// <summary>
        /// 允许外部显式注入目标 (保持与旧有引导模块或 WorldScene 的接口兼容性)
        /// </summary>
        public void SetTarget(Transform target)
        {
            LinkTarget(target);
        }
    }
}
