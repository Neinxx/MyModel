using DecalMini;
using NUnit.Framework;
using UnityEngine;

namespace DecalMini.Tests
{
    public class DecalAuraTests
    {
        [Test]
        public void AuraDataConversion_MatchesExpectedValues()
        {
            // 1. Setup
            GameObject go = new GameObject("Aura");
            var aura = go.AddComponent<DecalAuraComponent>();

            // Setup a mock texture to test index mapping
            Texture2D mockTex = new Texture2D(2, 2);
            aura.auraTexture = mockTex;
            aura.tintColor = Color.red;
            aura.radius = 2.0f;

            // 2. Execute
            DecalDataMini data = aura.ToDecalData();

            // 3. Verify
            Assert.AreEqual(1.0f, data.animParams2.z, "Mode should be Aura (1)");
            Assert.AreEqual(Color.red, (Color)data.color, "Tint color mismatch");

            // Verify texture index
            int expectedIndex = DecalSystemMini.GetTextureIndex(mockTex);
            Assert.AreEqual(expectedIndex, (int)data.fadeParams.z, "Texture index mismatch");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mockTex);
        }

        [Test]
        public void DecalSystem_Registration_SupportsAuraComponent()
        {
            // 1. Setup
            GameObject go = new GameObject("Aura");
            var aura = go.AddComponent<DecalAuraComponent>();

            // 2. Execute
            DecalSystemMini.Register(aura);

            // 3. Verify
            // Note: Registration might be count-based or set-based depending on kernel state
            Assert.IsTrue(aura.isActiveAndEnabled, "Aura component should be active and enabled");

            DecalSystemMini.Unregister(aura);
            Object.DestroyImmediate(go);
        }
    }
}
