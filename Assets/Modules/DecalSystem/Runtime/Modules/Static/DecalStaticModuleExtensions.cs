using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 静态数据加载模块扩展
    /// 为内核提供 ScriptableObject 资产的加载桥接
    /// </summary>
    public static class DecalStaticModuleExtensions
    {
        /// <summary>
        /// 将持久化资产加载进贴花内核
        /// </summary>
        public static void LoadIntoKernel(this DecalLevelDataMini levelData)
        {
            if (levelData == null || levelData.entries == null)
                return;

            // 调用内核纯数据 API：(唯一标识符, 数据集合)
            DecalSystemMini.RegisterStaticData(levelData, levelData.entries);
        }

        /// <summary>
        /// 从贴花内核中卸载资产
        /// </summary>
        public static void UnloadFromKernel(this DecalLevelDataMini levelData)
        {
            if (levelData == null)
                return;

            // 使用资产对象本身作为唯一标识符进行卸载
            DecalSystemMini.UnregisterStaticData(levelData);
        }
    }
}
