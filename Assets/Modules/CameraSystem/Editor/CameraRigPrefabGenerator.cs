using System.IO;
using CameraSystem.Runtime;
using CharacterController.Runtime;
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace CameraSystem.Editor
{
    public enum CameraRigBodyType
    {
        ThirdPersonFollow,
        Transposer,
        FramingTransposer
    }

    public static class CameraRigPrefabGenerator
    {
        public static GameObject GenerateCameraRigPrefab(
            string prefabPath,
            CameraRigBodyType bodyType,
            float cameraDistance,
            Vector3 shoulderOffset,
            InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return null;

            EnsureAssetFolder(prefabPath);

            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.transform.SetParent(root.transform, false);

                var camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.Skybox;

                cameraGo.AddComponent<AudioListener>();
                cameraGo.AddComponent<CinemachineBrain>();
                cameraGo.AddComponent<CameraManager>();
                cameraGo.AddComponent<URPCameraStackLinker>();

                var virtualCameraGo = new GameObject("cm");
                virtualCameraGo.transform.SetParent(cameraGo.transform, false);

                var virtualCamera = virtualCameraGo.AddComponent<CinemachineVirtualCamera>();
                ConfigureBody(virtualCamera, bodyType, cameraDistance, shoulderOffset);

                virtualCameraGo.AddComponent<CinemachineCollider>();
                virtualCameraGo.AddComponent<CinemachineTargetLinker>();

                return PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, interactionMode);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static GameObject GenerateUICameraPrefab(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return null;

            EnsureAssetFolder(prefabPath);

            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var uiCamera = root.AddComponent<Camera>();
                uiCamera.clearFlags = CameraClearFlags.Depth;
                uiCamera.cullingMask = GetUILayerMask();
                uiCamera.orthographic = true;
                uiCamera.orthographicSize = 5f;
                uiCamera.nearClipPlane = -100f;
                uiCamera.farClipPlane = 100f;
                uiCamera.depth = 10f;
                uiCamera.allowHDR = false;
                uiCamera.allowMSAA = false;

                root.AddComponent<UICameraRegisterHook>();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static Transform EnsureCameraTargetSocket(Transform characterRoot, Vector3 localOffset, bool useUndo)
        {
            if (characterRoot == null)
                return null;

            var registry = characterRoot.GetComponentInChildren<CharacterSocketRegistry>(true);
            if (registry == null)
            {
                registry = useUndo
                    ? Undo.AddComponent<CharacterSocketRegistry>(characterRoot.gameObject)
                    : characterRoot.gameObject.AddComponent<CharacterSocketRegistry>();
            }

            registry.Refresh();
            if (registry.TryGet(CharacterSocketId.CameraTarget, out var socket) && socket != null)
            {
                if (useUndo)
                    Undo.RecordObject(socket, "Update CameraTarget Socket Position");

                socket.localPosition = localOffset;
                return socket;
            }

            var pivotGo = new GameObject("CameraTarget");
            if (useUndo)
            {
                Undo.RegisterCreatedObjectUndo(pivotGo, "Create CameraTarget Socket");
                Undo.SetTransformParent(pivotGo.transform, characterRoot, "Parent CameraTarget Socket to Character");
            }
            else
            {
                pivotGo.transform.SetParent(characterRoot, false);
            }

            var pivot = pivotGo.transform;
            if (useUndo)
                Undo.RecordObject(pivot, "Configure CameraTarget Socket");

            pivot.localPosition = localOffset;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            var socketComponent = pivot.GetComponent<CharacterSocket>();
            if (socketComponent == null)
            {
                socketComponent = useUndo
                    ? Undo.AddComponent<CharacterSocket>(pivot.gameObject)
                    : pivot.gameObject.AddComponent<CharacterSocket>();
            }

            socketComponent.Configure(CharacterSocketId.CameraTarget);
            registry.Refresh();
            return pivot;
        }

        public static bool HasCameraTargetSocket(Transform characterRoot)
        {
            if (characterRoot == null)
                return false;

            var registry = characterRoot.GetComponentInChildren<CharacterSocketRegistry>(true);
            return registry != null && registry.TryGet(CharacterSocketId.CameraTarget, out var socket) && socket != null;
        }

        public static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static void ConfigureBody(
            CinemachineVirtualCamera virtualCamera,
            CameraRigBodyType bodyType,
            float cameraDistance,
            Vector3 shoulderOffset)
        {
            switch (bodyType)
            {
                case CameraRigBodyType.ThirdPersonFollow:
                    var follow = virtualCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
                    follow.CameraDistance = cameraDistance;
                    follow.ShoulderOffset = shoulderOffset;
                    follow.Damping = new Vector3(0.1f, 0.1f, 0.1f);
                    break;
                case CameraRigBodyType.Transposer:
                    var transposer = virtualCamera.AddCinemachineComponent<CinemachineTransposer>();
                    transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                    transposer.m_FollowOffset = new Vector3(0f, 2f, -cameraDistance);
                    transposer.m_XDamping = 0.5f;
                    transposer.m_YDamping = 0.5f;
                    transposer.m_ZDamping = 0.5f;
                    break;
                case CameraRigBodyType.FramingTransposer:
                    var framing = virtualCamera.AddCinemachineComponent<CinemachineFramingTransposer>();
                    framing.m_CameraDistance = cameraDistance;
                    framing.m_XDamping = 0.5f;
                    framing.m_YDamping = 0.5f;
                    framing.m_ZDamping = 0.5f;
                    break;
            }
        }

        private static int GetUILayerMask()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 ? 1 << uiLayer : 0;
        }
    }
}
