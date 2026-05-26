using UnityEngine;

namespace DecalMini
{
    public sealed class DecalRuntime : IDecalRuntime
    {
        public static IDecalRuntime Shared { get; } = new DecalRuntime();

        public int TotalCount => DecalSystemMini.TotalCount;
        public int ProjectorCount => DecalSystemMini.Count;
        public int ActiveRuntimeCells => DecalSystemMini.ActiveRuntimeCells;

        public void SpawnRuntimeDecal(
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            Texture2D texture,
            float duration,
            Color color,
            float softFade = 0.5f,
            int sortingOrder = 10000
        )
        {
            DecalSystemMini.SpawnRuntimeDecal(
                position,
                rotation,
                size,
                texture,
                duration,
                color,
                softFade,
                sortingOrder
            );
        }

        public void ClearRuntimePool()
        {
            DecalSystemMini.ClearRuntimePool();
        }
    }
}
