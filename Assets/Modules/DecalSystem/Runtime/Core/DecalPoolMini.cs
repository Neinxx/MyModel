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

        // ========================================================================
        // 2. 初始化与清理
        // ========================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_pool != null)
            {
                _pool.Clear();
                _pool = null;
            }
            _prefab = null;
        }

        /// <summary>
        /// 配置并激活对象池
        /// </summary>
        public static void Init(DecalProjectorMini prefab, int defaultCap = 20, int maxCap = 100)
        {
            if (_pool != null)
                return;

            _prefab = prefab;
            _pool = new ObjectPool<DecalProjectorMini>(
                CreatePooledItem,
                OnTakeFromPool,
                OnReturnedToPool,
                OnDestroyPoolObject,
                true,
                defaultCap,
                maxCap
            );

            Debug.Log(
                $"[DecalPool] Initialized with prefab: {prefab.name} (Cap: {defaultCap}/{maxCap})"
            );
        }

        // ========================================================================
        // 3. 公共 API
        // ========================================================================

        public static DecalProjectorMini Get() => _pool?.Get();

        public static void Release(DecalProjectorMini projector)
        {
            if (_pool != null && projector != null)
                _pool.Release(projector);
        }

        public static void Clear() => _pool?.Clear();

        // ========================================================================
        // 4. 对象池生命周期回调
        // ========================================================================

        private static DecalProjectorMini CreatePooledItem()
        {
            var go = Object.Instantiate(_prefab);
            go.gameObject.SetActive(false);

            if (!Application.isPlaying)
                go.gameObject.hideFlags = HideFlags.DontSave;

            return go;
        }

        private static void OnTakeFromPool(DecalProjectorMini projector) =>
            projector.gameObject.SetActive(true);

        private static void OnReturnedToPool(DecalProjectorMini projector) =>
            projector.gameObject.SetActive(false);

        private static void OnDestroyPoolObject(DecalProjectorMini projector)
        {
            if (projector == null || projector.gameObject == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(projector.gameObject);
            else
                Object.DestroyImmediate(projector.gameObject);
        }
    }
}
