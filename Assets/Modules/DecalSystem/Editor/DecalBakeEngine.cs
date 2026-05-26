using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DecalMini.Editor
{
    /// <summary>
    /// CORE BAKE ENGINE: Headless service responsible for the Decal Baking Pipeline.
    /// <para>
    /// The pipeline follows a strictly ordered execution sequence:
    /// 1. Validation: Ensures scene integrity and export paths.
    /// 2. Collection & Filtering: Gathers projectors and executes IDecalBakeFilter middleware.
    /// 3. Serialization: Converts MonoBehaviours into optimized binary structs.
    /// 4. Modification: Executes IDecalEntryModifier middleware (e.g., custom data injection).
    /// 5. Persistence: Saves the ScriptableObject asset and refreshes DB.
    /// 6. Post-Processing: Executes IDecalPostBakeProcessor middleware.
    /// </para>
    /// </summary>
    public static class DecalBakeEngine
    {
        /// <summary>
        /// Executes a full bake of the specified scene.
        /// </summary>
        /// <param name="scene">The target scene to bake.</param>
        /// <param name="exportPath">Root relative path (Assets/...) where the asset will be saved.</param>
        /// <param name="silent">If true, UI dialogs and non-essential logs are suppressed.</param>
        public static void Bake(Scene scene, string exportPath, bool silent = false)
        {
            // --- STAGE 1: VALIDATION ---
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                if (!silent)
                    EditorUtility.DisplayDialog(
                        "Bake Error",
                        "Cannot bake an unsaved or invalid scene.",
                        "OK"
                    );
                return;
            }

            // --- STAGE 2: COLLECTION & FILTERING ---
            var allProjectors = Object.FindObjectsByType<DecalProjectorMini>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            var filteredProjectors = new List<DecalProjectorMini>();

            foreach (var proj in allProjectors)
            {
                bool include = true;
                foreach (var filter in DecalEditorExtensions.BakeFilters)
                {
                    if (!filter.ShouldInclude(proj))
                    {
                        include = false;
                        break;
                    }
                }
                if (include)
                    filteredProjectors.Add(proj);
            }

            if (filteredProjectors.Count == 0)
            {
                if (!silent)
                    Debug.Log(
                        "<color=#7C8CFF><b>[Decal Bake]</b></color> No active projectors passed filters. Aborting."
                    );
                return;
            }

            // --- STAGE 3: SERIALIZATION ---
            // 自动将烘焙数据放在当前关卡场景所在的目录
            string actualExportPath = Path.GetDirectoryName(scene.path).Replace("\\", "/");
            string assetPath = $"{actualExportPath}/{scene.name}_DecalLevelData.asset";

            DecalLevelDataMini data = AssetDatabase.LoadAssetAtPath<DecalLevelDataMini>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DecalLevelDataMini>();
                if (!Directory.Exists(actualExportPath))
                    Directory.CreateDirectory(actualExportPath);
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.sceneName = scene.name;
            data.entries.Clear();

            foreach (var proj in filteredProjectors)
            {
                if (proj == null || proj.decalTexture == null)
                    continue;

                // Basic TRS to Matrix conversion
                float maxScale = Mathf.Max(
                    proj.transform.localScale.x,
                    Mathf.Max(proj.transform.localScale.y, proj.transform.localScale.z)
                );
                var entry = new DecalStaticEntry
                {
                    data = proj.ToDecalData(),
                    sortingOrder = proj.sortingOrder,
                    layerMask = 1 << proj.gameObject.layer,
                    position = proj.transform.position,
                    boundingRadius = maxScale * 0.866f,
                };

                // --- STAGE 4: MODIFICATION ---
                // Allow middleware to inject custom metadata or override rendering state
                foreach (var modifier in DecalEditorExtensions.EntryModifiers)
                {
                    modifier.ModifyEntry(proj, ref entry);
                }

                data.entries.Add(entry);
            }

            // --- STAGE 5: PERSISTENCE ---
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            // --- STAGE 6: POST-PROCESSING ---
            // Execute side-effects like registry syncing or external indexing
            foreach (var processor in DecalEditorExtensions.PostBakeProcessors)
            {
                processor.OnPostBake(data);
            }

            Debug.Log(
                $"<color=#7C8CFF><b>[Decal Bake]</b></color> Success: <color=#3FB950>{data.entries.Count}</color> static entries generated in <color=#9CDCFE>{assetPath}</color>"
            );
        }
    }
}
