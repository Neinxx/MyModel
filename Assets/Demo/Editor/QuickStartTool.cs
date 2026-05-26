using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ModularDemo.Editor
{
    /// <summary>
    /// 快速启动模块 (Quick Start Tool)
    /// 强制 Unity 的 Play 按钮始终从 Launch 场景启动，方便在任何场景直接测试完整流程。
    /// </summary>
    [InitializeOnLoad]
    public static class QuickStartTool
    {
        private const string PREF_KEY = "QuickStart_Enabled";
        private const string LAUNCH_SCENE_PATH = "Assets/Demo/Art/Scenes/Launch.unity"; // 请确保路径匹配您的 Launch 场景
        private const string MENU_NAME = "Tools/🚀 Enable Quick Start (Play from Launch Scene)";

        static QuickStartTool()
        {
            // 延迟一帧应用状态，防止编辑器还没初始化完
            EditorApplication.delayCall += ApplyState;
        }

        [MenuItem(MENU_NAME)]
        public static void ToggleQuickStart()
        {
            bool isEnabled = EditorPrefs.GetBool(PREF_KEY, false);
            EditorPrefs.SetBool(PREF_KEY, !isEnabled);
            ApplyState();
        }

        [MenuItem(MENU_NAME, true)]
        public static bool ToggleQuickStartValidate()
        {
            bool isEnabled = EditorPrefs.GetBool(PREF_KEY, false);
            Menu.SetChecked(MENU_NAME, isEnabled);
            return true;
        }

        private static void ApplyState()
        {
            bool isEnabled = EditorPrefs.GetBool(PREF_KEY, false);

            if (isEnabled)
            {
                SceneAsset launchScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    LAUNCH_SCENE_PATH
                );
                if (launchScene != null)
                {
                    EditorSceneManager.playModeStartScene = launchScene;
                    Debug.Log(
                        $"<color=#3FB950><b>[QuickStart]</b></color> 已启用！点击 Play 将强制从 {launchScene.name} 启动。"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        $"[QuickStart] 找不到 Launch 场景，请检查路径: {LAUNCH_SCENE_PATH}"
                    );
                    EditorSceneManager.playModeStartScene = null;
                }
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
                Debug.Log(
                    $"<color=#aaaaaa><b>[QuickStart]</b></color> 已禁用，点击 Play 将从当前所在场景启动。"
                );
            }
        }
    }
}
