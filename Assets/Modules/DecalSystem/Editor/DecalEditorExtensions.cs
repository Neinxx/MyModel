using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor
{
    // ========================================================================
    // 1. EXTENSION INTERFACES (Middleware Hooks)
    // ========================================================================

    /// <summary>
    /// POST-BAKE PROCESSOR: Implementation allows executing side-effects after the core bake is finished.
    /// <para>Common use cases: Auto-syncing registries, generating companion metadata, notifying external systems.</para>
    /// </summary>
    public interface IDecalPostBakeProcessor
    {
        /// <summary>
        /// Triggered after the DecalLevelData asset is saved and persisted.
        /// </summary>
        /// <param name="data">The final baked asset.</param>
        void OnPostBake(DecalLevelDataMini data);
    }

    /// <summary>
    /// TEXTURE USAGE PROVIDER: Provides additional texture dependencies for the Atlas Baker.
    /// <para>Ensures that textures used in baked data (but not in scenes) are not "slimmed" away.</para>
    /// </summary>
    public interface IDecalTextureUsageProvider
    {
        /// <summary>
        /// Returns a collection of textures that must be preserved in the atlas.
        /// </summary>
        IEnumerable<Texture2D> GetUsedTextures();
    }

    /// <summary>
    /// SCENE NAME PROVIDER: Customizes the data source for the [SceneNameSelector] UI attribute.
    /// </summary>
    public interface IDecalSceneNameProvider
    {
        /// <summary>
        /// Returns a list of strings to be displayed in the property drawer dropdown.
        /// </summary>
        IEnumerable<string> GetSceneNames();
    }

    /// <summary>
    /// BAKE FILTER: Determines which projectors should be included in the bake process.
    /// <para>Allows excluding decals based on size, layer, distance, or project-specific logic.</para>
    /// </summary>
    public interface IDecalBakeFilter
    {
        /// <summary>
        /// Called during the collection phase. Return false to skip this projector.
        /// </summary>
        bool ShouldInclude(DecalProjectorMini projector);
    }

    /// <summary>
    /// ENTRY MODIFIER: Intercepts the conversion from MonoBehaviour to Static Entry.
    /// <para>Allows injecting custom data into 'userData' or modifying render properties at bake time.</para>
    /// </summary>
    public interface IDecalEntryModifier
    {
        /// <summary>
        /// Modifies the final serialized entry before it is added to the LevelData asset.
        /// </summary>
        /// <param name="source">The source projector in the scene.</param>
        /// <param name="entry">The destination struct (passed by reference).</param>
        void ModifyEntry(DecalProjectorMini source, ref DecalStaticEntry entry);
    }

    // ========================================================================
    // 2. DISCOVERY ENGINE
    // ========================================================================

    /// <summary>
    /// EXTENSION REGISTRY: Automated discovery engine for Decal System middleware.
    /// Uses Unity's TypeCache for ultra-fast, zero-config extension discovery at compile-time.
    /// </summary>
    public static class DecalEditorExtensions
    {
        private static List<IDecalPostBakeProcessor> _postBakeProcessors;
        private static List<IDecalTextureUsageProvider> _textureProviders;
        private static List<IDecalSceneNameProvider> _sceneProviders;
        private static List<IDecalBakeFilter> _bakeFilters;
        private static List<IDecalEntryModifier> _entryModifiers;

        /// <summary>所有注册的后处理器</summary>
        public static IEnumerable<IDecalPostBakeProcessor> PostBakeProcessors => _postBakeProcessors ??= CreateInstances<IDecalPostBakeProcessor>();

        /// <summary>所有注册的贴图提供者</summary>
        public static IEnumerable<IDecalTextureUsageProvider> TextureProviders => _textureProviders ??= CreateInstances<IDecalTextureUsageProvider>();

        /// <summary>所有注册的场景名称提供者</summary>
        public static IEnumerable<IDecalSceneNameProvider> SceneProviders => _sceneProviders ??= CreateInstances<IDecalSceneNameProvider>();

        /// <summary>所有注册的烘焙过滤器</summary>
        public static IEnumerable<IDecalBakeFilter> BakeFilters => _bakeFilters ??= CreateInstances<IDecalBakeFilter>();

        /// <summary>所有注册的项修改器</summary>
        public static IEnumerable<IDecalEntryModifier> EntryModifiers => _entryModifiers ??= CreateInstances<IDecalEntryModifier>();

        /// <summary>
        /// Internal discovery logic using Unity TypeCache. 
        /// Automatically instantiates any non-abstract implementation of the target interface.
        /// </summary>
        private static List<T> CreateInstances<T>() where T : class
        {
            var types = TypeCache.GetTypesDerivedFrom<T>();
            var instances = new List<T>();

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                try
                {
                    if (Activator.CreateInstance(type) is T instance) instances.Add(instance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Decal Extensions] Discovery failed for {type.Name}: {e.Message}");
                }
            }
            return instances;
        }
    }
}
