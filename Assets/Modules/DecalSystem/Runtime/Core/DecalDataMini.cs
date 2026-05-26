using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// HLSL STRUCT TAG: Marker attribute used by the automated shader-generator to sync C# and HLSL structures.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class HLSLStructAttribute : Attribute { }

    /// <summary>
    /// DECAL RENDER DATA (GPU Interface): The primary data structure for the rendering kernel.
    /// <para>
    /// Design constraints:
    /// 1. 320-byte alignment (Stride): Strict adherence to 16-byte boundaries for efficient GPU bus transfer.
    /// 2. Row-Major Matrices: Explicit row storage to avoid HLSL float4x4 default transpose conflicts.
    /// 3. Parameter Packing: Vectors are densely packed to minimize the number of constant buffer slots.
    /// </para>
    /// </summary>
    [HLSLStruct]
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DecalDataMini
    {
        /// <summary>
        /// Total stride in bytes. Must match the StructuredBuffer stride in the shader.
        /// </summary>
        public const int Stride = 320;

        // ========================================================================
        // 1. TRANSFORMATION MATRICES [128 bytes]
        // ========================================================================

        // World-To-Decal (WTD): Used to transform world-space pixel position into unit-cube decal space.
        public Vector4 wtd0, wtd1, wtd2, wtd3;

        // Decal-To-World (DTW): Used to extract world-space position and rotation for sorting/culling.
        public Vector4 dtw0, dtw1, dtw2, dtw3;

        // ========================================================================
        // 2. CORE RENDERING PARAMETERS [32 bytes]
        // ========================================================================

        /// <summary>Global tint color. W component stores final opacity.</summary>
        public Vector4 color;

        /// <summary>UV Tiling (X,Y) and Offset (Z,W).</summary>
        public Vector4 uvScaleOffset;

        // ========================================================================
        // 3. PACKED ANIMATION & FADE LOGIC [48 bytes]
        // ========================================================================

        /// <summary>
        /// Fade Parameters:
        /// X: Angle Fade Start (dot product threshold),
        /// Y: Distance Fade multiplier,
        /// Z: Texture Array Index,
        /// W: Soft Edge multiplier.
        /// </summary>
        public Vector4 fadeParams;

        /// <summary>
        /// Animation Parameters:
        /// X: Rotation Speed (rad/s),
        /// Y: Pulse Frequency,
        /// Z: Pulse Intensity,
        /// W: Radial Mask Softness.
        /// </summary>
        public Vector4 animParams;

        /// <summary>
        /// Sprite/Aura Mode Parameters:
        /// X: Flipbook Column/Frame Count,
        /// Y: Flipbook Playback Speed,
        /// Z: Render Mode (0: Standard, 1: Multi-Layer Aura),
        /// W: Reserved for future extensions.
        /// </summary>
        public Vector4 animParams2;

        // ========================================================================
        // 4. AURA EXTENSION PARAMETERS (Layered Rendering) [112 bytes]
        // ========================================================================

        public Vector4 auraColor2;
        public Vector4 auraColor3;
        public Vector4 auraColor4;

        /// <summary>Rotational speeds for layers 1-4 (X,Y,Z,W).</summary>
        public Vector4 auraRotSpeeds;

        /// <summary>Pulse frequencies for layers 1-4 (X,Y,Z,W).</summary>
        public Vector4 auraPulseParams;

        /// <summary>Relative scales for layered distortion (X,Y,Z,W).</summary>
        public Vector4 auraScaleParams;

        /// <summary>Reserved for future geometric distortion parameters.</summary>
        public Vector4 auraMiscParams;

        /// <summary>
        /// Explicitly populates the row vectors from Unity's Matrix4x4 structure.
        /// Ensures correct alignment for GPU float4x4 consumption.
        /// </summary>
        public void SetMatrices(Matrix4x4 worldToDecal, Matrix4x4 decalToWorld)
        {
            wtd0 = worldToDecal.GetRow(0);
            wtd1 = worldToDecal.GetRow(1);
            wtd2 = worldToDecal.GetRow(2);
            wtd3 = worldToDecal.GetRow(3);

            dtw0 = decalToWorld.GetRow(0);
            dtw1 = decalToWorld.GetRow(1);
            dtw2 = decalToWorld.GetRow(2);
            dtw3 = decalToWorld.GetRow(3);
        }
    }
}
