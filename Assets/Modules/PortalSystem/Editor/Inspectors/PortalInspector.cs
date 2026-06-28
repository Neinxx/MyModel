using System.Collections.Generic;
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
        private Label _destinationStatusLabel;
        private DropdownField _levelDropdown;
        private DropdownField _spawnDropdown;
        private List<string> _discoveredLevelNames = new();

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
            _destinationStatusLabel = _root.Q<Label>("uve_DestinationStatus");
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

            var levelProp = serializedObject.FindProperty("_targetLevelName");
            var spawnProp = serializedObject.FindProperty("_targetSpawnPointId");

            var levelNames = PortalDestinationDiscovery.GetLevelNames();
            _discoveredLevelNames = new List<string>(levelNames);
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
                RefreshDestinationStatus(evt.newValue);
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

            RefreshSpawnDropdown(levelProp.stringValue);
            RefreshDestinationStatus(levelProp.stringValue);
        }

        private void RefreshSpawnDropdown(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
            {
                _spawnDropdown.choices = new List<string> { "Start" };
                return;
            }

            var spawnIDs = PortalDestinationDiscovery.GetSpawnIds(levelName);

            if (
                !string.IsNullOrEmpty(_spawnDropdown.value)
                && !spawnIDs.Contains(_spawnDropdown.value)
            )
            {
                spawnIDs.Add(_spawnDropdown.value);
            }
            _spawnDropdown.choices = spawnIDs;
        }

        private void RefreshDestinationStatus(string levelName)
        {
            if (_destinationStatusLabel == null)
            {
                return;
            }

            _destinationStatusLabel.RemoveFromClassList("portal-destination-status-ok");
            _destinationStatusLabel.RemoveFromClassList("portal-destination-status-warning");
            _destinationStatusLabel.RemoveFromClassList("portal-destination-status-error");

            if (string.IsNullOrEmpty(levelName))
            {
                _destinationStatusLabel.text = "Select a target scene";
                _destinationStatusLabel.AddToClassList("portal-destination-status-warning");
                return;
            }

            if (!_discoveredLevelNames.Contains(levelName))
            {
                _destinationStatusLabel.text = "Target not found in registry";
                _destinationStatusLabel.AddToClassList("portal-destination-status-error");
                return;
            }

            _destinationStatusLabel.text = "Destination ready";
            _destinationStatusLabel.AddToClassList("portal-destination-status-ok");
        }
    }
}
