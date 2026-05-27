using UnityEngine;

namespace CharacterShader.Runtime
{
    [CreateAssetMenu(fileName = "MatCapArrayConfig", menuName = "Character Shader/MatCap Array Config")]
    public class MatCapArrayConfig : ScriptableObject
    {
        [Tooltip("The material that will receive the real-time preview.")]
        public Material previewMaterial;

        [Tooltip("The resolution of the generated MatCap texture array. 256 or 512 is standard.")]
        public int resolution = 256;

        [Tooltip("MatCap textures for the 8 Material IDs.")]
        public Texture2D[] matcaps = new Texture2D[8];
    }
}
