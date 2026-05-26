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
            // 🚀 一个随机或空的 key 绝对不能被 CanLoad 误判为 true，保证 Provider 降级回退机制不被抢占
            Assert.IsFalse(_provider.CanLoad("InvalidAddressableKey_Random_12345"));
            Assert.IsFalse(_provider.CanLoad(string.Empty));
            Assert.IsFalse(_provider.CanLoad(null));
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
