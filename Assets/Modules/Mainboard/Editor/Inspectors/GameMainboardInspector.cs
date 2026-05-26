using Mainboard.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mainboard.Editor.Inspectors
{
    [CustomEditor(typeof(GameMainboard))]
    [CanEditMultipleObjects]
    public class GameMainboardInspector : UnityEditor.Editor
    {
        private VisualElement _root;
        private Label _stateLabel;
        private Label _phaseLabel;
        private Label _stepLabel;
        private ProgressBar _progressBar;

        public override VisualElement CreateInspectorGUI()
        {
            // 1. Load Assets
            // Load UXML dynamically instead of using PortalPathUtility to avoid cross-module dependency
            string basePath = "Assets/Modules/Mainboard/Editor/Styles/";
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(basePath + "GameMainboard.uxml");
            
            if (uxml == null)
                return new Label("UXML Missing: " + basePath + "GameMainboard.uxml");

            _root = uxml.Instantiate();

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(basePath + "GameMainboard.uss");
            if (uss != null)
                _root.styleSheets.Add(uss);

            // 2. Setup Core Elements
            _stateLabel = _root.Q<Label>("uve_RuntimeState");
            _phaseLabel = _root.Q<Label>("uve_Phase");
            _stepLabel = _root.Q<Label>("uve_Step");
            _progressBar = _root.Q<ProgressBar>("uve_ProgressBar");
            
            var bootBtn = _root.Q<Button>("uve_BootButton");
            var shutdownBtn = _root.Q<Button>("uve_ShutdownButton");
            var playInfo = _root.Q<Label>("uve_PlayModeInfo");

            // 3. Runtime Polling & Actions
            if (bootBtn != null && shutdownBtn != null)
            {
                bootBtn.clicked += () =>
                {
                    var board = target as GameMainboard;
                    if (board != null && Application.isPlaying)
                        _ = board.BootAsync();
                };

                shutdownBtn.clicked += () =>
                {
                    var board = target as GameMainboard;
                    if (board != null && Application.isPlaying)
                        _ = board.ShutdownAsync();
                };

                _root.schedule.Execute(() =>
                {
                    bool isPlaying = Application.isPlaying;
                    
                    var board = target as GameMainboard;
                    bool isBooted = board != null && board.IsBooted;
                    bool isBooting = board != null && board.IsBooting;

                    // Button states
                    bootBtn.SetEnabled(isPlaying && !isBooted && !isBooting);
                    shutdownBtn.SetEnabled(isPlaying && (isBooted || isBooting));
                    
                    if (playInfo != null)
                        playInfo.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;

                    // UI Updates
                    if (board != null)
                    {
                        if (_phaseLabel != null) _phaseLabel.text = board.CurrentPhase.ToString();
                        if (_stepLabel != null) _stepLabel.text = board.CurrentStep;
                        if (_progressBar != null) _progressBar.value = board.CurrentProgress;

                        if (_stateLabel != null)
                        {
                            if (isBooted)
                            {
                                _stateLabel.text = "ONLINE";
                                _stateLabel.style.color = new Color(0.25f, 0.73f, 0.31f); // Green
                            }
                            else if (isBooting)
                            {
                                _stateLabel.text = "BOOTING...";
                                _stateLabel.style.color = new Color(0.9f, 0.7f, 0.2f); // Yellow
                            }
                            else if (board.CurrentPhase == MainboardPhase.Faulted)
                            {
                                _stateLabel.text = "FAULTED";
                                _stateLabel.style.color = new Color(0.9f, 0.3f, 0.3f); // Red
                            }
                            else
                            {
                                _stateLabel.text = isPlaying ? "STANDBY" : "OFFLINE";
                                _stateLabel.style.color = isPlaying ? new Color(0.4f, 0.8f, 0.9f) : new Color(0.5f, 0.5f, 0.5f);
                            }
                        }
                    }
                }).Every(100); // 100ms polling for smooth progress bar
            }

            return _root;
        }
    }
}
