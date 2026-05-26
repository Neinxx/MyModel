using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor
{
    /// <summary>
    /// 贴画图集烘焙工具：支持自动采集、瘦身烘焙以及 Texture2DArray 自动化生成
    /// </summary>
    public static class DecalAtlasBakingToolMini
    {
        // ========================================================================
        // 1. 公共 API (Entry Points)
        // ========================================================================

        /// <summary>
        /// 扫描路径并全自动烘焙图集
        /// </summary>
        public static void AutoCollectAndBake(DecalAtlasConfigMini config)
        {
            if (config == null)
                return;

            string rootDir = ResolveSourceRoot(config);
            if (!AssetDatabase.IsValidFolder(rootDir))
                return;

            if (CollectTextures(config, rootDir))
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            Bake(config);
        }

        /// <summary>
        /// 仅烘焙当前项目中被引用到的贴图（资产瘦身）
        /// </summary>
        public static void BakeWithSlimming(DecalAtlasConfigMini config)
        {
            if (config == null)
                return;
            if (
                !EditorUtility.DisplayDialog(
                    "Slim & Bake",
                    "系统将自动剔除未使用的贴花资源，确定继续吗？",
                    "确定",
                    "取消"
                )
            )
                return;

            var usedTextures = CollectReferencedTextures(config);
            var newSlices = config
                .slices.Where(s => s.albedoMap != null && usedTextures.Contains(s.albedoMap))
                .ToList();

            config.slices = newSlices;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Bake(config);
        }

        /// <summary>
        /// 核心烘焙流程：生成 Texture2DArray 并导出资产
        /// </summary>
        public static void Bake(DecalAtlasConfigMini config)
        {
            if (config == null)
                return;

            // 1. 预处理：剔除无效切片，防止索引 0 出现空洞
            config.slices.RemoveAll(s => s.albedoMap == null);
            if (config.Count == 0)
                return;

            int count = config.Count;
            int size = config.textureSize;
            bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            TextureFormat format = GetTargetFormat(config.compressionFormat);

            // 2. 初始化容器 (法线必须为 Linear，Albedo 根据项目设置)
            var albedoArray = CreateArray(
                size,
                count,
                !isLinear, 
                $"{config.name}_Albedo_Array",
                format
            );
            var normalArray = CreateArray(
                size,
                count,
                false, // sRGB = false => Linear = true (法线硬性要求)
                $"{config.name}_Normal_Array",
                format
            );

            // 3. 逐层烘焙
            ExecuteBakeLoop(config, albedoArray, normalArray, size, isLinear, format);

            // 4. 资产持久化
            string exportFolder = ResolveExportFolder(config);
            config.bakedArray = SaveAsset(config, albedoArray, "_Albedo_Array", exportFolder);
            config.bakedNormalArray = SaveAsset(config, normalArray, "_Normal_Array", exportFolder);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"<color=#76E2FF><b>[Decal Baker]</b></color> 烘焙完成：{count} 层数据已同步。"
            );
        }

        // ========================================================================
        // 2. 资产扫描与分析 (Scanning)
        // ========================================================================

        private static bool CollectTextures(DecalAtlasConfigMini config, string rootDir)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootDir });
            bool isChanged = false;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("_Array"))
                    continue;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null || Path.GetFileName(path).ToLower().Contains("normal"))
                    continue;

                var existing = config.slices.FirstOrDefault(s => s.albedoMap == tex);
                Texture2D normTex = TryFindNormalMap(path);

                if (existing != null)
                {
                    if (existing.normalMap != normTex)
                    {
                        existing.normalMap = normTex;
                        isChanged = true;
                    }
                }
                else
                {
                    config.slices.Add(
                        new DecalSliceMini
                        {
                            name = tex.name,
                            albedoMap = tex,
                            normalMap = normTex,
                        }
                    );
                    isChanged = true;
                }
            }
            return isChanged;
        }

        private static HashSet<Texture2D> CollectReferencedTextures(DecalAtlasConfigMini config)
        {
            var used = new HashSet<Texture2D>();
            var allPotentialTextures = new HashSet<Texture2D>(
                config.slices.Select(s => s.albedoMap).Where(t => t != null)
            );

            // 1. 扫描当前打开的场景
            var projs = Object.FindObjectsByType<DecalProjectorMini>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            foreach (var p in projs)
                if (p.decalTexture != null)
                    used.Add(p.decalTexture);

            // 2. 扫描所有预制体
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string[] deps = AssetDatabase.GetDependencies(path, false);
                foreach (var dep in deps)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dep);
                    if (tex != null && allPotentialTextures.Contains(tex))
                        used.Add(tex);
                }
            }

            // 3. 扫描项目中所有的场景 (解耦：不再依赖特定的注册表类型)
            string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            foreach (var sGuid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sGuid);
                // 获取场景的所有直接依赖项
                string[] sceneDeps = AssetDatabase.GetDependencies(scenePath, true);
                foreach (var dep in sceneDeps)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dep);
                    if (tex != null && allPotentialTextures.Contains(tex))
                        used.Add(tex);
                }
            }

            // 4. 核心发现引擎：获取所有外部提供者认为“已使用”的额外贴图 (如注册表指定的场景)
            foreach (var provider in DecalEditorExtensions.TextureProviders)
            {
                var extra = provider.GetUsedTextures();
                if (extra != null)
                {
                    foreach (var tex in extra)
                        if (tex != null) used.Add(tex);
                }
            }

            return used;
        }

        // ========================================================================
        // 3. 核心烘焙逻辑 (Baking Kernel)
        // ========================================================================

        private static void ExecuteBakeLoop(
            DecalAtlasConfigMini config,
            Texture2DArray albedo,
            Texture2DArray normal,
            int size,
            bool isLinear,
            TextureFormat format
        )
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                size,
                size,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear // 强制线性读取，保护法线数据不被 Gamma 偏移
            );
            try
            {
                for (int i = 0; i < config.Count; i++)
                {
                    var slice = config.slices[i];
                    EditorUtility.DisplayProgressBar(
                        "Baking Decals",
                        $"Processing: {slice.name}",
                        (float)i / config.Count
                    );

                    BakeSingleLayer(slice.albedoMap, rt, albedo, i, format, false, !isLinear);
                    BakeSingleLayer(slice.normalMap, rt, normal, i, format, true, true);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void BakeSingleLayer(
            Texture2D src,
            RenderTexture rt,
            Texture2DArray array,
            int layer,
            TextureFormat format,
            bool isNormal,
            bool isLinear
        )
        {
            RenderTexture.active = rt;
            GL.Clear(true, true, isNormal ? new Color(0.5f, 0.5f, 1.0f, 1.0f) : Color.clear);

            if (src != null)
                Graphics.Blit(src, rt);

            Texture2D temp = new Texture2D(
                rt.width,
                rt.height,
                TextureFormat.RGBA32,
                true,
                isLinear
            );
            temp.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            temp.Apply(true);
            RenderTexture.active = null;

            if (format != TextureFormat.RGBA32)
                EditorUtility.CompressTexture(temp, format, TextureCompressionQuality.Normal);

            for (int mip = 0; mip < temp.mipmapCount; mip++)
                Graphics.CopyTexture(temp, 0, mip, array, layer, mip);

            Object.DestroyImmediate(temp);
        }

        // ========================================================================
        // 4. 辅助路径与工具 (Helpers)
        // ========================================================================

        private static string ResolveSourceRoot(DecalAtlasConfigMini config)
        {
            if (!string.IsNullOrEmpty(config.sourcePath))
            {
                string path = config.sourcePath.Replace("\\", "/").TrimEnd('/');
                return path.StartsWith("Assets") ? path : "Assets/" + path;
            }
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(config));
        }

        private static string ResolveExportFolder(DecalAtlasConfigMini config)
        {
            if (!string.IsNullOrEmpty(config.exportPath))
            {
                string path = config.exportPath.Replace("\\", "/").TrimEnd('/');
                if (
                    AssetDatabase.IsValidFolder(path.StartsWith("Assets") ? path : "Assets/" + path)
                )
                    return path;
            }
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace("\\", "/");
        }

        private static Texture2D TryFindNormalMap(string albedoPath)
        {
            string dir = Path.GetDirectoryName(albedoPath);
            string name = Path.GetFileNameWithoutExtension(albedoPath);
            string ext = Path.GetExtension(albedoPath);

            // 规则：基础贴图名称 + 后缀 (优先匹配用户指定的 _normal)
            string[] suffixes = { "_normal", "_Normal", "_n", "_N" };

            foreach (var s in suffixes)
            {
                string tryPath = Path.Combine(dir, name + s + ext).Replace("\\", "/");
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tryPath);
                if (tex != null)
                    return tex;
            }
            return null;
        }

        private static TextureFormat GetTargetFormat(DecalCompressionFormat format)
        {
            return format switch
            {
                DecalCompressionFormat.ASTC_4x4 => TextureFormat.ASTC_4x4,
                DecalCompressionFormat.ASTC_6x6 => TextureFormat.ASTC_6x6,
                DecalCompressionFormat.ETC2 => TextureFormat.ETC2_RGBA8,
                DecalCompressionFormat.DXT5 => TextureFormat.DXT5,
                _ => TextureFormat.RGBA32,
            };
        }

        private static Texture2DArray CreateArray(
            int size,
            int count,
            bool sRGB,
            string name,
            TextureFormat format
        )
        {
            return new Texture2DArray(size, size, count, format, true, !sRGB)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
        }

        private static Texture2DArray SaveAsset(
            DecalAtlasConfigMini config,
            Texture2DArray array,
            string suffix,
            string folder
        )
        {
            string path = Path.Combine(folder, config.name + suffix + ".asset").Replace("\\", "/");
            Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing != null)
                EditorUtility.CopySerialized(array, existing);
            else
            {
                AssetDatabase.CreateAsset(array, path);
                existing = array;
            }
            return existing;
        }
    }
}
