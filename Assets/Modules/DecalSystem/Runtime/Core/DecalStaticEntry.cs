using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// DECAL STATIC ENTRY: The serialized primitive of the decal system.
    /// <para>
    /// Memory Layout: Optimized for 0-GC retrieval and fast spatial hashing.
    /// Total Size: ~340 bytes (varies by platform padding).
    /// </para>
    /// </summary>
    [System.Serializable]
    public struct DecalStaticEntry
    {
        /// <summary>
        /// The GPU-ready data block containing matrices and material properties.
        /// </summary>
        public DecalDataMini data;

        /// <summary>
        /// Global rendering order for stable overlap handling.
        /// </summary>
        public int sortingOrder;

        /// <summary>
        /// Layer mask used for runtime filtering (e.g., exclude from certain cameras).
        /// </summary>
        public int layerMask;

        /// <summary>
        /// Pre-computed world position for spatial hashing O(1) lookups.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Pre-computed bounding radius for conservative frustum culling.
        /// </summary>
        public float boundingRadius;

        /// <summary>
        /// Generic project-specific hook. 
        /// Can be used to store Terrain IDs, Surface types, or metadata indices.
        /// </summary>
        public float userData;
    }
}
