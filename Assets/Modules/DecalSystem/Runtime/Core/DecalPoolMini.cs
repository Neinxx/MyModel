using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DecalMini
{
    /// <summary>
    /// 贴花对象池管理器：基于 UnityEngine.Pool 实现，解决高频脚印的 GC 压力
    /// </summary>
    public static class DecalPoolMini
    {
        // ========================================================================
        // 1. 核心字段
        // ========================================================================

        private static ObjectPool<DecalProjectorMini> _pool;
        private static DecalProjectorMini _prefab;
        private static readonly Dictionary<int, PoolState> _pools = new();
        private static readonly Dictionary<DecalProjectorMini, int> _instanceToPoolId = new();
        private static int _defaultPoolId;

        private sealed class PoolState
        {
            public int id;
            public DecalProjectorMini prefab;
            public ObjectPool<DecalProjectorMini> pool;
        }

        // ========================================================================
        // 2. 初始化与清理
        // ========================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }

        /// <summary>
        /// 配置并激活对象池
        /// </summary>
        public static void Init(DecalProjectorMini prefab, int defaultCap = 20, int maxCap = 100)
        {
            if (prefab == null)
                return;

            int prefabId = prefab.GetInstanceID();
            if (!_pools.ContainsKey(prefabId))
                _pools[prefabId] = CreatePool(prefab, defaultCap, maxCap);

            _defaultPoolId = prefabId;
            _prefab = prefab;
            _pool = _pools[prefabId].pool;

            DecalSystemLog.Verbose(
                $"[DecalPool] Initialized with prefab: {prefab.name} (Cap: {defaultCap}/{maxCap})"
            );
        }

        // ========================================================================
        // 3. 公共 API
        // ========================================================================

        public static DecalProjectorMini Get() => _pool?.Get();

        public static DecalProjectorMini Get(DecalProjectorMini prefab)
        {
            if (prefab == null)
                return Get();

            Init(prefab);
            return _pools.TryGetValue(prefab.GetInstanceID(), out var state)
                ? state.pool.Get()
                : null;
        }

        public static void Release(DecalProjectorMini projector)
        {
            if (projector == null)
                return;

            if (_instanceToPoolId.TryGetValue(projector, out int poolId) &&
                _pools.TryGetValue(poolId, out var state))
            {
                state.pool.Release(projector);
                return;
            }

            _pool?.Release(projector);
        }

        public static void Clear()
        {
            foreach (var state in _pools.Values)
            {
                state.pool.Clear();
            }

            _pools.Clear();
            _instanceToPoolId.Clear();
            _pool = null;
            _prefab = null;
            _defaultPoolId = 0;
        }

        // ========================================================================
        // 4. 对象池生命周期回调
        // ========================================================================

        private static PoolState CreatePool(DecalProjectorMini prefab, int defaultCap, int maxCap)
        {
            var state = new PoolState
            {
                id = prefab.GetInstanceID(),
                prefab = prefab,
            };

            state.pool = new ObjectPool<DecalProjectorMini>(
                () => CreatePooledItem(state),
                OnTakeFromPool,
                OnReturnedToPool,
                OnDestroyPoolObject,
                true,
                defaultCap,
                maxCap
            );

            return state;
        }

        private static DecalProjectorMini CreatePooledItem(PoolState state)
        {
            if (state == null || state.prefab == null)
                return null;

            var go = Object.Instantiate(state.prefab);
            go.gameObject.SetActive(false);

            if (!Application.isPlaying)
                go.gameObject.hideFlags = HideFlags.DontSave;

            _instanceToPoolId[go] = state.id;
            return go;
        }

        private static void OnTakeFromPool(DecalProjectorMini projector)
        {
            if (projector == null || projector.gameObject == null)
                return;

            projector.gameObject.SetActive(true);
        }

        private static void OnReturnedToPool(DecalProjectorMini projector)
        {
            if (projector == null || projector.gameObject == null)
                return;

            projector.gameObject.SetActive(false);
        }

        private static void OnDestroyPoolObject(DecalProjectorMini projector)
        {
            if (projector == null || projector.gameObject == null)
                return;

            _instanceToPoolId.Remove(projector);

            if (Application.isPlaying)
                Object.Destroy(projector.gameObject);
            else
                Object.DestroyImmediate(projector.gameObject);
        }
    }
}
