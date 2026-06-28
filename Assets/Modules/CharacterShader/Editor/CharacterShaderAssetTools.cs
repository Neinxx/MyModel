using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    public static class CharacterShaderAssetTools
    {
        private const string CompatibilityShaderName = "Universal Render Pipeline/Character/NPR Array";
        private const string CoreShaderName = "Universal Render Pipeline/Character/NPR Core";
        private const string CompatibilityMaterialPath = "Assets/Modules/CharacterShader/Runtime/Compatibility/CharacterNPRArray_Default.mat";
        private const string CoreMaterialPath = "Assets/Modules/CharacterShader/Runtime/Core/CharacterNPRCore_Default.mat";

        [MenuItem("Tools/Character Shader/Create NPR Core Material")]
        public static void CreateCoreMaterial()
        {
            CreateMaterial(CoreShaderName, CoreMaterialPath, "CharacterNPRCore_Default");
        }

        [MenuItem("Tools/Character Shader/Create NPR Array Compatibility Material")]
        public static void CreateCompatibilityMaterial()
        {
            CreateMaterial(CompatibilityShaderName, CompatibilityMaterialPath, "CharacterNPRArray_Default");
        }

        private static void CreateMaterial(string shaderName, string path, string materialName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Character Shader", $"Shader not found:\n{shaderName}", "OK");
                return;
            }

            Material material = new Material(shader)
            {
                name = materialName
            };

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            Selection.activeObject = material;
        }

        [MenuItem("Tools/Character Shader/Build Texture2DArray From Selection")]
        public static void BuildTextureArrayFromSelection()
        {
            List<Texture2D> textures = GetSelectedTextures();
            if (textures.Count == 0)
            {
                EditorUtility.DisplayDialog("Character Shader", "Select at least one Texture2D asset.", "OK");
                return;
            }

            Texture2D first = textures[0];
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Texture2DArray",
                $"{first.name}_Array.asset",
                "asset",
                "Choose where to save the generated Texture2DArray.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Texture2DArray textureArray = CreateTextureArray(textures);
            AssetDatabase.CreateAsset(textureArray, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = textureArray;
        }

        [MenuItem("Tools/Character Shader/Build Texture2DArray From Selection", true)]
        private static bool BuildTextureArrayFromSelectionValidate()
        {
            return GetSelectedTextures().Count > 0;
        }

        private static List<Texture2D> GetSelectedTextures()
        {
            List<Texture2D> textures = new List<Texture2D>();
            foreach (Object selected in Selection.objects)
            {
                if (selected is Texture2D texture)
                {
                    textures.Add(texture);
                }
            }

            textures.Sort((left, right) => string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right)));
            return textures;
        }

        private static Texture2DArray CreateTextureArray(IReadOnlyList<Texture2D> sources)
        {
            int width = sources[0].width;
            int height = sources[0].height;
            bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
            Texture2DArray textureArray = new Texture2DArray(width, height, sources.Count, TextureFormat.RGBA32, true, linear)
            {
                name = $"{sources[0].name}_Array",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = !linear,
                useMipMap = false,
                autoGenerateMips = false
            };

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(descriptor);
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);

            try
            {
                for (int slice = 0; slice < sources.Count; slice++)
                {
                    Texture2D source = sources[slice];
                    if (source.width != width || source.height != height)
                    {
                        Debug.LogWarning($"[CharacterShader] Resampling {source.name} from {source.width}x{source.height} to {width}x{height}.");
                    }

                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    readable.Apply(false, false);
                    textureArray.SetPixels(readable.GetPixels(), slice, 0);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                Object.DestroyImmediate(readable);
            }

            textureArray.Apply(true, true);
            return textureArray;
        }
    }
}
