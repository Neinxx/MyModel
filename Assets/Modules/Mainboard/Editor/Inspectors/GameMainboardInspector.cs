using Mainboard.Runtime;
using System.Collections.Generic;
using System.Linq;
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
        private VisualElement _profileModulesRoot;
        private SerializedProperty _profileProperty;

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
            _profileModulesRoot = _root.Q<VisualElement>("uve_ProfileModules");
            _profileProperty = serializedObject.FindProperty("profile");
            BuildProfileModulesView();
            
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
                        UpdateProfileModulesViewIfNeeded();

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

        private Object _lastProfileObject;

        private void UpdateProfileModulesViewIfNeeded()
        {
            if (_profileProperty == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            Object currentProfile = _profileProperty.objectReferenceValue;
            if (currentProfile == _lastProfileObject)
                return;

            BuildProfileModulesView();
        }

        private void BuildProfileModulesView()
        {
            if (_profileModulesRoot == null || _profileProperty == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            _profileModulesRoot.Clear();

            var profile = _profileProperty.objectReferenceValue as MainboardProfile;
            _lastProfileObject = profile;

            if (profile == null)
            {
                var empty = new HelpBox("Assign a MainboardProfile to inspect boot modules.", HelpBoxMessageType.Info);
                _profileModulesRoot.Add(empty);
                return;
            }

            SerializedObject profileSO = new SerializedObject(profile);
            SerializedProperty autoBoot = profileSO.FindProperty("autoBoot");
            SerializedProperty installers = profileSO.FindProperty("installers");

            var summary = new VisualElement();
            summary.AddToClassList("mainboard-profile-summary");
            summary.Add(new Label(profile.name) { name = "ProfileName" });
            summary.Q<Label>("ProfileName").AddToClassList("mainboard-profile-summary-title");

            int installerCount = installers != null && installers.isArray ? installers.arraySize : 0;
            var subtitle = new Label($"Auto Boot: {(autoBoot != null && autoBoot.boolValue ? "Enabled" : "Disabled")}  |  Modules: {installerCount}");
            subtitle.AddToClassList("mainboard-profile-summary-subtitle");
            summary.Add(subtitle);
            _profileModulesRoot.Add(summary);

            if (autoBoot != null)
            {
                var autoBootField = new PropertyField(autoBoot, "Auto Boot");
                autoBootField.Bind(profileSO);
                _profileModulesRoot.Add(autoBootField);
            }

            if (installers == null || !installers.isArray || installers.arraySize == 0)
            {
                _profileModulesRoot.Add(new HelpBox("Profile has no modules.", HelpBoxMessageType.Warning));
                return;
            }

            foreach (MainboardInstaller installer in EnumerateInstallers(installers))
            {
                _profileModulesRoot.Add(CreateInstallerCard(installer));
            }
        }

        private static IEnumerable<MainboardInstaller> EnumerateInstallers(SerializedProperty installers)
        {
            var list = new List<MainboardInstaller>();
            for (int i = 0; i < installers.arraySize; i++)
            {
                var installer = installers.GetArrayElementAtIndex(i).objectReferenceValue as MainboardInstaller;
                if (installer != null)
                    list.Add(installer);
            }

            return list.OrderBy(installer => installer.Priority).ThenBy(installer => installer.name);
        }

        private static VisualElement CreateInstallerCard(MainboardInstaller installer)
        {
            SerializedObject installerSO = new SerializedObject(installer);
            string title = $"{installer.Priority:000}  {installer.DisplayName}";
            var foldout = new Foldout { text = title, value = false };
            foldout.AddToClassList("mainboard-module-foldout");

            var header = new VisualElement();
            header.AddToClassList("mainboard-module-header-row");

            var meta = new Label($"{installer.GetType().Name}  |  {AssetDatabase.GetAssetPath(installer)}");
            meta.AddToClassList("mainboard-module-meta");
            header.Add(meta);
            foldout.Add(header);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("mainboard-module-button-row");

            var pingButton = new Button(() => EditorGUIUtility.PingObject(installer)) { text = "Ping" };
            pingButton.AddToClassList("mainboard-module-mini-button");
            buttonRow.Add(pingButton);

            var selectButton = new Button(() => Selection.activeObject = installer) { text = "Select" };
            selectButton.AddToClassList("mainboard-module-mini-button");
            buttonRow.Add(selectButton);
            foldout.Add(buttonRow);

            SerializedProperty iterator = installerSO.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                    continue;

                var propertyCopy = iterator.Copy();
                var field = new PropertyField(propertyCopy);
                field.Bind(installerSO);
                foldout.Add(field);
            }

            return foldout;
        }
    }
}
