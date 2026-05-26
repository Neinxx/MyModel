using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// DECAL LEVEL DATA: A container asset for pre-baked decal information.
    /// <para>
    /// This asset acts as a high-density binary buffer for static scene decals.
    /// It is designed for fast loading/unloading during scene transitions.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "DecalLevelData", menuName = "Decal System/Core/Level Data")]
    public class DecalLevelDataMini : ScriptableObject
    {
        /// <summary>
        /// Human-readable name of the originating scene.
        /// </summary>
        public string sceneName;

        /// <summary>
        /// The collection of serialized decal entries.
        /// Each entry is a self-contained unit of rendering and spatial data.
        /// </summary>
        public List<DecalStaticEntry> entries = new();
    }
}
