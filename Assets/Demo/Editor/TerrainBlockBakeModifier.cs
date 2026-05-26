using DecalMini;
using DecalMini.Editor;
using UnityEngine;

namespace ModularDemo.Editor
{
    /// <summary>
    /// 地形块记录器 (Middleware Implementation)
    /// 在烘焙过程中，自动检测贴花下方的地形块并将 ID 记录到 userData 字段中
    /// </summary>
    public class TerrainBlockBakeModifier : IDecalEntryModifier
    {
        public void ModifyEntry(DecalProjectorMini source, ref DecalStaticEntry entry)
        {
            // 业务逻辑：通过射线向下检测地形
            // 在实际项目中，这可能是根据投影器的位置计算所在的 TerrainData
            Ray ray = new Ray(source.transform.position + Vector3.up * 0.5f, Vector3.down);

            // 假设我们只对 Terrain 层感兴趣
            if (Physics.Raycast(ray, out RaycastHit hit, 2.0f))
            {
                var terrain = hit.collider.GetComponent<Terrain>();
                if (terrain != null)
                {
                    // 记录地形块的唯一 ID (这里简单使用 InstanceID)
                    entry.userData = terrain.GetInstanceID();

                    // Debug.Log($"[Terrain Modifier] Decal at {source.name} recorded Terrain ID: {entry.userData}");
                }
            }
        }
    }
}
