using System.Collections.Generic;
using UnityEngine;

namespace WorldSceneModule.Runtime
{
    /// <summary>
    /// 关卡数据聚合容器 (Level Module Data Container)
    /// 集中存放一个关卡下所有子模块的运行时静态/动态数据。
    /// 采用高度解耦的泛型设计，对所有具体模块（如贴花系统）零编译依赖，完美保持模块的纯净。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelModuleData", menuName = "World Scene/Level Module Data")]
    public class LevelModuleData : ScriptableObject
    {
        [Tooltip("List of aggregated module-specific ScriptableObjects (e.g. DecalData, QuestData, etc.)")]
        public List<ScriptableObject> subDatas = new List<ScriptableObject>();

        /// <summary>
        /// 泛型检索接口：获取指定类型的模块数据
        /// </summary>
        public T GetSubData<T>() where T : ScriptableObject
        {
            if (subDatas == null) return null;
            foreach (var data in subDatas)
            {
                if (data is T typedData)
                    return typedData;
            }
            return null;
        }

        /// <summary>
        /// 注册并合并子系统数据，根据具体运行时类型自动执行去重和替换
        /// </summary>
        public void RegisterSubData(ScriptableObject subData)
        {
            if (subData == null) return;
            subDatas.RemoveAll(x => x == null || x.GetType() == subData.GetType());
            subDatas.Add(subData);
        }
    }
}
