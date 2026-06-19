using System;
using UnityEngine;

namespace CharacterShader.Runtime
{
    [CreateAssetMenu(menuName = "Character Shader/Material Profile", fileName = "CharacterMaterialProfile")]
    public sealed class CharacterMaterialProfile : ScriptableObject
    {
        public const int SlotCount = 8;

        [SerializeField] private MaterialSlot[] slots = CreateDefaultSlots();
        [SerializeField] private RampArrayConfig rampArrayConfig;
        [SerializeField] private MatCapArrayConfig matCapArrayConfig;
        [SerializeField] private Texture2DArray rampArray;
        [SerializeField] private Texture2DArray matCapArray;

        public MaterialSlot[] Slots => slots;
        public RampArrayConfig RampArrayConfig => rampArrayConfig;
        public MatCapArrayConfig MatCapArrayConfig => matCapArrayConfig;
        public Texture2DArray RampArray => rampArray;
        public Texture2DArray MatCapArray => matCapArray;

        public string GetSlotDisplayName(int index)
        {
            EnsureSlotCount();
            if (index < 0 || index >= slots.Length)
            {
                return $"ID {index}";
            }

            MaterialSlot slot = slots[index];
            string slotName = string.IsNullOrWhiteSpace(slot.name) ? slot.kind.ToString() : slot.name;
            return $"ID {index} - {slotName} ({slot.kind})";
        }

        public string[] GetSlotDisplayNames()
        {
            EnsureSlotCount();
            string[] names = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                names[i] = GetSlotDisplayName(i);
            }

            return names;
        }

        public static string GetDefaultSlotDisplayName(int index)
        {
            MaterialSlot[] defaults = CreateDefaultSlots();
            if (index < 0 || index >= defaults.Length)
            {
                return $"ID {index}";
            }

            MaterialSlot slot = defaults[index];
            return $"ID {index} - {slot.name} ({slot.kind})";
        }

        public void SetRampArrayConfig(RampArrayConfig config)
        {
            rampArrayConfig = config;
        }

        public void SetMatCapArrayConfig(MatCapArrayConfig config)
        {
            matCapArrayConfig = config;
        }

        public void SetRampArray(Texture2DArray array)
        {
            rampArray = array;
        }

        public void SetMatCapArray(Texture2DArray array)
        {
            matCapArray = array;
        }

        public void EnsureSlotCount()
        {
            if (slots != null && slots.Length == SlotCount)
            {
                return;
            }

            MaterialSlot[] defaults = CreateDefaultSlots();
            if (slots != null)
            {
                int copyCount = Mathf.Min(slots.Length, defaults.Length);
                Array.Copy(slots, defaults, copyCount);
            }

            slots = defaults;
        }

        public void ApplyTo(Material material)
        {
            if (material == null)
            {
                return;
            }

            EnsureSlotCount();

            for (int i = 0; i < SlotCount; i++)
            {
                MaterialSlot slot = slots[i];
                Vector4 p0 = new Vector4(slot.rampSlice, slot.matCapSlice, slot.matCapStrength, slot.rimStrength);
                Vector4 p1 = new Vector4(slot.shadowHardness, slot.rampBiasScale, slot.metalShadowLift, slot.aoStrength);
                
                material.SetVector($"_MatProfile0_{i}", p0);
                material.SetVector($"_MatProfile1_{i}", p1);
            }

            if (rampArray != null)
            {
                material.SetTexture("_RampArray", rampArray);
                material.SetFloat("_UseRampArray", 1.0f);
                material.EnableKeyword("_USERAMPARRAY_ON");
            }
            else if (material.HasProperty("_UseRampArray"))
            {
                material.SetFloat("_UseRampArray", 0.0f);
                material.DisableKeyword("_USERAMPARRAY_ON");
            }

            if (matCapArray != null)
            {
                material.SetTexture("_MatCapArray", matCapArray);
                material.SetFloat("_UseMatCapArray", 1.0f);
                material.EnableKeyword("_USEMATCAPARRAY_ON");
            }
            else if (material.HasProperty("_UseMatCapArray"))
            {
                material.SetFloat("_UseMatCapArray", 0.0f);
                material.DisableKeyword("_USEMATCAPARRAY_ON");
            }
        }

        public void PullFrom(Material material)
        {
            if (material == null)
            {
                return;
            }

            EnsureSlotCount();
            for (int i = 0; i < SlotCount; i++)
            {
                Vector4 profile0 = material.GetVector($"_MatProfile0_{i}");
                Vector4 profile1 = material.GetVector($"_MatProfile1_{i}");

                slots[i].rampSlice = Mathf.RoundToInt(profile0.x);
                slots[i].matCapSlice = Mathf.RoundToInt(profile0.y);
                slots[i].matCapStrength = profile0.z;
                slots[i].rimStrength = profile0.w;
                slots[i].shadowHardness = profile1.x;
                slots[i].rampBiasScale = profile1.y;
                slots[i].metalShadowLift = profile1.z;
                slots[i].aoStrength = profile1.w;
            }

            if (material.HasProperty("_RampArray"))
            {
                rampArray = material.GetTexture("_RampArray") as Texture2DArray;
            }

            if (material.HasProperty("_MatCapArray"))
            {
                matCapArray = material.GetTexture("_MatCapArray") as Texture2DArray;
            }
        }

