using PlayerState.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModularDemo.Runtime
{
    public class PlayerStateBridge : MonoBehaviour, IPlayerStateReceiver
    {
        public PlayerStateSO playerState;

        public void BindPlayerState(PlayerStateSO state)
        {
            playerState = state;
            BindVisualControllers();
        }

        private void Start()
        {
            if (playerState == null)
                return;

            BindVisualControllers();
        }

        private void BindVisualControllers()
        {
            var visualControllers = GetComponentsInChildren<CharacterVisualController>(true);
            foreach (var vc in visualControllers)
            {
                if (vc != null && vc.playerState == null)
                {
                    vc.playerState = playerState;
                    // Toggle enabled to trigger OnEnable and setup the listeners correctly
                    if (vc.enabled)
                    {
                        vc.enabled = false;
                        vc.enabled = true;
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (playerState == null)
                return;
            playerState.lastPosition = transform.position;
            playerState.lastSceneName = SceneManager.GetActiveScene().name;
        }

        public void PickUpFeature(BaseFeatureSO feature)
        {
            playerState?.Equip(feature);
        }
    }
}
