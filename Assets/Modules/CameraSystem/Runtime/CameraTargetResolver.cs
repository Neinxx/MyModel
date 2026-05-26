using CharacterController.Runtime;
using UnityEngine;

namespace CameraSystem.Runtime
{
    internal static class CameraTargetResolver
    {
        public static Transform ResolveCameraTarget(Transform characterRoot)
        {
            if (characterRoot == null)
                return null;

            var registry = characterRoot.GetComponentInChildren<CharacterSocketRegistry>(true);
            if (registry != null && registry.TryGet(CharacterSocketId.CameraTarget, out var socket) && socket != null)
                return socket;

            Debug.LogWarning(
                $"[CameraTargetResolver] Character '{characterRoot.name}' is missing CharacterSocketId.CameraTarget. " +
                "Camera binding requires the new CharacterSocketRegistry protocol.");
            return null;
        }

        public static Transform ResolvePostProcessTarget(Transform characterRoot)
        {
            if (characterRoot == null)
                return null;

            var registry = characterRoot.GetComponentInChildren<CharacterSocketRegistry>(true);
            if (registry != null)
            {
                if (registry.TryGet(CharacterSocketId.VisualRoot, out var visualRoot) && visualRoot != null)
                    return visualRoot;

                if (registry.TryGet(CharacterSocketId.Body, out var body) && body != null)
                    return body;

                if (registry.TryGet(CharacterSocketId.Root, out var root) && root != null)
                    return root;
            }
            Debug.LogWarning(
                $"[CameraTargetResolver] Character '{characterRoot.name}' is missing post-process sockets. " +
                "Expected VisualRoot, Body, or Root in CharacterSocketRegistry.");
            return null;
        }
    }
}
