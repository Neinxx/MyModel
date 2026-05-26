using UnityEngine;

namespace CharacterShader.Runtime
{
    [CreateAssetMenu(fileName = "RampArrayConfig", menuName = "Character Shader/Ramp Array Config")]
    public class RampArrayConfig : ScriptableObject
    {
        [Tooltip("The material that will receive the real-time preview.")]
        public Material previewMaterial;

        [Tooltip("The resolution of the generated ramp texture. 256 is standard for light ramps.")]
        public int resolution = 256;

        [Tooltip("Gradients for the 8 Material IDs.")]
        public Gradient[] ramps = new Gradient[8];

        public RampArrayConfig()
        {
            // Initialize with default standard ramps
            for (int i = 0; i < 8; i++)
            {
                ramps[i] = new Gradient();
                ramps[i].SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.black, 0.45f), new GradientColorKey(Color.white, 0.55f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
                );
            }
        }
    }
}
