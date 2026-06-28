using System.Collections.Generic;
using ResourceManagerModule.Runtime;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ResourceManagerModule.Editor
{
    public static class AddressablesResourceCatalogGenerator
    {
        private const string CatalogAssetPath = "Assets/Resources/AddressablesResourceCatalog.asset";
        private const string ResourcesFolderPath = "Assets/Resources";

        [MenuItem("Tools/Resource Manager/Rebuild Addressables Catalog")]
        public static void RebuildCatalog()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[ResourceManager] Addressables settings were not found. Catalog was not rebuilt.");
                return;
            }

            EnsureResourcesFolder();

            AddressablesResourceCatalog catalog = AssetDatabase.LoadAssetAtPath<AddressablesResourceCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AddressablesResourceCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.SetKeys(CollectKeys(settings));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ResourceManager] Rebuilt Addressables catalog: {catalog.Keys.Count} keys.");
        }

        public static List<string> NormalizeKeys(IEnumerable<string> keys)
        {
            var normalizedKeys = new List<string>();
            if (keys == null)
            {
                return normalizedKeys;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key) || normalizedKeys.Contains(key))
                {
                    continue;
                }

                normalizedKeys.Add(key);
            }

            normalizedKeys.Sort(System.StringComparer.Ordinal);
            return normalizedKeys;
        }

        private static List<string> CollectKeys(AddressableAssetSettings settings)
        {
            var keys = new List<string>();
            foreach (var group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.address))
                    {
                        continue;
                    }

                    keys.Add(entry.address);
                }
            }

            return NormalizeKeys(keys);
        }

        private static void EnsureResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
