using UnityEngine;

namespace DecalMini
{
    public interface IDecalRuntime
    {
        int TotalCount { get; }
        int ProjectorCount { get; }
        int ActiveRuntimeCells { get; }

        void SpawnRuntimeDecal(
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            Texture2D texture,
            float duration,
            Color color,
            float softFade = 0.5f,
            int sortingOrder = 10000
        );

        void ClearRuntimePool();
    }
}
