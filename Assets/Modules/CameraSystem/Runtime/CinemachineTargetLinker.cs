using UnityEngine;
using Cinemachine;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// Keeps a Cinemachine virtual camera linked to the registered camera target socket.
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
            CameraManager.LogVerbose($"Virtual camera '{_vCam.name}' bound to registered pivot on '{target.name}'.");
        }

        /// <summary>
        /// Allows bootstrap code to provide a target explicitly.
        /// </summary>
        public void SetTarget(Transform target)
        {
            LinkTarget(target);
        }
    }
}
