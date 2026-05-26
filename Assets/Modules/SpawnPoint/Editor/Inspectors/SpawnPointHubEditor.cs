using System.Linq;
using SpawnPoint.Editor;
using SpawnPoint.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpawnPoint.Editor.Inspectors
{
    [CustomEditor(typeof(SpawnPointHub))]
    [CanEditMultipleObjects]
    public class SpawnPointHubEditor : UnityEditor.Editor
    {
        private VisualElement _root;
        private Label _hubCountLabel;
        private Label _stateLabel;

        public override VisualElement CreateInspectorGUI()
        {
            // 1. Load UXML
            var uxml = SpawnPointPathUtility.LoadUXML("SpawnPointHub");
            if (uxml == null)
                return new Label("UXML Missing: SpawnPointHub.uxml");

            _root = uxml.Instantiate();

            // 2. Load and Apply USS
            var uss = SpawnPointPathUtility.LoadUSS("SpawnPointEditor");
            if (uss != null)
                _root.styleSheets.Add(uss);

            // 3. Setup Analytics
            _hubCountLabel = _root.Q<Label>("uve_HubCount");
            _stateLabel = _root.Q<Label>("uve_RuntimeState");

            // 4. Track status change for visual feedback
            var blockedField = _root.Q<PropertyField>("uve_BlockedField");
            if (blockedField != null)
            {
                _root.TrackSerializedObjectValue(serializedObject, _ => UpdateVisuals());
            }

            // 5. Runtime Polling for Analytics
            _root.schedule.Execute(UpdateAnalytics).Every(500);

            UpdateVisuals();
            return _root;
        }

        private void UpdateVisuals()
        {
            var hub = target as SpawnPointHub;
            if (hub == null || _stateLabel == null)
                return;

            if (hub.IsBlocked)
            {
                _stateLabel.text = "BLOCKED";
                _stateLabel.style.color = new Color(0.97f, 0.32f, 0.29f); // #F85149
            }
            else
            {
                _stateLabel.text = Application.isPlaying ? "ONLINE" : "READY";
                _stateLabel.style.color = new Color(0.25f, 0.73f, 0.31f); // #3FB950
            }
        }

        private void UpdateAnalytics()
        {
            if (_hubCountLabel == null)
                return;

            // Accessing the static registry for total count
            int count = SpawnPointHub.AllHubs.Count();
            _hubCountLabel.text = count.ToString();
        }
    }
}
