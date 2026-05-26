using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    /// <summary>
    /// 直接引用提供者 (Direct Reference Provider)
    /// 允许手动向内存字典注册已存在的 Prefab/Texture 等资产，提供 O(1) 瞬时虚拟异步返回。
    /// 极其适合本地快速测试、Mocking 测试以及快速敏捷迭代。
    /// </summary>
    public class DirectRefProvider : IResourceProvider
    {
        private static readonly Dictionary<string, Object> _assetsMap = new Dictionary<string, Object>();

        /// <summary>
        /// 手动注册一个 Asset
        /// </summary>
        public static void RegisterAsset(string key, Object asset)
        {
            if (string.IsNullOrEmpty(key) || asset == null) return;
            _assetsMap[key] = asset;
        }

        /// <summary>
        /// 注销一个 Asset
        /// </summary>
        public static void UnregisterAsset(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _assetsMap.Remove(key);
        }

        /// <summary>
        /// 清空所有手动配置
        /// </summary>
        public static void Clear()
        {
            _assetsMap.Clear();
        }

        /// <summary>
        /// 精准注销指定 key 的缓存引用（适用于关卡切换前清理旧引用，防止跨场景幽灵引用）
        /// </summary>
        public static void InvalidateKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _assetsMap.Remove(key);
        }

        public Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (_assetsMap.TryGetValue(key, out var asset))
            {
                return Task.FromResult(asset as T);
            }
            return Task.FromResult<T>(null);
        }

        public void UnloadAsset(string key)
        {
            // 直接引用不需要物理销毁，仅在垃圾回收或 UnregisterAsset 时从静态 Map 清空即可
        }

        public bool CanLoad(string key)
        {
            return _assetsMap.ContainsKey(key);
        }
    }
}
