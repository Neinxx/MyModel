using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PortalSystem.Editor;
using PortalSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PortalSystem.Editor.Inspectors
{
    [CustomEditor(typeof(PortalHub))]
    [CanEditMultipleObjects]
    public class PortalInspector : UnityEditor.Editor
    {
        private VisualElement _root;
        private Label _stateLabel;
        private DropdownField _levelDropdown;
        private DropdownField _spawnDropdown;

        public override VisualElement CreateInspectorGUI()
        {
            // 1. Load Assets
            var uxml = PortalPathUtility.LoadUXML("PortalHub");
            if (uxml == null)
                return new Label("UXML Missing: PortalHub.uxml");

            _root = uxml.Instantiate();

            var uss = PortalPathUtility.LoadUSS("PortalEditor");
            if (uss != null)
                _root.styleSheets.Add(uss);

            // 2. Setup Core Elements
            _stateLabel = _root.Q<Label>("uve_RuntimeState");
            var testBtn = _root.Q<Button>("uve_TestButton");
            var playInfo = _root.Q<Label>("uve_PlayModeInfo");

            // 3. Setup Dropdowns
            SetupDropdowns();

            // 4. Runtime Polling
            if (testBtn != null)
            {
                testBtn.clicked += () =>
                {
                    var hub = target as PortalHub;
                    if (hub != null)
                        hub.TriggerTeleport();
                };

                _root
                    .schedule.Execute(() =>
                    {
                        bool isPlaying = Application.isPlaying;
                        testBtn.SetEnabled(isPlaying);
                        if (playInfo != null)
                            playInfo.style.display = isPlaying
                                ? DisplayStyle.None
                                : DisplayStyle.Flex;

                        if (_stateLabel != null)
                        {
                            _stateLabel.text = isPlaying ? "ONLINE" : "READY";
                            _stateLabel.style.color = isPlaying
                                ? new Color(0.25f, 0.73f, 0.31f)
                                : new Color(0.48f, 0.7f, 0.97f);
                        }
                    })
                    .Every(500);
            }

            return _root;
        }

        private void SetupDropdowns()
        {
            var levelContainer = _root.Q<VisualElement>("uve_LevelDropdownContainer");
            var spawnContainer = _root.Q<VisualElement>("uve_SpawnDropdownContainer");

            var levelProp = serializedObject.FindProperty("targetLevelName");
            var spawnProp = serializedObject.FindProperty("targetSpawnPointID");

            // --- Level Dropdown ---
            var levelNames = GetLevelNamesFromRegistry();
            if (
                !string.IsNullOrEmpty(levelProp.stringValue)
                && !levelNames.Contains(levelProp.stringValue)
            )
            {
                levelNames.Add(levelProp.stringValue);
            }
            _levelDropdown = new DropdownField("Target Scene", levelNames, levelProp.stringValue);
            _levelDropdown.tooltip = "Select the target level from the LevelRegistry.";
            _levelDropdown.RegisterValueChangedCallback(evt =>
            {
                levelProp.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                RefreshSpawnDropdown(evt.newValue);
            });
            levelContainer.Add(_levelDropdown);

            // --- Spawn Dropdown ---
            var spawnChoices = new List<string> { "Start" };
            if (
                !string.IsNullOrEmpty(spawnProp.stringValue)
                && !spawnChoices.Contains(spawnProp.stringValue)
            )
            {
                spawnChoices.Add(spawnProp.stringValue);
            }
            _spawnDropdown = new DropdownField(
                "Target Spawn ID",
                spawnChoices,
                spawnProp.stringValue
            );
            _spawnDropdown.tooltip = "Select a SpawnPointID discovered in the target scene.";
            _spawnDropdown.RegisterValueChangedCallback(evt =>
            {
                spawnProp.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            spawnContainer.Add(_spawnDropdown);

            // Initial refresh
            RefreshSpawnDropdown(levelProp.stringValue);
        }

        private List<string> GetLevelNamesFromRegistry()
        {
            var names = new List<string> { "" };
            // Find LevelRegistry by type name (to avoid hard dependency)
            var guids = AssetDatabase.FindAssets("t:LevelRegistry");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (registry != null)
                {
                    var so = new SerializedObject(registry);
                    var levelsProp = so.FindProperty("levels");
                    if (levelsProp != null && levelsProp.isArray)
                    {
                        for (int i = 0; i < levelsProp.arraySize; i++)
                        {
                            var element = levelsProp.GetArrayElementAtIndex(i);
                            var nameProp = element.FindPropertyRelative("levelName");
                            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                                names.Add(nameProp.stringValue);
                        }
                    }
                }
            }
            return names.Distinct().ToList();
        }

        private void RefreshSpawnDropdown(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
            {
                _spawnDropdown.choices = new List<string> { "Start" };
                return;
            }

            var spawnIDs = new List<string> { "Start" };

            // Try to find the scene path from the registry
            string scenePath = "";
            var guids = AssetDatabase.FindAssets("t:LevelRegistry");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (registry != null)
                {
                    var so = new SerializedObject(registry);
                    var levelsProp = so.FindProperty("levels");
                    for (int i = 0; i < levelsProp.arraySize; i++)
                    {
                        var element = levelsProp.GetArrayElementAtIndex(i);
                        if (element.FindPropertyRelative("levelName").stringValue == levelName)
                        {
                            var assetProp = element.FindPropertyRelative("sceneAsset");
                            if (assetProp != null && assetProp.objectReferenceValue != null)
                                scenePath = AssetDatabase.GetAssetPath(
                                    assetProp.objectReferenceValue
                                );
                            break;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(scenePath))
                    break;
            }

            // If found, scan the .unity file as text for SpawnPointHub's spawnPointID
            if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
            {
                try
                {
                    string content = File.ReadAllText(scenePath);
                    // Match pattern for _hubID in YAML: "_hubID: Value"
                    var matches = Regex.Matches(content, @"_hubID:\s*([^\s\r\n]+)");
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string id = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(id) && !spawnIDs.Contains(id))
                                spawnIDs.Add(id);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PortalInspector] Failed to scan scene file: {e.Message}");
                }
            }

            if (
                !string.IsNullOrEmpty(_spawnDropdown.value)
                && !spawnIDs.Contains(_spawnDropdown.value)
            )
            {
                spawnIDs.Add(_spawnDropdown.value);
            }
            _spawnDropdown.choices = spawnIDs;
        }
    }
}
