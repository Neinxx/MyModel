using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 材质交互管理器：提供基于物理材质的 O(1) 快速查询接口，驱动动态反馈系统
    /// </summary>
    public static class DecalSurfaceManagerMini
    {
        // ========================================================================
        // 1. 静态字段
        // ========================================================================

        private static DecalSurfaceConfigMini _config;
        private static readonly Dictionary<PhysicMaterial, DecalSurfaceEffect> _lookupCache = new();

        // ========================================================================
        // 2. 初始化与缓存
        // ========================================================================

        /// <summary>
        /// 初始化地表数据映射
        /// </summary>
        public static void Init(DecalSurfaceConfigMini config)
        {
            _config = config;
            _lookupCache.Clear();

            if (_config == null)
                return;

            // 建立物理材质到效果的快速索引
            foreach (var effect in _config.surfaceEffects)
            {
                if (effect.physicMaterial == null)
                    continue;

                if (!_lookupCache.ContainsKey(effect.physicMaterial))
                {
                    _lookupCache.Add(effect.physicMaterial, effect);
                }
            }

            DecalSystemLog.Verbose(
                $"<color=#7C8CFF><b>[Decal Mini]</b></color> Surface manager initialized. Loaded <color=#9CDCFE>{_lookupCache.Count}</color> interaction mappings."
            );
        }

        // ========================================================================
        // 3. 公共 API
        // ========================================================================

        /// <summary>
        /// 根据物理材质获取交互效果配置
        /// </summary>
        public static DecalSurfaceEffect GetEffect(PhysicMaterial mat)
        {
            if (mat != null && _lookupCache.TryGetValue(mat, out var effect))
            {
                return effect;
            }

            return null;
        }
    }
}
