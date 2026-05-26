using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WorldSceneModule.Runtime;

namespace WorldSceneModule.Editor
{
    [CustomEditor(typeof(WorldSceneDriver), true)]
    public class WorldSceneDriverEditor : UnityEditor.Editor
    {
        private VisualElement _root;

        public override VisualElement CreateInspectorGUI()
        {
            // 1. 加载风格统一的 UXML 和 USS
            var uxml = WorldScenePathUtility.LoadUXML("WorldSceneDriverEditor");
            if (uxml == null)
                return new Label("UXML  Missing");

            _root = uxml.Instantiate();

            // 载入关卡编辑器的通用样式表
            var registryUss = WorldScenePathUtility.LoadUSS("RegistryEditor");
            if (registryUss != null)
                _root.styleSheets.Add(registryUss);

            // 载入当前调度员的专属动态过渡样式表
            var worldSceneUss = WorldScenePathUtility.LoadUSS("WorldSceneDriverEditor");
            if (worldSceneUss != null)
                _root.styleSheets.Add(worldSceneUss);

            // 2. 获取核心节点
            var monitorContainer = _root.Q<VisualElement>(className: "monitor-container");
            var loadingBar = _root.Q<ProgressBar>("loading-bar");
            var loadingField = _root.Q<PropertyField>("loading-field");

            if (loadingBar != null)
            {
                loadingBar.lowValue = 0f;
                loadingBar.highValue = 1f;
            }

            // 4. 反应式监控运行时状态 (零 GC, 纯事件驱动)
            var stateProp = serializedObject.FindProperty("state");
            var progressProp = serializedObject.FindProperty("loadingProgress");

            // 禁用状态监视字段，使其仅用于只读展示
            var stateField = _root.Q<PropertyField>("state-field");
            var stepField = _root.Q<PropertyField>("step-field");
            if (stateField != null)
                stateField.SetEnabled(false);
            if (stepField != null)
                stepField.SetEnabled(false);
            if (loadingField != null)
                loadingField.SetEnabled(false);

            _root.TrackPropertyValue(
                stateProp,
                (prop) =>
                {
                    if (monitorContainer == null)
                        return;

                    WorldSceneState currentState = (WorldSceneState)prop.enumValueIndex;
                    if (currentState == WorldSceneState.Loading)
                    {
                        monitorContainer.RemoveFromClassList("active");
                        if (loadingBar != null)
                            loadingBar.style.display = DisplayStyle.Flex;
                        if (loadingField != null)
                            loadingField.style.display = DisplayStyle.None;
                    }
                    else if (currentState == WorldSceneState.Ready)
                    {
                        monitorContainer.AddToClassList("active");
                        if (loadingBar != null)
                            loadingBar.style.display = DisplayStyle.None;
                        if (loadingField != null)
                            loadingField.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        monitorContainer.RemoveFromClassList("active");
                        if (loadingBar != null)
                            loadingBar.style.display = DisplayStyle.None;
                        if (loadingField != null)
                            loadingField.style.display = DisplayStyle.Flex;
                    }
                }
            );

            _root.TrackPropertyValue(
                progressProp,
                (prop) =>
                {
                    if (loadingBar != null)
                    {
                        loadingBar.value = prop.floatValue;
                    }
                }
            );

            // 初始化一次状态转换
            if (stateProp != null)
            {
                WorldSceneState initialVal = (WorldSceneState)stateProp.enumValueIndex;
                if (initialVal == WorldSceneState.Ready && monitorContainer != null)
                {
                    monitorContainer.AddToClassList("active");
                }
            }

            return _root;
        }
    }
}
