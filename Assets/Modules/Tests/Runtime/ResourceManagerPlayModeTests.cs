using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ResourceManagerModule.Runtime;

namespace Tests.Runtime
{
    [TestFixture]
    public class ResourceManagerPlayModeTests
    {
        private GameObject _mockAsset;

        [SetUp]
        public void SetUp()
        {
            // 每一个测试开始前，创建一个全新的 Mock 资源，并清空资源管理器状态
            _mockAsset = new GameObject("MockAsset");
            DirectRefProvider.Clear();
            ResourceManager.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mockAsset != null)
            {
                Object.DestroyImmediate(_mockAsset);
            }
            DirectRefProvider.Clear();
            ResourceManager.ClearAll();
        }

        [UnityTest]
        public IEnumerator ResourceManager_LoadAsync_CacheHit_DoesNotDuplicateLoad()
        {
            // 1. 注册 Mock 资产到直接引用提供者
            DirectRefProvider.RegisterAsset("TestPrefab", _mockAsset);

            // 2. 异步加载第一次
            var task1 = ResourceManager.LoadAsync<GameObject>("TestPrefab");
            yield return new WaitUntil(() => task1.IsCompleted);
            var handle1 = task1.Result;

            Assert.IsNotNull(handle1);
            Assert.AreEqual(_mockAsset, handle1.Asset);
            Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TestPrefab"), "首次加载后引用计数应为 1");

            // 3. 异步加载第二次 (命中高速缓存)
            var task2 = ResourceManager.LoadAsync<GameObject>("TestPrefab");
            yield return new WaitUntil(() => task2.IsCompleted);
            var handle2 = task2.Result;

            Assert.IsNotNull(handle2);
            Assert.AreEqual(handle1.Asset, handle2.Asset, "两次加载返回的应为同一个内存实例");
            Assert.AreEqual(2, ResourceManager.GetDebugRefCount("TestPrefab"), "缓存命中后引用计数应累加至 2");

            // 4. 清理资源句柄
            handle1.Dispose();
            Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TestPrefab"), "释放一个句柄后引用计数应归 1");

            handle2.Dispose();
            Assert.AreEqual(0, ResourceManager.GetDebugRefCount("TestPrefab"), "释放所有句柄后引用计数应归 0");
        }

        [UnityTest]
        public IEnumerator ResourceManager_Release_RefCountZero_UnloadsAsset()
        {
            // 1. 注册并加载
            DirectRefProvider.RegisterAsset("TestPrefab", _mockAsset);
            var task = ResourceManager.LoadAsync<GameObject>("TestPrefab");
            yield return new WaitUntil(() => task.IsCompleted);
            var handle = task.Result;

            Assert.IsNotNull(handle);
            Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TestPrefab"));

            // 2. 直接释放句柄
            handle.Dispose();

            // 3. 断言引用计数归零，且已从高速缓存中剔除
            Assert.AreEqual(0, ResourceManager.GetDebugRefCount("TestPrefab"), "句柄被释放后，引用计数应清空为 0");
        }

        [UnityTest]
        public IEnumerator ResourceManager_UsingBlock_AutoReleasesOnDispose()
        {
            DirectRefProvider.RegisterAsset("TestPrefab", _mockAsset);

            // 模拟在 using 块内部加载，离开块时自动释放引用计数
            var task = ResourceManager.LoadAsync<GameObject>("TestPrefab");
            yield return new WaitUntil(() => task.IsCompleted);

            using (var handle = task.Result)
            {
                Assert.IsNotNull(handle);
                Assert.AreEqual(1, ResourceManager.GetDebugRefCount("TestPrefab"), "在 using 块内部，引用计数应为 1");
            }

            Assert.AreEqual(0, ResourceManager.GetDebugRefCount("TestPrefab"), "离开 using 块后，句柄应被自动 Dispose，引用计数应自动归 0");
        }
    }
}
