using System;
using System.Collections.Generic;
using CharacterShader.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    [FilePath("ProjectSettings/CharacterShaderProfileBindings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class CharacterShaderProfileBindingStore : ScriptableSingleton<CharacterShaderProfileBindingStore>
    {
        [SerializeField] private List<Binding> bindings = new List<Binding>();

        public CharacterMaterialProfile GetProfile(Material material)
        {
            string materialGuid = GetGuid(material);
            if (string.IsNullOrEmpty(materialGuid))
            {
                return null;
            }

            Binding binding = bindings.Find(item => item.materialGuid == materialGuid);
            if (binding == null || string.IsNullOrEmpty(binding.profileGuid))
            {
                return null;
            }

            string profilePath = AssetDatabase.GUIDToAssetPath(binding.profileGuid);
            return AssetDatabase.LoadAssetAtPath<CharacterMaterialProfile>(profilePath);
        }

        public void SetProfile(Material material, CharacterMaterialProfile profile)
        {
            string materialGuid = GetGuid(material);
            if (string.IsNullOrEmpty(materialGuid))
            {
                return;
            }

            Binding binding = bindings.Find(item => item.materialGuid == materialGuid);
            if (binding == null)
            {
                binding = new Binding { materialGuid = materialGuid };
                bindings.Add(binding);
            }

            binding.profileGuid = GetGuid(profile);
            Save(true);
        }

        private static string GetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        [Serializable]
        private sealed class Binding
        {
            public string materialGuid;
            public string profileGuid;
        }
    }
}
