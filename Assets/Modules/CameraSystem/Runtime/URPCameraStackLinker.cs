using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// Keeps the registered UI camera in the URP camera stack for this camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class URPCameraStackLinker : MonoBehaviour
    {
        private Camera _mainCamera;
        private UniversalAdditionalCameraData _mainCameraData;

        private void Awake()
        {
            _mainCamera = GetComponent<Camera>();
            _mainCameraData = GetComponent<UniversalAdditionalCameraData>();
            
            if (_mainCameraData == null)
            {
                _mainCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
        }

        private void OnEnable()
        {
            CameraManager.OnCameraUIRegistered += LinkCameraUI;
            CameraManager.OnCameraUIUnregistered += UnlinkCameraUI;

            if (CameraManager.CameraUI != null)
            {
                LinkCameraUI(CameraManager.CameraUI);
            }
        }

        private void OnDisable()
        {
            CameraManager.OnCameraUIRegistered -= LinkCameraUI;
            CameraManager.OnCameraUIUnregistered -= UnlinkCameraUI;
        }

        /// <summary>
        /// Adds the UI camera to this camera's URP stack.
        /// </summary>
        public void LinkCameraUI(Camera uiCamera)
        {
            if (uiCamera == null || _mainCamera == null || _mainCameraData == null) return;
            if (uiCamera == _mainCamera) return;

            var uiCameraData = uiCamera.GetComponent<UniversalAdditionalCameraData>();
            if (uiCameraData == null)
            {
                uiCameraData = uiCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            uiCameraData.renderType = CameraRenderType.Overlay;
            uiCameraData.renderPostProcessing = false;

            if (!_mainCameraData.cameraStack.Contains(uiCamera))
            {
                _mainCameraData.cameraStack.Add(uiCamera);
                
                // Avoid rendering the UI layer from the base camera and the overlay camera at the same time.
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0)
                {
                    _mainCamera.cullingMask &= ~(1 << uiLayer);
                }

                CameraManager.LogVerbose($"Stacked UI Camera '{uiCamera.name}' onto Main Camera '{_mainCamera.name}'.");
            }
        }

        /// <summary>
        /// Removes registered UI cameras from this camera's URP stack.
        /// </summary>
        public void UnlinkCameraUI()
        {
            if (_mainCameraData == null) return;

            for (int i = _mainCameraData.cameraStack.Count - 1; i >= 0; i--)
            {
                var cam = _mainCameraData.cameraStack[i];
                if (cam == null || cam.name == "CameraUI" || cam.name == "UICamera")
                {
                    _mainCameraData.cameraStack.RemoveAt(i);
                }
            }
            CameraManager.LogVerbose("UI Camera cleared from Stack.");
        }
    }
}
