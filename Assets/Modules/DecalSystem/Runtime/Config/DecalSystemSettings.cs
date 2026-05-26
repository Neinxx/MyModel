using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 全局设置：存储 Decal System 的配置信息，避免路径硬编码
    /// </summary>
    [CreateAssetMenu(fileName = "DecalSystemSettings", menuName = "Decal System/Settings")]
    public class DecalSystemSettings : ScriptableObject
    {
        [Header("Shader Generation")]
        [Tooltip("生成的 HLSL 文件的相对路径 (从 Assets 开始)")]
        public string generatedHLSLPath = "Assets/Modules/DecalSystem/Shaders/DecalDataMini.generated.hlsl";

        [Header("Editor Styles")]
        [Tooltip("USS 样式表名称 (不需要路径)")]
        public string editorStyleSheetName = "DecalSystemEditor";

        private static DecalSystemSettings _instance;

        public static DecalSystemSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 在项目中寻找设置文件
#if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DecalSystemSettings");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<DecalSystemSettings>(path);
                    }
#endif
                }
                return _instance;
            }
        }
    }
}
