using System;
using UnityEngine;

namespace DecalMini
{
    [Serializable]
    public class DecalAuraLayer
    {
        public string name = "New Layer";
        public bool active = true;
        public Color color = Color.white;
        public float scale = 1.0f;
        public float rotationSpeed = 0f;
        public float pulseSpeed = 0f;
    }

    [Serializable]
    public class DecalAuraModule
    {
        public Texture2D auraTexture;
        public float softFade = 0.1f;
        public float pulseIntensity = 0.5f;

        public DecalAuraLayer layerR = new DecalAuraLayer
        {
            name = "Core (R)",
            color = new Color(1, 0, 0, 1),
        };
        public DecalAuraLayer layerG = new DecalAuraLayer
        {
            name = "Inner (G)",
            color = new Color(0, 1, 0, 1),
        };
        public DecalAuraLayer layerB = new DecalAuraLayer
        {
            name = "Outer (B)",
            color = new Color(0, 0, 1, 1),
        };
        public DecalAuraLayer layerA = new DecalAuraLayer
        {
            name = "Detail (A)",
            color = new Color(1, 1, 1, 1),
        };

        public void FillDecalData(ref DecalDataMini data)
        {
            if (auraTexture == null)
                return;

            data.color = layerR.active ? layerR.color : Color.clear;
            data.auraColor2 = layerG.active ? layerG.color : Color.clear;
            data.auraColor3 = layerB.active ? layerB.color : Color.clear;
            data.auraColor4 = layerA.active ? layerA.color : Color.clear;

            // UV Scale & Offset (Restore to default for correct sampling)
            data.uvScaleOffset = new Vector4(1, 1, 0, 0);

            // Layer Scales (Correct Mapping to auraScaleParams)
            data.auraScaleParams = new Vector4(
                layerR.scale,
                layerG.scale,
                layerB.scale,
                layerA.scale
            );

            // Texture Index and Global Fading
            data.fadeParams = new Vector4(
                softFade,
                pulseIntensity,
                DecalSystemMini.GetTextureIndex(auraTexture),
                0.1f
            );

            // Rotation Speeds
            data.auraRotSpeeds = new Vector4(
                layerR.rotationSpeed,
                layerG.rotationSpeed,
                layerB.rotationSpeed,
                layerA.rotationSpeed
            );

            // Pulsing Speeds (Mapped to auraPulseParams in Struct)
            data.auraPulseParams = new Vector4(
                layerR.pulseSpeed,
                layerG.pulseSpeed,
                layerB.pulseSpeed,
                layerA.pulseSpeed
            );

            // Mode Flag: 1.0 means Aura Mode in Shader
            data.animParams2.z = 1.0f;
        }
    }
}
