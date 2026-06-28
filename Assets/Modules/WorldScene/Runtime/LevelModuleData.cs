using System.Collections.Generic;
using UnityEngine;

namespace WorldSceneModule.Runtime
{
    [CreateAssetMenu(fileName = "LevelModuleData", menuName = "World Scene/Level Module Data")]
    public class LevelModuleData : ScriptableObject
    {
        [Tooltip("List of aggregated module-specific ScriptableObjects (e.g. DecalData, QuestData, etc.)")]
        public List<ScriptableObject> subDatas = new List<ScriptableObject>();

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

        public void RegisterSubData(ScriptableObject subData)
        {
            if (subData == null) return;
            subDatas.RemoveAll(x => x == null || x.GetType() == subData.GetType());
            subDatas.Add(subData);
        }
    }
}
