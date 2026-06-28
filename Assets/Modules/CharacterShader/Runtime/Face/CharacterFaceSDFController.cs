using UnityEngine;

namespace CharacterShader.Runtime.Features
{
    /// <summary>
    /// Tracks the character's Head Bone and pushes its orientation vectors globally to the GPU.
    /// This is strictly required for accurate SDF Face Shadows during character animation/rotation.
    /// </summary>
    [ExecuteInEditMode]
    [AddComponentMenu("Character Shader/Face SDF Controller")]
    public sealed class CharacterFaceSDFController : MonoBehaviour
    {
        [Tooltip("The Head Bone transform. If left empty, it uses the Transform this script is attached to.")]
        public Transform headBone;

        [Tooltip("Renderers that should receive the head orientation. Leave empty to auto-bind child renderers.")]
        public Renderer[] targetRenderers;

        [Tooltip("Automatically collect child renderers when Target Renderers is empty.")]
        public bool autoBindChildRenderers = true;

        [Tooltip("Also write global vectors as a compatibility fallback for legacy materials.")]
        public bool writeGlobalFallback = false;

        [Tooltip("Enable this if the character mesh has its face flipped or rotated relative to the bone by 180 degrees.")]
        public bool invertForward = false;

        [Tooltip("Enable this if the character mesh has its face flipped horizontally.")]
        public bool invertRight = false;

        private static readonly int HeadForwardWSSID = Shader.PropertyToID("_HeadForwardWS");
        private static readonly int HeadRightWSSID = Shader.PropertyToID("_HeadRightWS");

        private MaterialPropertyBlock propertyBlock;

        private void Reset()
        {
            if (headBone == null)
            {
                headBone = transform;
            }

            RefreshRenderers();
        }

        private void OnEnable()
        {
            RefreshRenderers();
            ApplyHeadVectors();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            RefreshRenderers();
        }

        private void LateUpdate()
        {
            ApplyHeadVectors();
        }

        public void RefreshRenderers()
        {
            if (autoBindChildRenderers && (targetRenderers == null || targetRenderers.Length == 0))
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void ApplyHeadVectors()
        {
            Transform target = headBone != null ? headBone : transform;

            Vector3 forward = target.forward;
            Vector3 right = target.right;

            if (invertForward) forward = -forward;
            if (invertRight) right = -right;

            if (writeGlobalFallback)
            {
                Shader.SetGlobalVector(HeadForwardWSSID, forward);
                Shader.SetGlobalVector(HeadRightWSSID, right);
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(HeadForwardWSSID, forward);
                propertyBlock.SetVector(HeadRightWSSID, right);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