        public void ApplyPreset(int slotIndex, MaterialKind kind)
        {
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                return;
            }

            slots[slotIndex] = CreatePreset(kind, slotIndex);
        }

        public void ResetToDefaults()
        {
            slots = CreateDefaultSlots();
        }

        private void OnValidate()
        {
            EnsureSlotCount();
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Clamp();
            }
        }

        private static MaterialSlot[] CreateDefaultSlots()
        {
            return new[]
            {
                CreatePreset(MaterialKind.Skin, 0),
                CreatePreset(MaterialKind.Hair, 1),
                CreatePreset(MaterialKind.Fabric, 2),
                CreatePreset(MaterialKind.Metal, 3),
                CreatePreset(MaterialKind.Leather, 4),
                CreatePreset(MaterialKind.Silk, 5),
                CreatePreset(MaterialKind.Glass, 6),
                CreatePreset(MaterialKind.Effect, 7),
            };
        }

        private static MaterialSlot CreatePreset(MaterialKind kind, int defaultSlice)
        {
            switch (kind)
            {
                case MaterialKind.Skin:
                    return MaterialSlot.Create("Skin", kind, defaultSlice, defaultSlice, 0.45f, 0.25f, 1.00f, 1.00f, 0.15f, 1.00f);
                case MaterialKind.Hair:
                    return MaterialSlot.Create("Hair", kind, defaultSlice, defaultSlice, 0.55f, 0.20f, 1.00f, 1.00f, 0.12f, 1.00f);
                case MaterialKind.Fabric:
                    return MaterialSlot.Create("Fabric", kind, defaultSlice, defaultSlice, 0.80f, 0.15f, 1.25f, 0.80f, 0.35f, 0.90f);
                case MaterialKind.Metal:
                    return MaterialSlot.Create("Metal", kind, defaultSlice, defaultSlice, 1.00f, 0.10f, 1.45f, 0.65f, 0.55f, 0.75f);
                case MaterialKind.Leather:
                    return MaterialSlot.Create("Leather", kind, defaultSlice, defaultSlice, 0.35f, 0.35f, 0.80f, 1.20f, 0.08f, 1.00f);
                case MaterialKind.Silk:
                    return MaterialSlot.Create("Silk", kind, defaultSlice, defaultSlice, 0.65f, 0.20f, 1.00f, 1.00f, 0.18f, 1.00f);
                case MaterialKind.Glass:
                    return MaterialSlot.Create("Glass", kind, defaultSlice, defaultSlice, 0.50f, 0.20f, 1.00f, 1.00f, 0.20f, 1.00f);
                default:
                    return MaterialSlot.Create("Effect", MaterialKind.Effect, defaultSlice, defaultSlice, 0.50f, 0.20f, 1.00f, 1.00f, 0.20f, 1.00f);
            }
        }

        [Serializable]
        public struct MaterialSlot
        {
            public string name;
            public MaterialKind kind;
            [Min(0)] public int rampSlice;
            [Min(0)] public int matCapSlice;
            [Range(0, 3)] public float matCapStrength;
            [Range(0, 2)] public float rimStrength;
            [Range(0.1f, 4)] public float shadowHardness;
            [Range(0, 2)] public float rampBiasScale;
            [Range(0, 1)] public float metalShadowLift;
            [Range(0, 1)] public float aoStrength;

            public static MaterialSlot Create(
                string name,
                MaterialKind kind,
                int rampSlice,
                int matCapSlice,
                float matCapStrength,
                float rimStrength,
                float shadowHardness,
                float rampBiasScale,
                float metalShadowLift,
                float aoStrength)
            {
                return new MaterialSlot
                {
                    name = name,
                    kind = kind,
                    rampSlice = rampSlice,
                    matCapSlice = matCapSlice,
                    matCapStrength = matCapStrength,
                    rimStrength = rimStrength,
                    shadowHardness = shadowHardness,
                    rampBiasScale = rampBiasScale,
                    metalShadowLift = metalShadowLift,
                    aoStrength = aoStrength
                };
            }

            public void Clamp()
            {
                rampSlice = Mathf.Max(0, rampSlice);
                matCapSlice = Mathf.Max(0, matCapSlice);
                matCapStrength = Mathf.Clamp(matCapStrength, 0f, 3f);
                rimStrength = Mathf.Clamp(rimStrength, 0f, 2f);
                shadowHardness = Mathf.Clamp(shadowHardness, 0.1f, 4f);
                rampBiasScale = Mathf.Clamp(rampBiasScale, 0f, 2f);
                metalShadowLift = Mathf.Clamp01(metalShadowLift);
                aoStrength = Mathf.Clamp01(aoStrength);
            }
        }

        public enum MaterialKind
        {
            Skin,
            Hair,
            Fabric,
            Metal,
            Leather,
            Silk,
            Glass,
            Effect
        }
    }
}
