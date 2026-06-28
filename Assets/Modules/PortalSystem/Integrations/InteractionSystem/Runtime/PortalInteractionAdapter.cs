using InteractionSystem.Runtime;
using PortalSystem.Runtime;
using UnityEngine;

namespace PortalSystem.InteractionSystemIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PortalHub))]
    [AddComponentMenu("Portal System/Integrations/Portal Interaction Adapter")]
    public sealed class PortalInteractionAdapter : MonoBehaviour, IInteractable
    {
        [SerializeField]
        private int _interactionPriority = 100;

        private PortalHub _portalHub;

        public int InteractionPriority => _interactionPriority;
        public bool IsInteractable => _portalHub != null && _portalHub.HasValidDestination;

        private void Awake()
        {
            _portalHub = GetComponent<PortalHub>();
        }

        public void OnInteract(GameObject interactor)
        {
            if (_portalHub == null)
            {
                return;
            }

            _portalHub.TriggerTeleport();
        }
    }
}
