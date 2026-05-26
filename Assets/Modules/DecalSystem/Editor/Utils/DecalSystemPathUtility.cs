using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini.Editor
{
    /// <summary>
    /// 工业级路径工具：动态定位贴花系统资源路径。
    /// 即使移动整个 DecalSystem 文件夹，UI 依然能正确加载样式。
    /// </summary>
    public static class DecalSystemPathUtility
    {
        private static string _rootPath;

        public static string GetRootPath()
        {
            if (!string.IsNullOrEmpty(_rootPath))
                return _rootPath;

            var guids = AssetDatabase.FindAssets("DecalSystemPathUtility t:Script");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                // Flexible backtracking: find the 'Editor' folder's parent
                int editorIndex = path.IndexOf("/Editor/");
                if (editorIndex != -1)
                {
                    _rootPath = path.Substring(0, editorIndex);
                    return _rootPath;
                }
            }
            return "Assets/Modules/DecalSystem"; // Updated fallback
        }

        public static StyleSheet LoadUSS(string name)
        {
            string path = $"{GetRootPath()}/Editor/Skin/{name}.uss";
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (style == null)
            {
                // 尝试备用路径
                path = $"{GetRootPath()}/Editor/Styles/{name}.uss";
                style = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            return style;
        }


        public static VisualTreeAsset LoadUXML(string name)
        {
            string path = $"{GetRootPath()}/Editor/Layouts/{name}.uxml";
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (uxml == null)
            {
                // 工业级容错：搜索全局
                var guids = AssetDatabase.FindAssets($"{name} t:VisualTreeAsset");
                if (guids.Length > 0)
                    uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[0])
                    );
            }
            return uxml;
        }

        public static Texture2D LoadIcon(string name)
        {
            string path = $"{GetRootPath()}/Editor/Icons/{name}.png";
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
