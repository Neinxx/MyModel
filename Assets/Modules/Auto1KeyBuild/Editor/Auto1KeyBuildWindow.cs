using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace Auto1KeyBuildModule.Editor
{
    /// <summary>
    /// 工业级一键打包主控中心 — UI Toolkit 重构版 (Auto1KeyBuild Control Center)
    /// 使用 USS + VisualElement 实现 style.md 高对比度深色工业美学
    /// </summary>
    public class Auto1KeyBuildWindow : EditorWindow
    {
        // ── Settings Binding ──
        private Auto1KeyBuildSettings _settings;
        // ── Runtime State ──
        private string _buildLogs = "Ready for execution.";
        private bool? _lastBuildSucceeded = null;
        private string _auditReportText = "Click 'Audit Dependencies' to run analysis.";
        private int _redundantCount = 0;

        // ── Dynamic UI References ──
        private VisualElement _scenePathsContainer;
        private VisualElement _shaderPathsContainer;
        private VisualElement _buildResultContainer;
        private VisualElement _auditBannerContainer;
        private Label _logBodyLabel;
        private Label _dashboardLabel;
        private Label _preflightSummaryLabel;
        private VisualElement _preflightListContainer;
        private Toggle _autoPromoteToggle;

        [MenuItem("Tools/Auto1KeyBuild Hub", false, 1)]
        public static void OpenWindow()
        {
            var window = GetWindow<Auto1KeyBuildWindow>("Auto1KeyBuild Hub");
            window.minSize = new Vector2(620, 680);
            window.Show();
        }

        private void CreateGUI()
        {
            // ── 加载 USS ──
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Modules/Auto1KeyBuild/Editor/Auto1KeyBuildWindow.uss");

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("root");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            // ── 加载配置 ──
            _settings = Auto1KeyBuildSettings.GetOrCreateSettings();

            // ═══ Header ═══
            BuildHeader(root);

            // ═══ Scrollable Main Content ═══
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll-container");
            root.Add(scroll);

            BuildDashboardSection(scroll.contentContainer);

            // ─── Module Configuration ───
            BuildSettingsSection(scroll.contentContainer); 

            BuildPreflightSection(scroll.contentContainer);

            // ─── Build Pipeline CTA ───
            BuildPipelineSection(scroll.contentContainer);

            // ─── Build Result Banner ───
            _buildResultContainer = new VisualElement();
            scroll.contentContainer.Add(_buildResultContainer);

            // ─── Auxiliary Commands ───
            BuildAuxSection(scroll.contentContainer);

            // ─── Dependency Audit ───
            BuildAuditSection(scroll.contentContainer);
        }

        // ═════════════════════════════════════════════════════════════
        //  Header
        // ═════════════════════════════════════════════════════════════

        private void BuildHeader(VisualElement parent)
        {
            var header = new VisualElement();
            header.AddToClassList("header");

            var accent = new VisualElement();
            accent.AddToClassList("header-accent");
            header.Add(accent);

            var title = new Label("AUTO 1-KEY BUILD HUB");
            title.AddToClassList("title");
            header.Add(title);

            var subtitle = new Label("Build Pipeline v1.0");
            subtitle.AddToClassList("subtitle");
            header.Add(subtitle);

            parent.Add(header);
        }

        private void BuildDashboardSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var header = new Label("BUILD DASHBOARD");
            header.AddToClassList("section-header");
            section.Add(header);

            var box = new VisualElement();
            box.AddToClassList("dashboard-box");

            _dashboardLabel = new Label();
            _dashboardLabel.AddToClassList("dashboard-text");
            box.Add(_dashboardLabel);

            section.Add(box);
            section.Add(CreateDivider());
            parent.Add(section);

            RefreshDashboard();
        }

        // ═════════════════════════════════════════════════════════════
        //  Module Configuration (Settings Foldout)
        // ═════════════════════════════════════════════════════════════

        private void BuildSettingsSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var foldout = new Foldout { text = "MODULE CONFIGURATION", value = true };
            foldout.AddToClassList("settings-foldout");

            var card = new VisualElement();
            card.AddToClassList("card");

            card.Add(CreateGroupTitle("Build Profile"));
            BuildProfileFields(card);
            card.Add(CreateSpacer(12));

            // ── Scene Build Folders ──
            card.Add(CreateGroupTitle("Scene Build Folders"));

            _scenePathsContainer = new VisualElement();
            _scenePathsContainer.AddToClassList("path-list");
            card.Add(_scenePathsContainer);
            RebuildScenePathsList();

            var addSceneBtn = new Button(() => AddScenePath()) { text = "+ ADD SCENE FOLDER" };
            addSceneBtn.AddToClassList("btn-add-path");
            card.Add(addSceneBtn);

            // ── Spacer ──
            card.Add(CreateSpacer(12));

            // ── Shader Scan Folders ──
            card.Add(CreateGroupTitle("Shader Scan Folders"));

            _shaderPathsContainer = new VisualElement();
            _shaderPathsContainer.AddToClassList("path-list");
            card.Add(_shaderPathsContainer);
            RebuildShaderPathsList();

            var addBtn = new Button(() => AddShaderPath()) { text = "+ ADD SHADER FOLDER" };
            addBtn.AddToClassList("btn-add-path");
            card.Add(addBtn);

            // ── Spacer ──
            card.Add(CreateSpacer(12));

            // ── Group Names ──
            card.Add(CreateGroupTitle("Group Names & Queries"));

            card.Add(CreateConfigTextField("Decal Group Name", _settings.decalGroupName, v => {
                _settings.decalGroupName = v;
                SaveSettings();
            }));
            card.Add(CreateConfigTextField("Decal Config Query", _settings.decalConfigSearchQuery, v => {
                _settings.decalConfigSearchQuery = v;
                SaveSettings();
            }));
            card.Add(CreateConfigTextField("Shared Group Name", _settings.sharedGroupName, v => {
                _settings.sharedGroupName = v;
                SaveSettings();
            }));

            // ── Spacer ──
            card.Add(CreateSpacer(12));

            // ── Dependency Protection ──
            card.Add(CreateGroupTitle("Dependency Protection"));

            _autoPromoteToggle = new Toggle("Auto-Promote Redundancies") { value = _settings.autoPromoteRedundancies };
            _autoPromoteToggle.AddToClassList("config-field");
            _autoPromoteToggle.RegisterValueChangedCallback(evt => {
                _settings.autoPromoteRedundancies = evt.newValue;
                SaveSettings();
            });
            card.Add(_autoPromoteToggle);

            card.Add(CreateSpacer(12));
            card.Add(CreateGroupTitle("Content Update"));

            card.Add(CreateConfigToggle("Enable Content Updates", _settings.enableContentUpdateSupport, v => {
                _settings.enableContentUpdateSupport = v;
                SaveSettings();
            }));

            card.Add(CreateConfigToggle("Hashed Bundle Names", _settings.useHashBundleNamesForContentUpdates, v => {
                _settings.useHashBundleNamesForContentUpdates = v;
                SaveSettings();
            }));

            card.Add(CreateConfigToggle("Auto-Move Static Changes", _settings.autoMoveChangedStaticContent, v => {
                _settings.autoMoveChangedStaticContent = v;
                SaveSettings();
            }));

            card.Add(CreateConfigTextField("Content Update Group", _settings.contentUpdateGroupName, v => {
                _settings.contentUpdateGroupName = v;
                SaveSettings();
            }));

            card.Add(CreateConfigToggle("Local Incremental Test", _settings.localContentUpdateTestMode, v => {
                _settings.localContentUpdateTestMode = v;
                SaveSettings();
            }));

            card.Add(CreateConfigTextField("Local Test Remote Root", _settings.localContentUpdateServerDataPath, v => {
                _settings.localContentUpdateServerDataPath = v;
                SaveSettings();
            }));

            card.Add(CreateConfigTextField("Previous State Bin", _settings.previousContentStatePath, v => {
                _settings.previousContentStatePath = v;
                SaveSettings();
            }));

            var restrictionField = new EnumField("Restriction Mode", _settings.contentUpdateRestrictionMode);
            restrictionField.AddToClassList("config-field");
            restrictionField.RegisterValueChangedCallback(evt => {
                _settings.contentUpdateRestrictionMode = (CheckForContentUpdateRestrictionsOptions)evt.newValue;
                SaveSettings();
            });
            card.Add(restrictionField);

            foldout.Add(card);
            section.Add(foldout);
            section.Add(CreateDivider());
            parent.Add(section);
        }

        private void BuildProfileFields(VisualElement parent)
        {
            var profile = _settings.ActiveProfile;

            parent.Add(CreateConfigTextField("Profile Name", profile.profileName, v => {
                profile.profileName = v;
                SaveSettings();
                RefreshDashboard();
            }));

            var targetField = new EnumField("Build Target", profile.buildTarget);
            targetField.AddToClassList("config-field");
            targetField.RegisterValueChangedCallback(evt => {
                profile.buildTarget = (BuildTarget)evt.newValue;
                SaveSettings();
                RefreshDashboard();
            });
            parent.Add(targetField);

            parent.Add(CreateConfigTextField("Output Directory", profile.outputDirectory, v => {
                profile.outputDirectory = v;
                SaveSettings();
                RefreshDashboard();
            }));

            parent.Add(CreateConfigToggle("Development Build", profile.developmentBuild, v => {
                profile.developmentBuild = v;
                SaveSettings();
                RefreshDashboard();
            }));

            parent.Add(CreateConfigToggle("Clean Output", profile.cleanOutputBeforeBuild, v => {
                profile.cleanOutputBeforeBuild = v;
                SaveSettings();
                RefreshDashboard();
            }));

            parent.Add(CreateConfigToggle("Preflight Gate", profile.runPreflightBeforeBuild, v => {
                profile.runPreflightBeforeBuild = v;
                SaveSettings();
            }));

            parent.Add(CreateConfigToggle("Write Reports", profile.writeBuildReport, v => {
                profile.writeBuildReport = v;
                SaveSettings();
            }));
        }

        // ═════════════════════════════════════════════════════════════
        //  Build Pipeline CTA
        // ═════════════════════════════════════════════════════════════

        private void BuildPipelineSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var header = new Label("ONE-CLICK AUTOMATED PIPELINE");
            header.AddToClassList("section-header");
            section.Add(header);

            var ctaRow = new VisualElement();
            ctaRow.AddToClassList("cta-row");

            var dryRunBtn = new Button(ExecuteDryRun) { text = "DRY RUN" };
            dryRunBtn.AddToClassList("btn-build-run");
            dryRunBtn.AddToClassList("btn-left");
            ctaRow.Add(dryRunBtn);

            // ── BUILD ──
            var buildBtn = new Button(() => ExecuteBuild(autoRun: false)) { text = "BUILD" };
            buildBtn.AddToClassList("btn-build");
            buildBtn.AddToClassList("btn-mid");
            ctaRow.Add(buildBtn);

            // ── BUILD & RUN ──
            var buildRunBtn = new Button(() => ExecuteBuild(autoRun: true)) { text = "BUILD & RUN" };
            buildRunBtn.AddToClassList("btn-build-run");
            buildRunBtn.AddToClassList("btn-right");
            ctaRow.Add(buildRunBtn);

            section.Add(ctaRow);

            var contentRow = new VisualElement();
            contentRow.AddToClassList("cta-row");

            var prepareContentBtn = new Button(ExecutePrepareContentUpdate) { text = "PREP CONTENT UPDATE" };
            prepareContentBtn.AddToClassList("btn-build-run");
            prepareContentBtn.AddToClassList("btn-left");
            contentRow.Add(prepareContentBtn);

            var prepareLocalContentBtn = new Button(ExecutePrepareLocalContentUpdateTest) { text = "PREP LOCAL INCREMENTAL TEST" };
            prepareLocalContentBtn.AddToClassList("btn-build-run");
            prepareLocalContentBtn.AddToClassList("btn-mid");
            contentRow.Add(prepareLocalContentBtn);

            var incrementalBtn = new Button(ExecuteIncrementalContentBuild) { text = "BUILD INCREMENTAL CONTENT" };
            incrementalBtn.AddToClassList("btn-build");
            incrementalBtn.AddToClassList("btn-right");
            contentRow.Add(incrementalBtn);

            section.Add(contentRow);
            section.Add(CreateDivider());
            parent.Add(section);
        }

        private void BuildPreflightSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var header = new Label("PREFLIGHT GATE");
            header.AddToClassList("section-header");
            section.Add(header);

            var commandRow = new VisualElement();
            commandRow.AddToClassList("aux-row");

            var runBtn = new Button(ExecutePreflight) { text = "RUN PREFLIGHT" };
            runBtn.AddToClassList("btn-aux");
            runBtn.AddToClassList("btn-left");
            commandRow.Add(runBtn);

            var reportBtn = new Button(() => EditorUtility.RevealInFinder(Auto1KeyBuildPipeline.ReportRoot)) { text = "REPORTS" };
            reportBtn.AddToClassList("btn-aux");
            reportBtn.AddToClassList("btn-right");
            commandRow.Add(reportBtn);
            section.Add(commandRow);

            _preflightSummaryLabel = new Label("Preflight has not run yet.");
            _preflightSummaryLabel.AddToClassList("dashboard-text");
            section.Add(_preflightSummaryLabel);

            _preflightListContainer = new VisualElement();
            _preflightListContainer.AddToClassList("preflight-list");
            section.Add(_preflightListContainer);

            section.Add(CreateDivider());
            parent.Add(section);
        }

        // ═════════════════════════════════════════════════════════════
        //  Auxiliary Commands
        // ═════════════════════════════════════════════════════════════

        private void BuildAuxSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var header = new Label("AUXILIARY DISCRETE COMMANDS");
            header.AddToClassList("section-header");
            section.Add(header);

            // Row 1
            var row1 = new VisualElement();
            row1.AddToClassList("aux-row");

            var syncBtn = new Button(() => {
                int count = Auto1KeyBuildPipeline.BakeLevelRegistryScenePaths();
                SetBuildResult(true, $"Successfully synchronized and baked {count} scene paths in LevelRegistry.");
            }) { text = "SYNC SCENES" };
            syncBtn.AddToClassList("btn-aux");
            syncBtn.AddToClassList("btn-left");
            row1.Add(syncBtn);

            var groupBtn = new Button(() => {
                int count = Auto1KeyBuildPipeline.ExecuteAutoGrouping();
                SetBuildResult(true, $"Rules matching finished. Assigned {count} assets to Addressable groups.");
            }) { text = "AUTO GROUP" };
            groupBtn.AddToClassList("btn-aux");
            groupBtn.AddToClassList("btn-right");
            row1.Add(groupBtn);

            section.Add(row1);

            // Row 2
            var row2 = new VisualElement();
            row2.AddToClassList("aux-row");

            var auditBtn = new Button(ExecuteDependencyAudit) { text = "AUDIT DEPENDENCIES" };
            auditBtn.AddToClassList("btn-aux");
            auditBtn.AddToClassList("btn-left");
            row2.Add(auditBtn);

            var settingsBtn = new Button(() => {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            }) { text = "ADDRESSABLES" };
            settingsBtn.AddToClassList("btn-aux");
            settingsBtn.AddToClassList("btn-right");
            row2.Add(settingsBtn);

            section.Add(row2);
            section.Add(CreateDivider());
            parent.Add(section);
        }

        // ═════════════════════════════════════════════════════════════
        //  Dependency Audit Section
        // ═════════════════════════════════════════════════════════════

        private void BuildAuditSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var header = new Label("DEPENDENCY DUPLICATION AUDIT");
            header.AddToClassList("section-header");
            section.Add(header);

            _auditBannerContainer = new VisualElement();
            section.Add(_auditBannerContainer);
            RefreshAuditBanner();

            var auditActions = new VisualElement();
            auditActions.AddToClassList("aux-row");

            var runAuditBtn = new Button(ExecuteDependencyAudit) { text = "AUDIT DEPENDENCIES" };
            runAuditBtn.AddToClassList("btn-aux");
            runAuditBtn.AddToClassList("btn-left");
            auditActions.Add(runAuditBtn);

            var fixAuditBtn = new Button(ExecuteDependencyDeduplication) { text = "FIX DUPLICATES" };
            fixAuditBtn.AddToClassList("btn-fix");
            fixAuditBtn.AddToClassList("btn-right");
            auditActions.Add(fixAuditBtn);

            section.Add(auditActions);

            // Log box
            var logBox = new VisualElement();
            logBox.AddToClassList("log-box");

            var logHeader = new Label("Live Dependency Graph Details:");
            logHeader.AddToClassList("log-header");
            logBox.Add(logHeader);

            _logBodyLabel = new Label(_auditReportText);
            _logBodyLabel.AddToClassList("log-body");
            logBox.Add(_logBodyLabel);

            section.Add(logBox);
            parent.Add(section);
        }

        // ═════════════════════════════════════════════════════════════
        //  Actions
        // ═════════════════════════════════════════════════════════════

        private void ExecuteBuild(bool autoRun)
        {
            string title = autoRun ? "Confirm Build & Run" : "Confirm Build";
            string msg = autoRun
                ? "Run the full automated pipeline, build the player, and launch it?"
                : "Run the full automated pipeline and build the player?";
            string ok = autoRun ? "Build & Run" : "Build";

            if (!EditorUtility.DisplayDialog(title, msg, ok, "Cancel")) return;

            bool result = Auto1KeyBuildPipeline.BuildPlayer(
                out _buildLogs,
                _autoPromoteToggle?.value ?? _settings.autoPromoteRedundancies,
                autoRun: autoRun);

            SetBuildResult(result, _buildLogs);
            RefreshDashboard();
        }

        private void ExecuteDependencyAudit()
        {
            _auditReportText = Auto1KeyBuildPipeline.AuditDependencies(out var redundant);
            _redundantCount = redundant.Count;
            RefreshAuditBanner();
            RefreshLogBody();
        }

        private void ExecuteDependencyDeduplication()
        {
            if (!EditorUtility.DisplayDialog(
                "Resolve Duplicate Dependencies",
                "Promote duplicated implicit dependencies into the shared Addressables group now?",
                "Fix Duplicates",
                "Cancel"))
            {
                return;
            }

            bool resolved = Auto1KeyBuildPipeline.ResolveDependencyDuplicates(out _auditReportText, out _redundantCount);
            RefreshAuditBanner();
            RefreshLogBody();
            SetBuildResult(resolved, resolved
                ? "Dependency duplicates resolved. Re-run Addressables Analyze after the next content build for Unity's final bundle report."
                : $"Dependency deduplication completed with {_redundantCount} remaining implicit duplicate asset(s). Check the audit log for details.");
        }

        private void ExecutePrepareContentUpdate()
        {
            int changes = Auto1KeyBuildPipeline.EnsureContentUpdateSupport(out string report);
            SetBuildResult(true, report + $"Content update readiness pass completed. Applied changes: {changes}.");
            RefreshDashboard();
        }

        private void ExecutePrepareLocalContentUpdateTest()
        {
            bool result = Auto1KeyBuildPipeline.PrepareLocalIncrementalContentTest(out _buildLogs);
            SetBuildResult(result, _buildLogs);
            RefreshDashboard();
        }

        private void ExecuteIncrementalContentBuild()
        {
            if (!EditorUtility.DisplayDialog(
                "Build Incremental Content",
                "Build an Addressables content update using the previous addressables_content_state.bin?",
                "Build Incremental",
                "Cancel"))
            {
                return;
            }

            bool result = Auto1KeyBuildPipeline.BuildIncrementalContent(out _buildLogs);
            SetBuildResult(result, _buildLogs);
            RefreshDashboard();
        }

        private void ExecutePreflight()
        {
            var report = Auto1KeyBuildPipeline.RunPreflight(_settings);
            RenderPreflight(report);
            _auditReportText = Auto1KeyBuildPipeline.FormatPreflightReport(report);
            RefreshLogBody();
        }

        private void ExecuteDryRun()
        {
            var report = Auto1KeyBuildPipeline.RunPreflight(_settings);
            RenderPreflight(report);
            _buildLogs = Auto1KeyBuildPipeline.FormatPreflightReport(report);
            SetBuildResult(report.errorCount == 0, _buildLogs + "Dry Run completed without mutating Addressables or player output.");
        }

        private void SetBuildResult(bool success, string logs)
        {
            _lastBuildSucceeded = success;
            _buildLogs = logs;
            RefreshBuildResultBanner();
        }

        // ═════════════════════════════════════════════════════════════
        //  Dynamic UI Refresh Helpers
        // ═════════════════════════════════════════════════════════════

        private void RefreshBuildResultBanner()
        {
            if (_buildResultContainer == null) return;
            _buildResultContainer.Clear();

            if (!_lastBuildSucceeded.HasValue) return;

            bool passed = _lastBuildSucceeded.Value;
            _buildResultContainer.Add(CreateBanner(
                passed,
                passed ? "BUILD PROCESS PASSED" : "BUILD PROCESS FAILED",
                _buildLogs));
        }

        private void RefreshAuditBanner()
        {
            if (_auditBannerContainer == null) return;
            _auditBannerContainer.Clear();

            bool passed = _redundantCount == 0;
            _auditBannerContainer.Add(CreateBanner(
                passed,
                passed ? "ZERO DUPLICATION DETECTED" : "Implicit Dependency Duplication Warnings",
                passed
                    ? "Excellent! All shared assets are perfectly decoupled or encapsulated."
                    : $"{_redundantCount} asset(s) are duplicated across different bundles. Use 'Fix Duplicates' to promote shared dependencies before building."));
        }

        private void RefreshLogBody()
        {
            if (_logBodyLabel != null)
                _logBodyLabel.text = _auditReportText;
        }

        private void RefreshDashboard()
        {
            if (_dashboardLabel == null || _settings == null) return;
            var profile = _settings.ActiveProfile;
            var scenes = Auto1KeyBuildPipeline.CollectPlayerBuildScenes(_settings, out _);
            _dashboardLabel.text =
                $"Profile: {profile.profileName}\n" +
                $"Target: {profile.buildTarget} | Scenes: {scenes.Count} | Output: {profile.outputDirectory}/{profile.buildTarget}\n" +
                $"Development: {(profile.developmentBuild ? "On" : "Off")} | Clean: {(profile.cleanOutputBeforeBuild ? "On" : "Off")} | Preflight: {(profile.runPreflightBeforeBuild ? "On" : "Off")} | Reports: {(profile.writeBuildReport ? "On" : "Off")}";
        }

        private void RenderPreflight(Auto1KeyBuildReport report)
        {
            if (_preflightSummaryLabel != null)
            {
                _preflightSummaryLabel.text = report.summary;
            }

            if (_preflightListContainer == null) return;
            _preflightListContainer.Clear();

            foreach (var check in report.checks)
            {
                var row = new VisualElement();
                row.AddToClassList("preflight-row");
                row.AddToClassList(check.severity == Auto1KeyBuildCheckSeverity.Error ? "preflight-error" : check.severity == Auto1KeyBuildCheckSeverity.Warning ? "preflight-warning" : "preflight-pass");

                var title = new Label(check.title);
                title.AddToClassList("preflight-title");
                row.Add(title);

                var message = new Label(check.message);
                message.AddToClassList("preflight-message");
                row.Add(message);

                _preflightListContainer.Add(row);
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  Scene Paths List
        // ═════════════════════════════════════════════════════════════

        private void RebuildScenePathsList()
        {
            if (_scenePathsContainer == null || _settings == null) return;
            if (_settings.sceneFolderPaths == null) _settings.sceneFolderPaths = new List<string>();
            _scenePathsContainer.Clear();

            for (int i = 0; i < _settings.sceneFolderPaths.Count; i++)
            {
                int idx = i; // capture for closure
                var row = new VisualElement();
                row.AddToClassList("path-row");

                var tf = new TextField { value = _settings.sceneFolderPaths[idx] };
                tf.AddToClassList("path-field");
                tf.style.flexGrow = 1;
                tf.style.flexShrink = 1;
                tf.style.minWidth = 0;
                tf.RegisterValueChangedCallback(evt => {
                    _settings.sceneFolderPaths[idx] = evt.newValue;
                    SaveSettings();
                });
                row.Add(tf);

                var removeBtn = new Button(() => RemoveScenePath(idx)) { text = "x" };
                removeBtn.AddToClassList("btn-remove");
                removeBtn.style.flexShrink = 0;
                row.Add(removeBtn);

                _scenePathsContainer.Add(row);
            }
        }

        private void AddScenePath()
        {
            if (_settings.sceneFolderPaths == null) _settings.sceneFolderPaths = new List<string>();
            _settings.sceneFolderPaths.Add("Assets/");
            SaveSettings();
            RebuildScenePathsList();
        }

        private void RemoveScenePath(int index)
        {
            if (_settings.sceneFolderPaths != null && index >= 0 && index < _settings.sceneFolderPaths.Count)
            {
                _settings.sceneFolderPaths.RemoveAt(index);
                SaveSettings();
                RebuildScenePathsList();
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  Shader Paths List
        // ═════════════════════════════════════════════════════════════

        private void RebuildShaderPathsList()
        {
            if (_shaderPathsContainer == null || _settings == null) return;
            if (_settings.shaderFolderPaths == null) _settings.shaderFolderPaths = new List<string>();
            _shaderPathsContainer.Clear();

            for (int i = 0; i < _settings.shaderFolderPaths.Count; i++)
            {
                int idx = i; // capture for closure
                var row = new VisualElement();
                row.AddToClassList("path-row");

                var tf = new TextField { value = _settings.shaderFolderPaths[idx] };
                tf.AddToClassList("path-field");
                tf.style.flexGrow = 1;
                tf.style.flexShrink = 1;
                tf.style.minWidth = 0;
                tf.RegisterValueChangedCallback(evt => {
                    _settings.shaderFolderPaths[idx] = evt.newValue;
                    SaveSettings();
                });
                row.Add(tf);

                var removeBtn = new Button(() => RemoveShaderPath(idx)) { text = "x" };
                removeBtn.AddToClassList("btn-remove");
                removeBtn.style.flexShrink = 0;
                row.Add(removeBtn);

                _shaderPathsContainer.Add(row);
            }
        }

        private void AddShaderPath()
        {
            _settings.shaderFolderPaths.Add("Assets/");
            SaveSettings();
            RebuildShaderPathsList();
        }

        private void RemoveShaderPath(int index)
        {
            if (index >= 0 && index < _settings.shaderFolderPaths.Count)
            {
                _settings.shaderFolderPaths.RemoveAt(index);
                SaveSettings();
                RebuildShaderPathsList();
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  Persistence
        // ═════════════════════════════════════════════════════════════

        private void SaveSettings()
        {
            if (_settings != null)
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  Reusable UI Factory Methods
        // ═════════════════════════════════════════════════════════════

        private VisualElement CreateDivider()
        {
            var div = new VisualElement();
            div.AddToClassList("divider");
            return div;
        }

        private VisualElement CreateSpacer(float height)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            return spacer;
        }

        private TextField CreateConfigTextField(string label, string initialValue, System.Action<string> onChange)
        {
            var tf = new TextField(label) { value = initialValue };
            tf.AddToClassList("config-field");
            tf.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            return tf;
        }

        private Toggle CreateConfigToggle(string label, bool initialValue, System.Action<bool> onChange)
        {
            var toggle = new Toggle(label) { value = initialValue };
            toggle.AddToClassList("config-field");
            toggle.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            return toggle;
        }

        private Label CreateGroupTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("card-label-azure");
            return label;
        }

        private VisualElement CreateBanner(bool passed, string title, string message)
        {
            var banner = new VisualElement();
            banner.AddToClassList("banner");
            banner.AddToClassList(passed ? "banner-pass" : "banner-fail");

            var titleLabel = new Label($"{(passed ? "[PASS]" : "[FAIL]")}  {title}");
            titleLabel.AddToClassList(passed ? "banner-title-pass" : "banner-title-fail");
            banner.Add(titleLabel);

            var bodyLabel = new Label(message);
            bodyLabel.AddToClassList("banner-body");
            banner.Add(bodyLabel);

            return banner;
        }
    }
}
