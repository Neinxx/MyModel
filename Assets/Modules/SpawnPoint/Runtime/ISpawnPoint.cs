using UnityEngine;

namespace SpawnPoint.Runtime
{
    /// <summary>
    /// 生成点标准化接口
    /// 允许逻辑层在不依赖具体实现的情况下操作生成点
    /// </summary>
    public interface ISpawnPoint
    {
        string ID { get; }
        int TeamIndex { get; }
        int Priority { get; }
        bool IsBlocked { get; }
        Transform SpawnTransform { get; }
        Vector3 Position { get; }
        Quaternion Rotation { get; }
    }
}
