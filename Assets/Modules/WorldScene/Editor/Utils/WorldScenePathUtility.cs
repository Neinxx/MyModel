using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldSceneModule.Editor
{
    public static class WorldScenePathUtility
    {
        private static string _rootPath;

        public static string GetRootPath()
        {
            if (!string.IsNullOrEmpty(_rootPath)) return _rootPath;

            var guids = AssetDatabase.FindAssets("WorldScenePathUtility t:Script");
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
            return "Assets/Modules/WorldScene";
        }

        public static StyleSheet LoadUSS(string name) => AssetDatabase.LoadAssetAtPath<StyleSheet>($"{GetRootPath()}/Editor/Styles/{name}.uss");
        public static VisualTreeAsset LoadUXML(string name) => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{GetRootPath()}/Editor/Styles/{name}.uxml");
    }
}
