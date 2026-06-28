using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ResourceManagerModule.Runtime;

namespace ResourceManagerModule.Tests
{
    [TestFixture]
    public class AddressablesProviderTests
    {
        private AddressablesProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new AddressablesProvider();
        }

        [Test]
        public void Test_CanLoad_InvalidKeys_ReturnsFalse()
        {
            Assert.IsFalse(_provider.CanLoad("InvalidAddressableKey_Random_12345"));
            Assert.IsFalse(_provider.CanLoad(string.Empty));
            Assert.IsFalse(_provider.CanLoad(null));
        }

        [Test]
        public void Test_CanLoad_UsesCatalogWithoutAddressablesInitialization()
        {
            var catalog = ScriptableObject.CreateInstance<AddressablesResourceCatalog>();
            catalog.SetKeys(new[] { "CatalogKey" });

            var provider = new AddressablesProvider(catalog);

            Assert.IsTrue(provider.CanLoad("CatalogKey"));
            Assert.IsFalse(provider.CanLoad("MissingCatalogKey"));

            UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Test_SetCatalog_ClearsCachedKeyState()
        {
            var firstCatalog = ScriptableObject.CreateInstance<AddressablesResourceCatalog>();
            firstCatalog.SetKeys(new[] { "FirstKey" });
            var secondCatalog = ScriptableObject.CreateInstance<AddressablesResourceCatalog>();
            secondCatalog.SetKeys(new[] { "SecondKey" });

            var provider = new AddressablesProvider(firstCatalog);
            Assert.IsTrue(provider.CanLoad("FirstKey"));

            provider.SetCatalog(secondCatalog);

            Assert.IsFalse(provider.CanLoad("FirstKey"));
            Assert.IsTrue(provider.CanLoad("SecondKey"));

            UnityEngine.Object.DestroyImmediate(firstCatalog);
            UnityEngine.Object.DestroyImmediate(secondCatalog);
        }

        [Test]
        public async System.Threading.Tasks.Task Test_NullOrEmptyKeys_ThrowArgumentException()
        {
            try
            {
                await _provider.LoadAssetAsync<GameObject>(null);
                Assert.Fail("Expected ArgumentException for null key");
            }
            catch (ArgumentException)
            {
                // Pass
            }

            try
            {
                await _provider.LoadAssetAsync<GameObject>(string.Empty);
                Assert.Fail("Expected ArgumentException for empty key");
            }
            catch (ArgumentException)
            {
                // Pass
            }
        }
    }
}
