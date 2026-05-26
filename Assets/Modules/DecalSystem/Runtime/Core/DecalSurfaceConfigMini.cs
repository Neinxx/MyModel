using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    // ========================================================================
    // 1. 基础数据结构
    // ========================================================================

    [System.Serializable]
    public class DecalSurfaceItem
    {
        public string surfaceName;
        public List<Material> targetMaterials = new();
    }

    /// <summary>
    /// 地表交互效果配置：定义特定地表触发的视觉反馈资产，适配脚印与射击系统
    /// </summary>
    [System.Serializable]
    public class DecalSurfaceEffect
    {
        [Header("Identity")]
        public string surfaceName;
        public PhysicMaterial physicMaterial;

        [Header("Decal Overrides")]
        public Texture2D decalTexture;
        public DecalProjectorMini decalPrefab;
        public float decalSizeMultiplier = 1.0f;

        [Header("FX Overrides")]
        public GameObject impactFX; // 适配射击系统的 GameObject 预制体
        public ParticleSystem particlePrefab; // 适配脚印系统的 ParticleSystem 预制体
    }

    /// <summary>
    /// 材质地表配置资源：定义材质所属地表类型，并关联全系统交互特效
    /// </summary>
    [CreateAssetMenu(
        fileName = "DecalSurfaceConfig",
        menuName = "Decal System/Core/Surface Config"
    )]
    public class DecalSurfaceConfigMini : ScriptableObject
    {
        // ========================================================================
        // 2. 配置内容
        // ========================================================================

        [Header("Surface Mapping (Static/Baking)")]
        public List<DecalSurfaceItem> surfaces = new();

        [Header("Surface Effects (Dynamic/Interaction)")]
        public List<DecalSurfaceEffect> surfaceEffects = new();

        // ========================================================================
        // 3. 内部缓存
        // ========================================================================

        private Dictionary<Material, string> _materialToSurfaceMap;

        private void OnEnable() => RebuildCache();

        private void OnValidate() => RebuildCache();

        // ========================================================================
        // 4. 公共 API
        // ========================================================================

        public string GetSurfaceType(Material mat)
        {
            if (mat == null)
                return "Default";
            if (_materialToSurfaceMap == null)
                RebuildCache();
            return _materialToSurfaceMap.TryGetValue(mat, out string surface) ? surface : "Default";
        }

        // ========================================================================
        // 5. 逻辑实现 (Implementation)
        // ========================================================================

        public void RebuildCache()
        {
            _materialToSurfaceMap ??= new Dictionary<Material, string>();
            _materialToSurfaceMap.Clear();

            foreach (var surface in surfaces)
            {
                if (string.IsNullOrEmpty(surface.surfaceName))
                    continue;
                foreach (var mat in surface.targetMaterials)
                {
                    if (mat != null && !_materialToSurfaceMap.ContainsKey(mat))
                    {
                        _materialToSurfaceMap.Add(mat, surface.surfaceName);
                    }
                }
            }
        }
    }
}
