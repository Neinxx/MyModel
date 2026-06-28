using UnityEngine;

namespace DecalMini
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ShaderMini/Decal Tire Track")]
    public class DecalTireTrackComponent : MonoBehaviour
    {
        [Header("Emitter")]
        [Tooltip("Optional wheel anchors. When empty, wheel positions are inferred from wheel count and spacing.")]
        public Transform[] wheelAnchors;
        public bool emitOnPlay = true;

        [Header("Track")]
        public DecalTireTrackModule tireTrackModule = new();

        private DecalTireTrackRenderer _renderer;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureRenderer();
            tireTrackModule?.ResetRuntime();
        }

        private void Update()
        {
            if (!Application.isPlaying || !emitOnPlay || tireTrackModule == null)
                return;

            EnsureRenderer();
            tireTrackModule.Tick(transform, wheelAnchors, _renderer, Time.deltaTime);
        }

        private void OnDisable()
        {
            tireTrackModule?.ResetRuntime();
            DisposeRenderer();
        }

        private void OnDestroy()
        {
            DisposeRenderer();
        }

        public void ClearTracks()
        {
            tireTrackModule?.ResetRuntime();
        }

        private void EnsureRenderer()
        {
            if (_renderer != null)
                return;

            _renderer = new DecalTireTrackRenderer(gameObject.name);
        }

        private void DisposeRenderer()
        {
            if (_renderer == null)
                return;

            _renderer.Dispose();
            _renderer = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            tireTrackModule?.DrawGizmos(transform, wheelAnchors);
        }
#endif
    }
}
