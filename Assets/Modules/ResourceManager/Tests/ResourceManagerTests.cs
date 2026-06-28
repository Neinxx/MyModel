using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ResourceManagerModule.Runtime;

namespace ResourceManagerModule.Tests
{
    [TestFixture]
    public class ResourceManagerTests
    {
        private GameObject _mockAsset;

        [SetUp]
        public void SetUp()
        {
            _mockAsset = new GameObject("TestMockAsset");
            DirectRefProvider.Clear();
            ResourceManager.VerboseLogging = false;
            ResourceManager.EnableResourcesFallback = true;
            ResourceManager.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mockAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_mockAsset);
            }
            DirectRefProvider.Clear();
            ResourceManager.VerboseLogging = false;
            ResourceManager.EnableResourcesFallback = true;
            ResourceManager.ClearAll();
        }

        [UnityTest]
        public IEnumerator Test_ReferenceCounting_CorrectFlow()
        {
            DirectRefProvider.RegisterAsset("TestKey", _mockAsset);

            var loadTask = ResourceManager.LoadAsync<GameObject>("TestKey");
            yield return new WaitUntil(() => loadTask.IsCompleted);
            var handle1 = loadTask.Result;

            Assert.IsNotNull(handle1);
            Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TestKey"));

            var loadTask2 = ResourceManager.LoadAsync<GameObject>("TestKey");
            yield return new WaitUntil(() => loadTask2.IsCompleted);
            var handle2 = loadTask2.Result;

            Assert.AreEqual(2, ResourceManager.GetDebugRefCount("TestKey"));

            handle1.Dispose();
            Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TestKey"));

            handle2.Dispose();
            Assert.AreEqual(0, ResourceManager.GetDebugRefCount("TestKey"));
        }

        [UnityTest]
        public IEnumerator Test_ConcurrentLoadCoalescing_Integrity()
        {
            DirectRefProvider.RegisterAsset("ConcurrentKey", _mockAsset);

            var loadTask1 = ResourceManager.LoadAsync<GameObject>("ConcurrentKey");
            var loadTask2 = ResourceManager.LoadAsync<GameObject>("ConcurrentKey");

            yield return new WaitUntil(() => loadTask1.IsCompleted && loadTask2.IsCompleted);

            var handle1 = loadTask1.Result;
            var handle2 = loadTask2.Result;

            Assert.IsNotNull(handle1);
            Assert.IsNotNull(handle2);
            Assert.AreSame(handle1.Asset, handle2.Asset, "并发合并加载应当返回同一实例");
            Assert.AreEqual(2, ResourceManager.GetDebugRefCount("ConcurrentKey"), "并发加载完成后，引用计数应正确累加为 2");

            handle1.Dispose();
            handle2.Dispose();
        }

        [UnityTest]
        public IEnumerator Test_TypeMismatch_ThrowsInvalidCastException()
        {
            DirectRefProvider.RegisterAsset("TypeMismatchKey", _mockAsset);

            var loadTask = ResourceManager.LoadAsync<GameObject>("TypeMismatchKey");
            yield return new WaitUntil(() => loadTask.IsCompleted);
            var handle = loadTask.Result;

            var badTask = ResourceManager.LoadAsync<Texture2D>("TypeMismatchKey");
            yield return new WaitUntil(() => badTask.IsCompleted);

            Assert.IsTrue(badTask.IsFaulted);
            Assert.IsNotNull(badTask.Exception);
            Assert.IsInstanceOf<InvalidCastException>(badTask.Exception.InnerException);

            handle.Dispose();
        }

        [UnityTest]
        public IEnumerator Test_TeardownClearAll_PurgesFully()
        {
            DirectRefProvider.RegisterAsset("TeardownKey", _mockAsset);

            var loadTask = ResourceManager.LoadAsync<GameObject>("TeardownKey");
            yield return new WaitUntil(() => loadTask.IsCompleted);
            var handle = loadTask.Result;

            Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TeardownKey"));

            ResourceManager.ClearAll();

            Assert.AreEqual(0, ResourceManager.GetDebugRefCount("TeardownKey"), "ClearAll 之后缓存应该被清空，引用计数应归 0");
        }

        [UnityTest]
        public IEnumerator Test_Load_UsesSameProviderForUnload()
        {
            var provider = new CountingProvider("ProviderBindingKey", _mockAsset);
            ResourceManager.RegisterProviderBefore<ResourcesProvider>(provider);

            var loadTask = ResourceManager.LoadAsync<GameObject>("ProviderBindingKey");
            yield return new WaitUntil(() => loadTask.IsCompleted);

            var handle = loadTask.Result;
            handle.Dispose();

            Assert.AreEqual(1, provider.UnloadCount);
            ResourceManager.UnregisterProvider(provider);
        }

        [UnityTest]
        public IEnumerator Test_StrictMode_MissingKeyFailsBeforeResourcesFallback()
        {
            ResourceManager.EnableResourcesFallback = false;

            var loadTask = ResourceManager.LoadAsync<GameObject>("MissingStrictKey");
            yield return new WaitUntil(() => loadTask.IsCompleted);

            Assert.IsTrue(loadTask.IsFaulted);
            Assert.IsInstanceOf<KeyNotFoundException>(loadTask.Exception.InnerException);
        }

        private sealed class CountingProvider : IResourceProvider
        {
            private readonly string _key;
            private readonly GameObject _asset;

            public CountingProvider(string key, GameObject asset)
            {
                _key = key;
                _asset = asset;
            }

            public int UnloadCount { get; private set; }

            public Task<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
            {
                return Task.FromResult(_asset as T);
            }

            public void UnloadAsset(string key)
            {
                UnloadCount++;
            }

            public bool CanLoad(string key)
            {
                return key == _key;
            }
        }
    }
}
