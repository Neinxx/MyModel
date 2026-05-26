using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CameraSystem.Runtime
{
    /// <summary>
    /// URP 摄像机堆栈链接器 (URP Camera Stack Linker)
    /// 负责将注册的 UI 摄像机自动堆叠至主摄像机 (Main Camera) 的 URP Camera Stack 中，摆脱反射和硬编码。
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
            
            // 确保主摄像机拥有 URP 额外的摄像机数据
            if (_mainCameraData == null)
            {
                _mainCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
        }

        private void OnEnable()
        {
            CameraManager.OnCameraUIRegistered += LinkCameraUI;
            CameraManager.OnCameraUIUnregistered += UnlinkCameraUI;

            // 🌟 黄金自愈：如果 UI 摄像机已经提早被注册，则立即进行堆叠绑定
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
        /// 精准且类型安全地将 UI 摄像机注入到主摄像机的 URP Stack 中
        /// </summary>
        public void LinkCameraUI(Camera uiCamera)
        {
            if (uiCamera == null || _mainCamera == null || _mainCameraData == null) return;
            if (uiCamera == _mainCamera) return;

            // 1. 获取或添加 UI 摄像机的 URP 附加数据并将其设为 Overlay
            var uiCameraData = uiCamera.GetComponent<UniversalAdditionalCameraData>();
            if (uiCameraData == null)
            {
                uiCameraData = uiCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            uiCameraData.renderType = CameraRenderType.Overlay;
            uiCameraData.renderPostProcessing = false; // UI 通常不需要场景后处理

            // 2. 将其安全注入主摄像机的 Stack
            if (!_mainCameraData.cameraStack.Contains(uiCamera))
            {
                _mainCameraData.cameraStack.Add(uiCamera);
                
                // 3. 完美剔除：从主摄像机的 Culling Mask 中移除 UI 层，防止双重渲染或深度冲突
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0)
                {
                    _mainCamera.cullingMask &= ~(1 << uiLayer);
                }

                Debug.Log($"<color=#3FB950><b>[URPCameraStackLinker]</b></color> Successfully stacked UI Camera '{uiCamera.name}' onto Main Camera '{_mainCamera.name}'.");
            }
        }

        /// <summary>
        /// 从主摄像机的 URP Stack 中安全移除 UI 摄像机
        /// </summary>
        public void UnlinkCameraUI()
        {
            if (_mainCameraData == null) return;

            // 清理已注销的 UI 摄像机
            for (int i = _mainCameraData.cameraStack.Count - 1; i >= 0; i--)
            {
                var cam = _mainCameraData.cameraStack[i];
                if (cam == null || cam.name == "CameraUI" || cam.name == "UICamera")
                {
                    _mainCameraData.cameraStack.RemoveAt(i);
                }
            }
            Debug.Log($"<color=#FF5252><b>[URPCameraStackLinker]</b></color> UI Camera cleared from Stack.");
        }
    }
}
