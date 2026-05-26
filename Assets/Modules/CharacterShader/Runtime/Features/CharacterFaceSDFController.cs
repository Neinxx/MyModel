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

        [Tooltip("Enable this if the character mesh has its face flipped or rotated relative to the bone by 180 degrees.")]
        public bool invertForward = false;

        [Tooltip("Enable this if the character mesh has its face flipped horizontally.")]
        public bool invertRight = false;

        private static readonly int HeadForwardWSSID = Shader.PropertyToID("_HeadForwardWS");
        private static readonly int HeadRightWSSID = Shader.PropertyToID("_HeadRightWS");

        private void Reset()
        {
            if (headBone == null)
            {
                headBone = transform;
            }
        }

        private void LateUpdate()
        {
            Transform target = headBone != null ? headBone : transform;

            Vector3 forward = target.forward;
            Vector3 right = target.right;

            if (invertForward) forward = -forward;
            if (invertRight) right = -right;

            // Push to global shader variables for the SDF shadow calculation
            Shader.SetGlobalVector(HeadForwardWSSID, forward);
            Shader.SetGlobalVector(HeadRightWSSID, right);
        }
    }
}
