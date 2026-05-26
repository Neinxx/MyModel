using UnityEngine;

namespace CharacterController.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Character/Character Socket")]
    public sealed class CharacterSocket : MonoBehaviour
    {
        [SerializeField] private CharacterSocketId id = CharacterSocketId.Custom;
        [SerializeField] private string customId;

        public CharacterSocketId Id => id;
        public string CustomId => customId;

        public void Configure(CharacterSocketId socketId, string customSocketId = null)
        {
            id = socketId;
            customId = customSocketId;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.28f);
        }
#endif
    }
}
