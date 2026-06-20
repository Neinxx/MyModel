using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor.Tests
{
    /// <summary>
    /// 高级自动化单元测试驱动：支持模块分组与步骤追踪
    /// </summary>
    public class DecalSystemMiniTests
    {
        [Test]
        public static void CircularBuffer_Overflow_IsClamped()
        {
            RunNUnitTest(Test_CircularBuffer_Overflow);
        }

        [Test]
        public static void CullingMath_RemovesOutOfRangeDecals()
        {
            RunNUnitTest(Test_CullingMath);
        }

        [Test]
        public static void AdditiveLevelLoading_MergesAndUnloadsIndependently()
        {
            RunNUnitTest(Test_AdditiveLevelLoading);
        }

        [Test]
        public static void StreamingSafety_IsIdempotentAndNullSafe()
        {
            RunNUnitTest(Test_Streaming_Safety);
        }

        [Test]
        public static void PoolingLifecycle_CleansParticleAndProjectorPools()
        {
            RunNUnitTest(Test_Pooling_Lifecycle);
        }

        [Test]
        public static void ReleaseAll_ClearsRuntimeMemoryState()
        {
            RunNUnitTest(Test_ReleaseAll_MemorySafety);
        }

        [Test]
        public static void DataSerialization_PreservesGpuFields()
        {
            RunNUnitTest(Test_Data_Serialization);
        }

        [Test]
        public static void GridRebuild_ReindexesStaticData()
        {
            RunNUnitTest(Test_Grid_Rebuild_Trigger);
        }

        [Test]
        public static void GpuStride_IsAlignedWithStructSize()
        {
            RunNUnitTest(Test_GPU_Stride_Alignment);
        }

        [Test]
        public static void SortingStability_OrdersByPriorityAndDistance()
        {
            RunNUnitTest(Test_Sorting_Stability);
        }

        [Test]
        public static void TotalCount_IncludesStaticAndActiveRuntimeData()
        {
            RunNUnitTest(Test_TotalCount_Integrity);
        }

        [Test]
        public static void RenderingFillData_RemainsAllocationLight()
        {
            RunNUnitTest(Test_ZeroGC_Rendering);
        }

        [Test]
        public static void TextureIndex_IsClampedBeforeRenderData()
        {
            RunNUnitTest(Test_TextureIndex_Safety);
        }

        [Test]
        public static void LifecycleIsolation_PreservesStaticDataWhenRuntimeExpires()
        {
            RunNUnitTest(Test_Lifecycle_Isolation);
        }

        [Test]
        public static void DynamicFade_ComputesExpectedAlpha()
        {
            RunNUnitTest(Test_Dynamic_Fade_Logic);
        }

        [MenuItem("Tools/Decal System/Run Diagnostics", false, 900)]
        public static void RunAllTests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[DecalSystemMini] Diagnostics skipped while entering or running Play Mode.");
                return;
            }

            EditorApplication.delayCall += () =>
            {
                Debug.Log(
                    "<color=#58A6FF><b>[DecalSystemMini]  🚀 启动全系统深度体检...</b></color>"
                );

                int passed = 0;
                int total = 0;

                // --- 项目 1：核心算法与渲染数据 ---
                LogModule("CORE DATA & RENDER");
                passed += RunTest("循环缓冲区越界防御", Test_CircularBuffer_Overflow) ? 1 : 0;
                total++;
                passed += RunTest("视锥与距离剔除数学逻辑", Test_CullingMath) ? 1 : 0;
                total++;

                // --- 项目 2：流式加载安全 ---
                LogModule("STREAMING & SAFETY");
                passed += RunTest("多关卡叠加加载与卸载", Test_AdditiveLevelLoading) ? 1 : 0;
                total++;
                passed += RunTest("加载幂等性与无效输入防御", Test_Streaming_Safety) ? 1 : 0;
                total++;

                // --- 项目 3：对象池与内存安全 ---
                LogModule("POOLING & MEMORY");
                passed += RunTest("粒子与贴花对象池生命周期", Test_Pooling_Lifecycle) ? 1 : 0;
                total++;
                passed += RunTest("全量释放与内存引用清理", Test_ReleaseAll_MemorySafety) ? 1 : 0;
                total++;

                // --- 项目 4：序列化与管线 ---
                LogModule("SERIALIZATION & PIPELINE");
                passed += RunTest("数据结构序列化完整性", Test_Data_Serialization) ? 1 : 0;
                total++;
                passed += RunTest("空间网格动态重建验证", Test_Grid_Rebuild_Trigger) ? 1 : 0;
                total++;

                // --- 项目 5：严苛环境压力测试 ---
                LogModule("STRESS & PRECISION");
                passed += RunTest("GPU 内存步长对齐校验", Test_GPU_Stride_Alignment) ? 1 : 0;
                total++;
                passed += RunTest("深度排序稳定性权重校验", Test_Sorting_Stability) ? 1 : 0;
                total++;
                passed += RunTest("全系统 TotalCount 计数完整性", Test_TotalCount_Integrity)
                    ? 1
                    : 0;
                total++;
                passed += RunTest("核心渲染循环 0-GC 压力测试", Test_ZeroGC_Rendering) ? 1 : 0;
                total++;
                passed += RunTest("纹理索引非法边界防御", Test_TextureIndex_Safety) ? 1 : 0;
                total++;
                passed += RunTest("动静态贴花生命周期隔离验证", Test_Lifecycle_Isolation) ? 1 : 0;
                total++;
                passed += RunTest("动态贴花 Alpha 淡出逻辑校验", Test_Dynamic_Fade_Logic) ? 1 : 0;
                total++;

                if (passed == total)
                    Debug.Log(
                        $"<color=#3FB950><b>[DecalSystemMini] 🎉 完结: 全部 {passed}/{total} 项核心指标均已达标。</b></color>"
                    );
                else
                    Debug.LogError(
                        $"<color=#F85149><b>[DecalSystemMini] ❌ 警告: 只有 {passed}/{total} 项通过，请立即检查最近的变更！</b></color>"
                    );
            };
        }

        private static void LogModule(string name) =>
            Debug.Log($"<color=#7C8CFF><b>▶ MODULE: {name}</b></color>");

        private static void LogStep(string desc) =>
            Debug.Log($"   <color=#8B949E>└─ Step: {desc}</color>");

        private static bool RunTest(string testName, System.Action testMethod)
        {
            ResetTestState();
            try
            {
                testMethod();
                Debug.Log($"   <color=#3FB950>✔ [PASS]</color> {testName}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    $"   <color=#F85149>✘ [FAIL]</color> <b>{testName}</b>: {e.Message}"
                );
                return false;
            }
            finally
            {
                ResetTestState();
            }
        }

        private static void RunNUnitTest(System.Action testMethod)
        {
            ResetTestState();
            try
            {
                testMethod();
            }
            finally
            {
                ResetTestState();
            }
        }

        private static void ResetTestState()
        {
            DecalSystemMini.ReleaseAll();
            DecalPoolMini.Clear();
            DecalParticlePoolMini.ClearAll();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new System.Exception(message);
        }

        // =================================================================================
        // [CORE] 用例 1：环形缓冲区极限越界防御
        // =================================================================================
        private static void Test_CircularBuffer_Overflow()
        {
            LogStep("灌入 1500 个运行时贴花...");
            for (int i = 0; i < 1500; i++)
            {
                DecalSystemMini.SpawnRuntimeDecal(
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    0,
                    10f
                );
            }

            LogStep("验证 TotalCount 强制钳制在 1024");
            Assert(
                DecalSystemMini.TotalCount == 1024,
                $"溢出后总数异常: {DecalSystemMini.TotalCount}"
            );

            LogStep("检查 _poolPtr 指针回绕数学正确性");
            var field = typeof(DecalSystemMini).GetField(
                "_poolPtr",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            int ptr = (int)field.GetValue(null);
            Assert(ptr == 1500 % 1024, "指针回绕逻辑错误");
        }

        // =================================================================================
        // [STREAMING] 用例 2：多场景叠加加载与卸载
        // =================================================================================
        private static void Test_AdditiveLevelLoading()
        {
            var levelA = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            levelA.entries.Add(new DecalStaticEntry { position = Vector3.zero, layerMask = -1 });
            var levelB = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            levelB.entries.Add(new DecalStaticEntry { position = Vector3.one, layerMask = -1 });

            LogStep("加载 Level A 与 Level B...");
            levelA.LoadIntoKernel();
            levelB.LoadIntoKernel();

            LogStep("验证数据完美合并 (2 Entries)");
            DecalDataMini[] output = new DecalDataMini[10];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 0f);
            int count = DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            Assert(count == 2, "合并加载后数据量错误");

            LogStep("单独卸载 Level A...");
            levelA.UnloadFromKernel();
            count = DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            Assert(count == 1, "卸载 Level A 后残留或误删了 Level B");

            Object.DestroyImmediate(levelA);
            Object.DestroyImmediate(levelB);
        }

        // =================================================================================
        // [CORE] 用例 3：距离剔除核心数学正确性
        // =================================================================================
        private static void Test_CullingMath()
        {
            LogStep("在 5米 与 100米 处分别布置贴花...");
            DecalSystemMini.SpawnRuntimeDecal(
                new Vector3(0, 0, 5),
                Quaternion.identity,
                Vector3.one,
                0,
                10f
            );
            DecalSystemMini.SpawnRuntimeDecal(
                new Vector3(0, 0, 100),
                Quaternion.identity,
                Vector3.one,
                0,
                10f
            );

            LogStep("设置 50米 裁剪距离，验证 100米 贴花被成功剔除");
            DecalDataMini[] output = new DecalDataMini[10];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 0f);
            int count = DecalSystemMini.FillData(Vector3.zero, 50f, -1, output, frustum);
            Assert(count == 1, "裁剪算法数学误差超标");
        }

        // =================================================================================
        // [MEMORY] 用例 4：全量生命周期清理 (内存泄漏熔断)
        // =================================================================================
        private static void Test_ReleaseAll_MemorySafety()
        {
            LogStep("注入运行时数据与关卡数据...");
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                0,
                10f
            );
            var level = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            level.LoadIntoKernel();

            LogStep("触发 ReleaseAll 全量清理...");
            DecalSystemMini.ReleaseAll();

            LogStep("反射检查底层数组引用是否彻底断开...");
            var poolField = typeof(DecalSystemMini).GetField(
                "_runtimePool",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert(poolField.GetValue(null) == null, "_runtimePool 未置空");

            var ptrField = typeof(DecalSystemMini).GetField(
                "_poolPtr",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert((int)ptrField.GetValue(null) == 0, "_poolPtr 未重置");

            Object.DestroyImmediate(level);
        }

        // =================================================================================
        // [POOLING] 用例 5：对象池生命周期与内存闭环
        // =================================================================================
        private static void Test_Pooling_Lifecycle()
        {
            LogStep("测试粒子池播放与活跃注册...");
            var psGo = new GameObject("TestPS");
            var ps = psGo.AddComponent<ParticleSystem>();
            DecalParticlePoolMini.Play(ps, Vector3.zero, Quaternion.identity, 1f);
            var activeField = typeof(DecalParticlePoolMini).GetField(
                "_activeParticles",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            var activeList = (System.Collections.IList)activeField.GetValue(null);
            Assert(activeList.Count == 1, "粒子未进入活跃队列");

            LogStep("测试 ClearAll 彻底销毁资源...");
            DecalParticlePoolMini.ClearAll();
            Assert(activeList.Count == 0, "资源清理后残留活跃引用");

            LogStep("测试贴花对象池 Get/Release 循环...");
            var projGo = new GameObject("TestProj");
            var projPrefab = projGo.AddComponent<DecalProjectorMini>();
            DecalPoolMini.Init(projPrefab, 5, 10);
            var p1 = DecalPoolMini.Get();
            DecalPoolMini.Release(p1);

            var poolField = typeof(DecalPoolMini).GetField(
                "_pool",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            var pool = poolField.GetValue(null);
            var countInactiveProp = pool.GetType().GetProperty("CountInactive");
            Assert((int)countInactiveProp.GetValue(pool) == 1, "对象池回收计数错误");

            Object.DestroyImmediate(psGo);
            Object.DestroyImmediate(projGo);
            DecalParticlePoolMini.ClearAll();
        }

        // =================================================================================
        // [STREAMING] 用例 6：数据流加载卸载安全性
        // =================================================================================
        private static void Test_Streaming_Safety()
        {
            LogStep("模拟极端 Null 输入防御...");
            DecalStaticModuleExtensions.LoadIntoKernel(null);
            DecalStaticModuleExtensions.UnloadFromKernel(null);

            LogStep("模拟重复加载 LevelData (幂等性校验)...");
            var level = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            level.entries.Add(new DecalStaticEntry { position = Vector3.up });
            level.LoadIntoKernel();
            level.LoadIntoKernel();

            var loadedField = typeof(DecalSystemMini).GetField(
                "_loadedSources",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            var loadedSet = (System.Collections.IEnumerable)loadedField.GetValue(null);
            int count = 0;
            foreach (var item in loadedSet)
                count++;
            Assert(count == 1, "幂等性失效: 存在重复加载的数据集");

            LogStep("模拟卸载未加载过的幽灵数据...");
            var ghost = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            ghost.UnloadFromKernel();

            LogStep("验证数据最终清理状态...");
            level.UnloadFromKernel();
            Assert(DecalSystemMini.Count == 0, "卸载后网格数据残留");

            Object.DestroyImmediate(level);
            Object.DestroyImmediate(ghost);
        }

        // =================================================================================
        // [SERIALIZATION] 用例 7：数据结构序列化完整性 (防止 [Serializable] 丢失)
        // =================================================================================
        private static void Test_Data_Serialization()
        {
            LogStep("创建带随机矩阵的测试数据...");
            var source = new DecalDataMini();
            Matrix4x4 testMatrix = Matrix4x4.TRS(
                new Vector3(12.3f, 45.6f, 78.9f),
                Quaternion.Euler(10, 20, 30),
                new Vector3(1, 2, 3)
            );
            source.SetMatrices(testMatrix.inverse, testMatrix);
            source.color = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);

            LogStep("使用 JsonUtility 模拟 Unity 资源序列化往返...");
            string json = JsonUtility.ToJson(source);
            var dest = JsonUtility.FromJson<DecalDataMini>(json);

            LogStep("验证关键字段在序列化后保持不变...");
            Assert(
                Mathf.Approximately(dest.dtw0.w, source.dtw0.w),
                $"矩阵 X 轴坐标丢失: {dest.dtw0.w} != {source.dtw0.w}"
            );
            Assert(
                Mathf.Approximately(dest.color.z, source.color.z),
                $"颜色 B 通道丢失: {dest.color.z} != {source.color.z}"
            );
            Assert(dest.dtw3.w != 0, "投影矩阵 W 分量丢失 (可能是因为没加 [Serializable])");
        }

        // =================================================================================
        // [PIPELINE] 用例 8：空间网格动态重建逻辑验证
        // =================================================================================
        private static void Test_Grid_Rebuild_Trigger()
        {
            LogStep("注入初始 AtlasConfig (网格大小 5)...");
            var config5 = ScriptableObject.CreateInstance<DecalAtlasConfigMini>();
            config5.spatialGridSize = 5f;
            DecalSystemMini.SetAtlasConfig(config5);

            LogStep("注册一个位于 (12, 0, 0) 的静态贴花 (应位于网格 X=2)...");
            var level = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            level.entries.Add(
                new DecalStaticEntry
                {
                    position = new Vector3(12, 0, 0),
                    layerMask = -1,
                    data = new DecalDataMini { color = Color.white },
                }
            );
            level.LoadIntoKernel();

            LogStep("切换 AtlasConfig (网格大小变为 20)...");
            var config20 = ScriptableObject.CreateInstance<DecalAtlasConfigMini>();
            config20.spatialGridSize = 20f;
            // 触发重建
            DecalSystemMini.SetAtlasConfig(config20);

            LogStep("验证贴花已迁移到新网格 (X=0)...");
            var gridField = typeof(DecalSystemMini).GetField(
                "_staticGrid",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            var grid = (System.Collections.IDictionary)gridField.GetValue(null);

            // 在网格 20 中，(12,0,0) 的 Key 是 (0,0)
            long newKey = (0L << 32) | (0L & 0xFFFFFFFFL);
            Assert(grid.Contains(newKey), "网格重建后未能正确重新索引静态贴花");

            Object.DestroyImmediate(config5);
            Object.DestroyImmediate(config20);
            Object.DestroyImmediate(level);
        }

        // =================================================================================
        // [STRESS] 用例 9：GPU 内存步长严格对齐校验 (核心防御)
        // =================================================================================
        private static void Test_GPU_Stride_Alignment()
        {
            LogStep("检查 C# 内存布局与 Stride 常量一致性...");
            int actualSize = System.Runtime.InteropServices.Marshal.SizeOf<DecalDataMini>();

            Assert(
                actualSize == DecalDataMini.Stride,
                $"GPU 步长不匹配! C# 结构体大小为 {actualSize}，但 Stride 常量定义为 {DecalDataMini.Stride}。这会导致 GPU 渲染数据偏移！"
            );

            LogStep("检查 16 字节对齐安全 (float4 对齐)...");
            Assert(
                actualSize % 16 == 0,
                $"结构体大小 {actualSize} 未 16 字节对齐，在某些平台可能会导致显存读取性能下降或报错。"
            );
        }

        // =================================================================================
        // [STRESS] 用例 10：深度排序稳定性与权重校验
        // =================================================================================
        private static void Test_Sorting_Stability()
        {
            LogStep("在同一位置注入不同 SortingOrder 的贴花...");
            // 使用命名参数确保填入正确的参数位
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.up,
                Quaternion.identity,
                Vector3.one,
                textureIndex: 55,
                sortingOrder: 10
            );
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.up,
                Quaternion.identity,
                Vector3.one,
                textureIndex: 55,
                sortingOrder: 5
            );
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.up,
                Quaternion.identity,
                Vector3.one,
                textureIndex: 55,
                sortingOrder: 20
            );

            LogStep("验证排序结果是否按 Order 从小到大排列...");
            DecalDataMini[] output = new DecalDataMini[10];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 1f); // 向上半空间裁剪

            int count = DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            Assert(count == 3, "数据量异常");

            // 验证 SortingOrder 是否正确存入了对应的内部排序字段（这里需要反射检查或通过 FillData 的结果验证）
            // 注意：FillData 已经根据 _entryComparer (Order + Distance) 排序
            // 我们检查返回的 dataArray 顺序是否符合 Order 5, 10, 20

            // 为了验证结果，我们需要知道 DecalSortEntry 的内部排序状态。
            // 由于 DecalDataMini 没存 SortingOrder，我们通过反射拿私有的排序缓存来验证。
            var sortBufferField = typeof(DecalSystemMini).GetField(
                "_sortBuffer",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            var sortBuffer = (System.Array)sortBufferField.GetValue(null);

            int order0 = (int)
                sortBuffer
                    .GetValue(0)
                    .GetType()
                    .GetField("SortingOrder")
                    .GetValue(sortBuffer.GetValue(0));
            int order1 = (int)
                sortBuffer
                    .GetValue(1)
                    .GetType()
                    .GetField("SortingOrder")
                    .GetValue(sortBuffer.GetValue(1));
            int order2 = (int)
                sortBuffer
                    .GetValue(2)
                    .GetType()
                    .GetField("SortingOrder")
                    .GetValue(sortBuffer.GetValue(2));

            Assert(order0 == 5, $"排序权重逻辑错误:  预期 5，实际 {order0}");
            Assert(order1 == 10, $"排序权重逻辑错误: 预期 10，实际 {order1}");
            Assert(order2 == 20, $"排序权重逻辑错误: 预期 20，实际 {order2}");

            LogStep("验证相同 Order 下，按距离(远到近)排序...");
            DecalSystemMini.ReleaseAll();
            DecalSystemMini.SpawnRuntimeDecal(
                new Vector3(0, 0, 10),
                Quaternion.identity,
                Vector3.one,
                0,
                sortingOrder: 0
            ); // 远
            DecalSystemMini.SpawnRuntimeDecal(
                new Vector3(0, 0, 2),
                Quaternion.identity,
                Vector3.one,
                0,
                sortingOrder: 0
            ); // 近

            count = DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            // 远处的先画 (Index 0)，近处的后画 (Index 1) 以覆盖远处
            // 提取位置 (dtw0.w, dtw1.w, dtw2.w)
            float dist0 = new Vector3(
                output[0].dtw0.w,
                output[0].dtw1.w,
                output[0].dtw2.w
            ).magnitude;
            float dist1 = new Vector3(
                output[1].dtw0.w,
                output[1].dtw1.w,
                output[1].dtw2.w
            ).magnitude;

            Assert(
                dist0 > dist1,
                $"相同 Order 下的距离排序逻辑错误: 远端({dist0}) 应当在近端({dist1}) 之前绘制"
            );

            LogStep("验证同位置同 Order 下，后生成的动态贴花后绘制...");
            DecalSystemMini.ReleaseAll();
            DecalSystemMini.SpawnRuntimeDecal(Vector3.up, Quaternion.identity, Vector3.one, 0, sortingOrder: 12000);
            DecalSystemMini.SpawnRuntimeDecal(Vector3.up, Quaternion.identity, Vector3.one, 0, sortingOrder: 12000);

            count = DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            Assert(count == 2, "同层级序号排序测试数据量异常");

            int sequence0 = (int)
                sortBuffer
                    .GetValue(0)
                    .GetType()
                    .GetField("Sequence")
                    .GetValue(sortBuffer.GetValue(0));
            int sequence1 = (int)
                sortBuffer
                    .GetValue(1)
                    .GetType()
                    .GetField("Sequence")
                    .GetValue(sortBuffer.GetValue(1));

            Assert(
                sequence0 < sequence1,
                $"同位置同 Order 下的生成顺序逻辑错误: 旧贴花({sequence0}) 应在新贴花({sequence1}) 之前绘制"
            );
        }

        // =================================================================================
        // [STRESS] 用例 11：全系统 TotalCount 计数完整性 (防止渲染丢失)
        // =================================================================================
        private static void Test_TotalCount_Integrity()
        {
            LogStep("清空系统并注入 10 个静态贴花...");
            DecalSystemMini.ReleaseAll();
            var level = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            for (int i = 0; i < 10; i++)
                level.entries.Add(new DecalStaticEntry { position = Vector3.forward * i });
            level.LoadIntoKernel();

            LogStep("注入 5 个运行时贴花...");
            for (int i = 0; i < 5; i++)
                DecalSystemMini.SpawnRuntimeDecal(
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    0,
                    100f
                );

            LogStep("验证 TotalCount 是否等于 15...");
            int total = DecalSystemMini.TotalCount;
            Assert(
                total == 15,
                $"TotalCount 计数不完整! 预期 15, 实际 {total}。这会导致渲染缓冲区分配不足！"
            );

            LogStep("模拟运行时贴花过期...");
            // 通过反射强制修改过期时间
            var expirationsField = typeof(DecalSystemMini).GetField(
                "_runtimeExpirations",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            float[] expirations = (float[])expirationsField.GetValue(null);
            for (int i = 0; i < 1024; i++)
                expirations[i] = -1f;

            LogStep("验证 TotalCount 是否回落到 10 (仅剩静态贴花)...");
            total = DecalSystemMini.TotalCount;
            Assert(
                total == 10,
                $"运行时贴花过期后 TotalCount 异常: {total}。应当保留静态贴花的计数！"
            );

            DecalSystemMini.ReleaseAll();
            Object.DestroyImmediate(level);
        }

        // =================================================================================
        // [STRESS] 用例 12：核心渲染循环 0-GC 压力测试 (移动端性能命脉)
        // =================================================================================
        private static void Test_ZeroGC_Rendering()
        {
            LogStep("准备大量测试数据 (100 个贴花)...");
            DecalSystemMini.ReleaseAll();
            for (int i = 0; i < 100; i++)
                DecalSystemMini.SpawnRuntimeDecal(
                    Random.insideUnitSphere * 10f,
                    Quaternion.identity,
                    Vector3.one,
                    0,
                    100f
                );

            DecalDataMini[] output = new DecalDataMini[200];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 100f);

            // 预热：消除 JIT 编译和 Unity 内部延迟分配的影响
            for (int i = 0; i < 10; i++)
            {
                DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            }

            LogStep("执行 1000 次数据填充，监测 GC 分配...");
            System.GC.Collect();
            long initialMemory = System.GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);
            }

            long finalMemory = System.GC.GetAllocatedBytesForCurrentThread();
            long diff = finalMemory - initialMemory;

            // 完美的 0-GC 渲染循环应当不分配任何堆内存。允许 1024 字节以内的微小 JIT 或 profiling 辅助开销。
            Assert(
                diff < 1024,
                $"渲染循环检测到 GC 分配! 增量: {diff} 字节。这对移动端性能是致命的！"
            );

            DecalSystemMini.ReleaseAll();
        }

        // =================================================================================
        // [SAFETY] 用例 13：纹理索引非法边界防御 (防止 GPU 挂死)
        // =================================================================================
        private static void Test_TextureIndex_Safety()
        {
            LogStep("注入一个带非法纹理索引 (999) 的贴花...");
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                999,
                100f
            );

            DecalDataMini[] output = new DecalDataMini[1];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 100f);

            DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);

            LogStep("验证输出数据中的索引是否被系统处理...");
            // 注意：目前的系统可能是在 Shader 中做 Clamping，也可能在 C# 做
            // 如果是在 C# 做，这里应该断言它被修正了。
            // 假设我们要求 C# 层进行基础防御：
            int idx = (int)output[0].fadeParams.z;

            // 如果配置里只有 8 张图，999 应该被处理（这里我们检查它是否仍然是 999）
            // 如果目前的逻辑还没做 C# 层的 Clamping，这个测试可以作为未来的需求。
            // 让我们先写一个观察性断言：
            Assert(idx != 999, "系统允许非法纹理索引 (999) 进入渲染数据，这会导致 GPU 读取越界！");

            DecalSystemMini.ReleaseAll();
        }

        // =================================================================================
        // [SAFETY] 用例 14：动静态贴花生命周期隔离验证 (回归测试)
        // =================================================================================
        private static void Test_Lifecycle_Isolation()
        {
            LogStep("注入 1 个静态贴花和 1 个即将过期的动态贴花...");
            DecalSystemMini.ReleaseAll();
            var level = ScriptableObject.CreateInstance<DecalLevelDataMini>();
            level.entries.Add(
                new DecalStaticEntry { position = Vector3.forward * 5, layerMask = -1 }
            );
            level.LoadIntoKernel();

            // 注入一个生存期极短的动态贴花
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                0,
                0.1f
            );

            DecalDataMini[] output = new DecalDataMini[10];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 100f);

            LogStep("等待动态贴花过期...");
            // 模拟时间流逝 (通过反射修改过期时间)
            var expirationsField = typeof(DecalSystemMini).GetField(
                "_runtimeExpirations",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            float[] expirations = (float[])expirationsField.GetValue(null);
            expirations[0] = -1f; // 强制过期

            LogStep("执行 FillData 并验证静态贴花是否依然存在...");
            int count = DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);

            Assert(
                count == 1,
                $"生命周期隔离失效! 动态贴花过期后，静态贴花也消失了 (Count: {count})。"
            );

            DecalSystemMini.ReleaseAll();
            Object.DestroyImmediate(level);
        }

        // =================================================================================
        // [SAFETY] 用例 15：动态贴花 Alpha 淡出逻辑校验
        // =================================================================================
        private static void Test_Dynamic_Fade_Logic()
        {
            LogStep("注入一个剩余寿命为 0.5 秒的动态贴花 (应触发淡出)...");
            DecalSystemMini.ReleaseAll();
            DecalSystemMini.SpawnRuntimeDecal(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                0,
                100f
            );

            // 通过反射强制修改其过期时间，使其进入最后的 0.5s 淡出期
            var expirationsField = typeof(DecalSystemMini).GetField(
                "_runtimeExpirations",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            float[] expirations = (float[])expirationsField.GetValue(null);

            // 必须使用 GetCurrentTime() 以适配编辑器环境
            float now = (float)
                typeof(DecalSystemMini)
                    .GetMethod("GetCurrentTime", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);
            expirations[0] = now + 0.5f;

            DecalDataMini[] output = new DecalDataMini[10];
            Plane[] frustum = new Plane[6];
            for (int i = 0; i < 6; i++)
                frustum[i] = new Plane(Vector3.up, 100f);

            DecalSystemMini.FillData(Vector3.zero, 100f, -1, output, frustum);

            LogStep("验证 Alpha 是否被正确裁剪 (0.5s 预期 alpha 约为 0.5)...");
            float alpha = output[0].color.w;

            Assert(
                alpha > 0.4f && alpha < 0.6f,
                $"淡出逻辑计算错误! 剩余 0.5s 时 Alpha  为 {alpha}，预期接近 0.5。"
            );

            DecalSystemMini.ReleaseAll();
        }
    }
}
