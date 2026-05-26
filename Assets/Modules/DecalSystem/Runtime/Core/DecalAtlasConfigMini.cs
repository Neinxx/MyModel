using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    // ========================================================================
    // 1. 基础数据项
    // ========================================================================

    [System.Serializable]
    public class DecalSliceMini
    {
        public string name;
        public Texture2D albedoMap;
        public Texture2D normalMap;
    }

    public enum DecalCompressionFormat
    {
        Uncompressed,
        ASTC_4x4,
        ASTC_6x6,
        ETC2,
        RGBA32,
        DXT5
    }

    /// <summary>
    /// 贴画图集配置资源：定义渲染上限、烘焙参数与序列帧扫描规则
    /// </summary>
    [CreateAssetMenu(fileName = "DecalAtlasConfig", menuName = "Decal System/Core/Atlas Config")]
    public class DecalAtlasConfigMini : ScriptableObject
    {
        // ========================================================================
        // 2. 运行时内核配置
        // ========================================================================

        // [Header("Runtime Kernel")]
        public int maxStaticDecals = 65536;
        public float spatialGridSize = 10.0f;

        // [Header("Spawn Defaults")]
        public Vector3 defaultSize = Vector3.one;
        public LayerMask defaultLayer = 1;
        public LayerMask creationRaycastLayer = -1;

        // ========================================================================
        // 3. 烘焙与资产管理
        // ========================================================================

        // [Header("Baking Settings")]
        public int textureSize = 512;
        public DecalCompressionFormat compressionFormat = DecalCompressionFormat.ASTC_4x4;
        public bool showDebugGrid = false;

        // [Header("Asset Paths")]
        public string sourcePath = "";
        public string exportPath = "";

        // [Header("Atlas Content")]
        public List<DecalSliceMini> slices = new();

        // [Header("Baked Assets")]
        public Texture2DArray bakedArray;
        public Texture2DArray bakedNormalArray;

        // ========================================================================
        // 4. 内部缓存与处理
        // ========================================================================

        private Dictionary<Texture2D, int> _indexCache;
        private Dictionary<Texture2D, int> _flipbookCache;

        private void OnEnable() => RebuildCache();

        private void OnValidate() => RebuildCache();

        // ========================================================================
        // 5. 公共 API
        // ========================================================================

        public int Count => slices?.Count ?? 0;

        public int GetTextureIndex(Texture2D tex)
        {
            if (tex == null)
                return -1;
            if (_indexCache == null)
                RebuildCache();
            return _indexCache.TryGetValue(tex, out int index) ? index : -1;
        }

        public int GetFlipbookCount(Texture2D tex)
        {
            if (tex == null || _flipbookCache == null)
                return 1;
            return _flipbookCache.TryGetValue(tex, out int count) ? count : 1;
        }

        public string GetName(int index)
        {
            if (index < 0 || index >= Count)
                return "Empty";
            var slice = slices[index];
            return !string.IsNullOrEmpty(slice.name)
                ? slice.name
                : (slice.albedoMap != null ? slice.albedoMap.name : $"Slot {index}");
        }

        // ========================================================================
        // 6. 逻辑实现 (Implementation)
        // ========================================================================

        /// <summary>
        /// 重建索引字典与序列帧信息 (智能扫描命名规范)
        /// </summary>
        public void RebuildCache()
        {
            _indexCache ??= new Dictionary<Texture2D, int>();
            _flipbookCache ??= new Dictionary<Texture2D, int>();

            _indexCache.Clear();
            _flipbookCache.Clear();

            // 1. 基础索引
            for (int i = 0; i < slices.Count; i++)
            {
                if (slices[i].albedoMap != null && !_indexCache.ContainsKey(slices[i].albedoMap))
                {
                    _indexCache.Add(slices[i].albedoMap, i);
                }
            }

            // 2. 序列帧扫描
            ScanFlipbooks();
        }

        private void ScanFlipbooks()
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(.+)_f_(\d+)$");
            var groups = new Dictionary<string, List<Texture2D>>();

            foreach (var slice in slices)
            {
                if (slice.albedoMap == null)
                    continue;
                var match = regex.Match(slice.albedoMap.name);
                if (match.Success)
                {
                    string groupName = match.Groups[1].Value;
                    if (!groups.ContainsKey(groupName))
                        groups[groupName] = new List<Texture2D>();
                    groups[groupName].Add(slice.albedoMap);
                }
            }

            foreach (var group in groups.Values)
            {
                if (group.Count <= 1)
                    continue;
                group.Sort((a, b) => string.Compare(a.name, b.name));
                _flipbookCache[group[0]] = group.Count;
            }
        }
    }
}
