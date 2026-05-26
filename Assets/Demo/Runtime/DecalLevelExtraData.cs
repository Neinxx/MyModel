using UnityEngine;
using DecalMini;

namespace ModularDemo.Runtime
{
    public class DecalLevelExtraData : MonoBehaviour, IDecalLevelDataProvider
    {
        public DecalLevelDataMini levelData;

        public DecalLevelDataMini LevelData => levelData;

        private void OnDrawGizmos()
        {
            if (levelData != null)
            {
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawCube(transform.position, Vector3.one * 0.5f);
                Gizmos.DrawIcon(transform.position + Vector3.up * 0.8f, "icon_DecalData", true);
            }
        }
    }
}
