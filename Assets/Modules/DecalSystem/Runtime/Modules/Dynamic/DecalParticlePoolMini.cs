using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 高性能粒子池：支持编辑器模拟与运行时高频调用，解决特效产生的 GC 与内存抖动
    /// </summary>
    public static class DecalParticlePoolMini
    {
        // ========================================================================
        // 1. 内部辅助结构
        // ========================================================================

        private struct ActiveInfo
        {
            public ParticleSystem instance;
            public int prefabId;
            public float stopTime;
            public float recycleTime;
            public bool isStopping;
        }

        // ========================================================================
        // 2. 静态字段与池数据
        // ========================================================================

        private static readonly Dictionary<int, Stack<ParticleSystem>> _pools = new();
        private static readonly List<ActiveInfo> _activeParticles = new();
        private static DecalParticleManagerBridge _runtimeBridge;
        private static float _lastEditorTime = -1f;

        public static int maxCapacityPerPrefab = 50;
        public static int ActiveCount => _activeParticles.Count;

        // ========================================================================
        // 3. 初始化与生命周期
        // ========================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pools.Clear();
            _activeParticles.Clear();
            _runtimeBridge = null;
        }

        // ========================================================================
        // 4. 公共 API
        // ========================================================================

        /// <summary>
        /// 播放粒子特效
        /// </summary>
        public static void Play(
            ParticleSystem prefab,
            Vector3 position,
            Quaternion rotation,
            float duration
        )
        {
            if (prefab == null)
                return;

            int pid = prefab.GetInstanceID();
            ParticleSystem instance = GetFromPool(prefab, pid);

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);

            var main = instance.main;
            main.loop = true;
            instance.Play(true);

            _activeParticles.Add(
                new ActiveInfo
                {
                    instance = instance,
                    prefabId = pid,
                    stopTime = Time.realtimeSinceStartup + duration,
                    recycleTime =
                        Time.realtimeSinceStartup
                        + duration
                        + main.startLifetime.constantMax
                        + 0.5f,
                    isStopping = false,
                }
            );

            if (Application.isPlaying && _activeParticles.Count == 1)
                EnsureRuntimeManager();
        }

        public static void TickEditorSimulation()
        {
            if (Application.isPlaying || _activeParticles.Count == 0)
            {
                _lastEditorTime = -1f;
                return;
            }

            float now = Time.realtimeSinceStartup;
            float deltaTime = _lastEditorTime > 0 ? now - _lastEditorTime : 0.02f;
            _lastEditorTime = now;

            TickParticles(now, deltaTime, true);
        }

        public static void ClearAll()
        {
            foreach (var stack in _pools.Values)
            {
                foreach (var ps in stack)
                    if (ps != null)
                        SafeDestroy(ps.gameObject);
            }
            _pools.Clear();

            foreach (var info in _activeParticles)
                if (info.instance != null)
                    SafeDestroy(info.instance.gameObject);
            _activeParticles.Clear();

            if (_runtimeBridge != null)
            {
                SafeDestroy(_runtimeBridge.gameObject);
                _runtimeBridge = null;
            }
        }

        // ========================================================================
        // 5. 模拟与更新逻辑 (Simulation)
        // ========================================================================

        private static void RuntimeUpdate()
        {
            if (_activeParticles.Count == 0)
                return;
            TickParticles(Time.realtimeSinceStartup, Time.deltaTime, false);
        }

        private static void TickParticles(float now, float deltaTime, bool isEditor)
        {
            for (int i = _activeParticles.Count - 1; i >= 0; i--)
            {
                var info = _activeParticles[i];
                if (info.instance == null)
                {
                    _activeParticles.RemoveAt(i);
                    continue;
                }

                if (isEditor)
                    info.instance.Simulate(deltaTime, true, false, false);

                // 阶段 1：停止发射
                if (!info.isStopping && now >= info.stopTime)
                {
                    info.instance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    info.isStopping = true;
                    _activeParticles[i] = info;
                }

                // 阶段 2：归还池
                if (now >= info.recycleTime)
                {
                    RecycleInstance(info);
                    _activeParticles.RemoveAt(i);
                }
            }
        }

        // ========================================================================
        // 6. 内部管理逻辑 (Internal)
        // ========================================================================

        private static ParticleSystem GetFromPool(ParticleSystem prefab, int pid)
        {
            if (_pools.TryGetValue(pid, out var stack))
            {
                while (stack.Count > 0)
                {
                    var instance = stack.Pop();
                    if (instance != null)
                        return instance;
                }
            }

            var newInstance = Object.Instantiate(prefab);
            if (!Application.isPlaying)
                newInstance.gameObject.hideFlags = HideFlags.DontSave;

            EnsureRuntimeManager();
            if (_runtimeBridge != null)
                newInstance.transform.SetParent(_runtimeBridge.transform);

            if (!_pools.ContainsKey(pid))
                _pools[pid] = new Stack<ParticleSystem>();
            return newInstance;
        }

        private static void RecycleInstance(ActiveInfo info)
        {
            info.instance.gameObject.SetActive(false);
            if (_pools[info.prefabId].Count < maxCapacityPerPrefab)
            {
                _pools[info.prefabId].Push(info.instance);
            }
            else
            {
                SafeDestroy(info.instance.gameObject);
            }
        }

        private static void EnsureRuntimeManager()
        {
            if (_runtimeBridge != null)
                return;

            var bridges = Resources.FindObjectsOfTypeAll<DecalParticleManagerBridge>();
            foreach (var b in bridges)
            {
                if (b.gameObject.scene.isLoaded || b.gameObject.hideFlags == HideFlags.DontSave)
                {
                    _runtimeBridge = b;
                    return;
                }
            }

            GameObject go = new("[DecalParticleManager]");
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(go);
            else
                go.hideFlags = HideFlags.DontSave;

            _runtimeBridge = go.AddComponent<DecalParticleManagerBridge>();
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        // 内部更新桥接器
        private class DecalParticleManagerBridge : MonoBehaviour
        {
            private void Update() => RuntimeUpdate();
        }
    }
}
