using System;
using System.Collections;
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

            // 🚀 同时启动两个加载任务，不等待第一个完成，以测试并发加载合并机制
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

            // 🚀 尝试用错误的类型 (比如 Texture2D) 来加载已缓存为 GameObject 的相同 Key 资源，应抛出类型不匹配异常
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
    }
}
