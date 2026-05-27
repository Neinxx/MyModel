using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace Auto1KeyBuildModule.Editor
{
    [System.Serializable]
    public class Auto1KeyBuildProfile
    {
        public string profileName = "Windows Release";
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
        public string outputDirectory = "Builds";
        public bool developmentBuild = false;
        public bool cleanOutputBeforeBuild = false;
        public bool runPreflightBeforeBuild = true;
        public bool writeBuildReport = true;
    }

    /// <summary>
    /// 工业级打包模块配置资产类 (Auto1KeyBuild Settings)
    /// 实现规则解耦，使 Shader 扫描路径、组名称等关键打包参数支持可视化配置
    /// </summary>
    public class Auto1KeyBuildSettings : ScriptableObject
    {
        [Header("Build Profiles")]
        public int activeProfileIndex = 0;
        public List<Auto1KeyBuildProfile> profiles = new List<Auto1KeyBuildProfile>()
        {
            new Auto1KeyBuildProfile()
        };

        [Header("Scene Build Configuration")]
        [Tooltip("The folders to scan for Unity scenes when building the player.")]
        public List<string> sceneFolderPaths = new List<string>()
        {
            "Assets/Demo/Art/Scenes"
        };

        [Header("Shader Scan Configuration")]
        [Tooltip("The folders to scan for shaders to package in the Decal Group.")]
        public List<string> shaderFolderPaths = new List<string>()
        {
            "Assets/Modules/DecalSystem/Shaders"
        };

        [Header("Decal Group Configurations")]
        [Tooltip("Name of the addressable group for decal assets.")]
        public string decalGroupName = "Decal_Group";

        [Tooltip("Search query to locate decal configuration assets.")]
        public string decalConfigSearchQuery = "t:DecalAtlasConfigMini";

        [Header("Dependency Protection Options")]
        [Tooltip("Automatically promote duplicate implicit dependencies to a shared group.")]
        public bool autoPromoteRedundancies = true;

        [Tooltip("The group name to place promoted shared dependencies.")]
        public string sharedGroupName = "Shared_AutoGroup";

        [Header("Content Update Pipeline")]
        [Tooltip("Enable Addressables settings required for remote catalog content updates.")]
        public bool enableContentUpdateSupport = true;

        [Tooltip("Use hashed bundle names for content update safety and CDN cache correctness.")]
        public bool useHashBundleNamesForContentUpdates = true;

        [Tooltip("Automatically move changed assets from static shipped groups into a remote content update group.")]
        public bool autoMoveChangedStaticContent = true;

        [Tooltip("Name prefix for generated content update groups.")]
        public string contentUpdateGroupName = "Content_Update";

        [Tooltip("Use a local file:// Remote.LoadPath for content update testing on this workstation.")]
        public bool localContentUpdateTestMode = true;

        [Tooltip("Local folder used as the simulated remote content root for content update tests.")]
        public string localContentUpdateServerDataPath = "ServerData/[BuildTarget]";

        [Tooltip("Addressable keys that must stay in the local shipped catalog/group. These are boot-critical and must not be auto-moved into content update groups.")]
        public List<string> protectedLocalAddressableKeys = new List<string>()
        {
            "Assets/Demo/Art/Prefabs/Anbu_art.prefab",
            "Assets/Demo/Data/WorldSceneDriver.prefab",
            "Assets/Demo/Shader/NewShaderVariants.shadervariants",
            "Assets/Demo/Art/Prefabs/Character_logic.prefab",
            "Assets/Demo/Art/Prefabs/CameraView.prefab"
        };

        [Tooltip("Optional path to the previous addressables_content_state.bin. Leave empty to auto-detect from AddressableAssetsData.")]
        public string previousContentStatePath = "";

        [Tooltip("How Addressables should handle modified entries in static post-release groups.")]
        public CheckForContentUpdateRestrictionsOptions contentUpdateRestrictionMode = CheckForContentUpdateRestrictionsOptions.Disabled;

        public Auto1KeyBuildProfile ActiveProfile
        {
            get
            {
                EnsureProfiles();
                activeProfileIndex = Mathf.Clamp(activeProfileIndex, 0, profiles.Count - 1);
                return profiles[activeProfileIndex];
            }
        }

        public void EnsureProfiles()
        {
            if (profiles == null)
            {
                profiles = new List<Auto1KeyBuildProfile>();
            }

            if (profiles.Count == 0)
            {
                profiles.Add(new Auto1KeyBuildProfile());
            }
        }

        /// <summary>
        /// 获取或创建全局唯一的打包配置资产，持久化存储于本地模块中
        /// </summary>
        public static Auto1KeyBuildSettings GetOrCreateSettings()
        {
            const string settingsDir = "Assets/Modules/Auto1KeyBuild/Editor";
            const string settingsPath = settingsDir + "/Auto1KeyBuildSettings.asset";

            var settings = AssetDatabase.LoadAssetAtPath<Auto1KeyBuildSettings>(settingsPath);
            if (settings == null)
            {
                settings = CreateInstance<Auto1KeyBuildSettings>();
                
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }

                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#3DB8FF><b>[Auto1KeyBuild]</b></color> Config asset initialized at {settingsPath}");
            }

            settings.EnsureProfiles();
            return settings;
        }
    }
}
