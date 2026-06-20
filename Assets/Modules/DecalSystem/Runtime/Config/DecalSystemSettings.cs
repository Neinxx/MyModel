using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 全局设置：存储 Decal System 的配置信息，避免路径硬编码
    /// </summary>
    [CreateAssetMenu(fileName = "DecalSystemSettings", menuName = "Decal System/Settings")]
    public class DecalSystemSettings : ScriptableObject
    {
        [Header("Diagnostics")]
        [Tooltip("Enable detailed Decal System logs in the editor and player.")]
        public bool verboseLogging = false;

        [Header("Shader Generation")]
        [Tooltip("生成的 HLSL 文件的相对路径 (从 Assets 开始)")]
        public string generatedHLSLPath = "Assets/Modules/DecalSystem/Shaders/DecalDataMini.generated.hlsl";

        [Header("Editor Styles")]
        [Tooltip("USS 样式表名称 (不需要路径)")]
        public string editorStyleSheetName = "DecalSystemEditor";

        private static DecalSystemSettings _instance;
        private const string ResourcesAssetName = "DecalSystemSettings";

        public static DecalSystemSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<DecalSystemSettings>(ResourcesAssetName);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<DecalSystemSettings>();
                        _instance.hideFlags = HideFlags.DontSave;
                    }
                }

                return _instance;
            }
        }

        public static void SetInstance(DecalSystemSettings settings)
        {
            _instance = settings;
        }
    }
}
