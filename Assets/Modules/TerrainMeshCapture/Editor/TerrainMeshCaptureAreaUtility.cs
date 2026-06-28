using UnityEngine;

namespace TerrainMeshCapture.Editor
{
    internal static class TerrainMeshCaptureAreaUtility
    {
        public static Terrain ResolveTerrain(TerrainMeshCaptureArea area)
        {
            if (area == null)
            {
                return null;
            }

            Terrain[] terrains = Terrain.activeTerrains;
            Vector3 worldPosition = area.transform.position;
            Terrain fallback = null;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = terrain;
                }

                Vector3 local = terrain.transform.InverseTransformPoint(worldPosition);
                Vector3 size = terrain.terrainData.size;
                if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
                {
                    return terrain;
                }
            }

            return Terrain.activeTerrain != null ? Terrain.activeTerrain : fallback;
        }

        public static TerrainMeshCaptureSettings BuildSettings(TerrainMeshCaptureProfile profile)
        {
            var settings = new TerrainMeshCaptureSettings();
            settings.ApplyProfile(profile);
            return settings;
        }

        public static bool TryGetAreaRect(
            TerrainMeshCaptureArea area,
            TerrainMeshCaptureProfile profile,
            out Terrain terrain,
            out Rect areaRect)
        {
            terrain = ResolveTerrain(area);
            areaRect = default;
            if (area == null || profile == null || terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            Vector3 centerLocal = terrain.transform.InverseTransformPoint(area.transform.position);
            areaRect = TerrainMeshCaptureBaker.BuildRect(centerLocal, profile.AreaSize);
            return true;
        }
    }
}
