using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PortalSystem.Editor
{
    public static class PortalPathUtility
    {
        private static string _rootPath;

        public static string GetRootPath()
        {
            if (!string.IsNullOrEmpty(_rootPath))
                return _rootPath;

            var guids = AssetDatabase.FindAssets("PortalPathUtility t:Script");
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
            return "Assets/Modules/PortalSystem";
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
