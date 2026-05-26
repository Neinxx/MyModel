using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpawnPoint.Editor
{
    /// <summary>
    /// 路径工具：动态定位生成点系统资源路径。
    /// </summary>
    public static class SpawnPointPathUtility
    {
        private static string _rootPath;

        public static string GetRootPath()
        {
            if (!string.IsNullOrEmpty(_rootPath))
                return _rootPath;

            var guids = AssetDatabase.FindAssets("SpawnPointPathUtility t:Script");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                int editorIndex = path.IndexOf("/Editor/");
                if (editorIndex != -1)
                {
                    _rootPath = path.Substring(0, editorIndex);
                    return _rootPath;
                }
            }
            return "Assets/Modules/SpawnPoint";
        }

        public static StyleSheet LoadUSS(string name)
        {
            string path = $"{GetRootPath()}/Editor/Styles/{name}.uss";
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
        }

        public static VisualTreeAsset LoadUXML(string name)
        {
            string path = $"{GetRootPath()}/Editor/Styles/{name}.uxml";
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        }
    }
}
