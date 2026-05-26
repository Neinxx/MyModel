using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEditor.Build.Reporting;
using WorldSceneModule.Editor;
using WorldSceneModule.Runtime;

namespace Auto1KeyBuildModule.Editor
{
    public enum Auto1KeyBuildCheckSeverity
    {
        Pass,
        Warning,
        Error
    }

    [Serializable]
    public class Auto1KeyBuildCheck
    {
        public string title;
        public string message;
        public Auto1KeyBuildCheckSeverity severity;

        public Auto1KeyBuildCheck(string title, string message, Auto1KeyBuildCheckSeverity severity)
        {
            this.title = title;
            this.message = message;
            this.severity = severity;
        }
    }

    [Serializable]
    public class Auto1KeyBuildReport
    {
        public string profileName;
        public string buildTarget;
        public string outputPath;
        public string generatedAt;
        public bool succeeded;
        public int sceneCount;
        public int errorCount;
        public int warningCount;
        public string summary;
        public List<string> scenes = new List<string>();
        public List<Auto1KeyBuildCheck> checks = new List<Auto1KeyBuildCheck>();
    }

    /// <summary>
    /// 工业级可扩展一键式资产自动编组与打包引擎 (Auto1KeyBuild Pipeline)
    /// 承载规则自动分组、依赖冗余审计与防爆提升、以及无锁静默构建的核心能力
    /// </summary>
    public static class Auto1KeyBuildPipeline
    {
        public const string ReportRoot = "Logs/Auto1KeyBuild";

        /// <summary>
        /// 执行一键式完整打包流程 (Path Baking -> Auto Group -> Audit -> Close Windows -> Build)
        /// </summary>
        public static bool ExecuteOneKeyBuild(out string reportMessage, bool? autoPromoteOverride = null)
        {
            reportMessage = "";
            try
            {
                // 加载持久化配置
                var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
                bool autoPromote = autoPromoteOverride ?? buildSettings.autoPromoteRedundancies;
                // 1. 关卡注册表物理路径自动预烘焙
                int bakedCount = BakeLevelRegistryScenePaths();
                reportMessage += $"✔ [LevelRegistry] Path Pre-baking completed. Synced {bakedCount} scenes.\n";

                // 2. 执行规则化自动编组
                int groupedCount = ExecuteAutoGrouping();
                reportMessage += $"✔ [Auto-Grouping] Rules matching completed. Assigned {groupedCount} assets to Groups.\n";

                int protectedLocalCount = RestoreProtectedLocalAddressables();
                if (protectedLocalCount > 0)
                {
                    reportMessage += $"✔ [Boot Assets] Restored {protectedLocalCount} protected boot asset(s) to the local Addressables group.\n";
                }

                // 3. 依赖图谱关系分析与冗余防护
                var auditReport = AuditDependencies(out var redundantAssets);
                int normalizedSharedAddressCount = NormalizeSharedGroupAddresses();
                if (redundantAssets.Count > 0)
                {
                    reportMessage += $"⚠ [Dependency Audit] Detected {redundantAssets.Count} redundant implicit assets.\n";
                    if (autoPromote)
                    {
                        int promotedCount = AutoPromoteRedundancies(redundantAssets);
                        reportMessage += $"✔ [Auto-Promotion] Promoted {promotedCount} assets to '{buildSettings.sharedGroupName}' to eliminate duplication.\n";
                    }
                }
                else
                {
                    reportMessage += "✔ [Dependency Audit] 0 Redundancy detected. Package size optimized.\n";
                }

                if (normalizedSharedAddressCount > 0)
                {
                    reportMessage += $"✔ [Shared Group Hygiene] Normalized {normalizedSharedAddressCount} shared address(es) to avoid address collisions.\n";
                }

                // 4. 清理环境，解除 GUI 窗口文件读写锁定 (解决 Bug #4 SBP 崩溃问题)
                SafeCloseLayoutWindows();
                reportMessage += "✔ [File Lock Safeguards] SBP conflicting windows closed & references released.\n";

                // 5. 触发 Addressables 工业级打包
                Debug.Log("<color=#3DB8FF><b>[Auto1KeyBuild]</b></color> Starting Addressables Player Build...");
                AddressableAssetSettings.BuildPlayerContent(out var buildResult);

                if (string.IsNullOrEmpty(buildResult.Error))
                {
                    reportMessage += $"★ [Build Result] Addressables Build Succeeded! Duration: {buildResult.Duration:F2}s.\n";
                    Debug.Log($"<color=#40B84F><b>[Auto1KeyBuild]</b></color> Build succeeded! Path: {buildResult.OutputPath}");
                    return true;
                }
                else
                {
                    reportMessage += $"✘ [Build Result] Addressables Build Failed: {buildResult.Error}\n";
                    Debug.LogError($"<color=#873535><b>[Auto1KeyBuild]</b></color> Build failed: {buildResult.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                reportMessage += $"✘ [Fatal Exception] Build pipeline aborted: {ex.Message}\n";
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// 执行完整的 Unity Player 构建流程（含 Addressables 预处理 + 玩家包输出）
        /// </summary>
        /// <param name="autoRun">true = 构建完毕后自动启动应用</param>
        public static bool BuildPlayer(out string reportMessage, bool? autoPromoteOverride = null, bool autoRun = false)
        {
            var settings = Auto1KeyBuildSettings.GetOrCreateSettings();
            var profile = settings.ActiveProfile;

            if (profile.runPreflightBeforeBuild)
            {
                var preflight = RunPreflight(settings);
                if (preflight.errorCount > 0)
                {
                    reportMessage = FormatPreflightReport(preflight);
                    reportMessage += "✘ [Player Build] Blocked by Preflight errors.\n";
                    WriteBuildReport(preflight, reportMessage);
                    return false;
                }
            }

            // 1. 先执行完整的 Addressables 预处理管线
            bool addressablesOk = ExecuteOneKeyBuild(out reportMessage, autoPromoteOverride);
            if (!addressablesOk)
            {
                reportMessage += "✘ [Player Build] Skipped — Addressables pipeline failed.\n";
                return false;
            }

            try
            {
                // 2. 智能收集玩家包场景：Build Settings 已启用场景 + 配置目录扫描结果
                var scenes = CollectPlayerBuildScenes(settings, out string sceneSourceReport);
                reportMessage += sceneSourceReport;

                if (scenes.Count == 0)
                {
                    reportMessage += "✘ [Player Build] No scenes found. Configure Scene Build Folders or enable scenes in Build Settings.\n";
                    Debug.LogError("<color=#873535><b>[Auto1KeyBuild]</b></color> No scenes found for Player Build.");
                    return false;
                }

                // 3. 确定输出路径与构建目标
                BuildTarget target = profile.buildTarget;
                string ext = target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64 ? ".exe"
                           : target == BuildTarget.StandaloneOSX ? ".app"
                           : target == BuildTarget.Android ? ".apk"
                           : "";

                string productName = PlayerSettings.productName;
                if (string.IsNullOrWhiteSpace(productName)) productName = "Build";

                if (!TryResolveOutputDirectory(profile, out string outputDir, out string outputError))
                {
                    reportMessage += $"✘ [Output Hygiene] {outputError}\n";
                    Debug.LogError($"<color=#873535><b>[Auto1KeyBuild]</b></color> {outputError}");
                    return false;
                }

                if (profile.cleanOutputBeforeBuild && Directory.Exists(outputDir))
                {
                    if (!IsSafeCleanOutputDirectory(outputDir, target, out string safetyReason))
                    {
                        reportMessage += $"✘ [Output Hygiene] Refused to clean unsafe output directory: {outputDir}. {safetyReason}\n";
                        Debug.LogError($"<color=#873535><b>[Auto1KeyBuild]</b></color> Refused to clean unsafe output directory: {outputDir}. {safetyReason}");
                        return false;
                    }

                    Directory.Delete(outputDir, true);
                    reportMessage += $"✔ [Output Hygiene] Cleaned output directory: {outputDir}\n";
                }

                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                string outputPath = Path.Combine(outputDir, productName + ext);

                // 4. 构建选项
                BuildOptions options = BuildOptions.None;
                if (profile.developmentBuild) options |= BuildOptions.Development;
                if (autoRun) options |= BuildOptions.AutoRunPlayer;

                Debug.Log($"<color=#3DB8FF><b>[Auto1KeyBuild]</b></color> Starting Player Build → {outputPath} (Target: {target}, AutoRun: {autoRun})");

                // 5. 触发 Unity Player Build
                BuildReport buildReport;
                var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
                AddressableAssetSettings.PlayerBuildOption previousPlayerBuildOption = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                bool shouldRestoreAddressablesPlayerBuildOption = false;

                if (addressableSettings != null)
                {
                    previousPlayerBuildOption = addressableSettings.BuildAddressablesWithPlayerBuild;
                    if (previousPlayerBuildOption != AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer)
                    {
                        addressableSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                        shouldRestoreAddressablesPlayerBuildOption = true;
                        reportMessage += "✔ [Addressables Player Hook] Disabled automatic Addressables rebuild during Player Build. Prebuilt content will be included from Addressables.BuildPath.\n";
                    }
                }

                SafeCloseLayoutWindows();

                try
                {
                    buildReport = BuildPipeline.BuildPlayer(scenes.ToArray(), outputPath, target, options);
                }
                finally
                {
                    if (shouldRestoreAddressablesPlayerBuildOption && addressableSettings != null)
                    {
                        addressableSettings.BuildAddressablesWithPlayerBuild = previousPlayerBuildOption;
                    }
                }

                if (buildReport.summary.result == BuildResult.Succeeded)
                {
                    double totalMinutes = buildReport.summary.totalTime.TotalSeconds;
                    long sizeBytes = (long)buildReport.summary.totalSize;
                    string sizeMB = (sizeBytes / (1024.0 * 1024.0)).ToString("F2");

                    reportMessage += $"★ [Player Build] Succeeded! Duration: {totalMinutes:F1}s | Size: {sizeMB} MB | Output: {outputPath}\n";
                    Debug.Log($"<color=#40B84F><b>[Auto1KeyBuild]</b></color> Player Build succeeded → {outputPath}");
                    WriteBuildReport(CreateFinalReport(settings, scenes, outputPath, true, reportMessage), reportMessage);
                    return true;
                }
                else
                {
                    reportMessage += $"✘ [Player Build] Failed: {buildReport.summary.result}\n";
                    Debug.LogError($"<color=#873535><b>[Auto1KeyBuild]</b></color> Player Build failed: {buildReport.summary.result}");
                    WriteBuildReport(CreateFinalReport(settings, scenes, outputPath, false, reportMessage), reportMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                reportMessage += $"✘ [Player Build Fatal] {ex.Message}\n";
                Debug.LogException(ex);
                return false;
            }
        }

        public static Auto1KeyBuildReport RunPreflight(Auto1KeyBuildSettings settings = null)
        {
            settings = settings != null ? settings : Auto1KeyBuildSettings.GetOrCreateSettings();
            var profile = settings.ActiveProfile;
            var report = new Auto1KeyBuildReport
            {
                profileName = profile.profileName,
                buildTarget = profile.buildTarget.ToString(),
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                outputPath = BuildOutputDirectory(profile)
            };

            AddCheck(report, "Profile", string.IsNullOrWhiteSpace(profile.profileName) ? "Profile name is empty." : $"Active profile: {profile.profileName}", string.IsNullOrWhiteSpace(profile.profileName) ? Auto1KeyBuildCheckSeverity.Warning : Auto1KeyBuildCheckSeverity.Pass);

            var scenes = CollectPlayerBuildScenes(settings, out _);
            report.scenes.AddRange(scenes);
            report.sceneCount = scenes.Count;
            AddCheck(report, "Scenes", scenes.Count == 0 ? "No scenes found. Configure Scene Build Folders or enable scenes in Build Settings." : $"Resolved {scenes.Count} scene(s).", scenes.Count == 0 ? Auto1KeyBuildCheckSeverity.Error : Auto1KeyBuildCheckSeverity.Pass);

            ValidateFolders(report, "Scene Folder", settings.sceneFolderPaths, true);
            ValidateFolders(report, "Shader Folder", settings.shaderFolderPaths, false);

            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddCheck(report, "Addressables", addressableSettings == null ? "Addressables settings asset is missing." : "Addressables settings asset found.", addressableSettings == null ? Auto1KeyBuildCheckSeverity.Error : Auto1KeyBuildCheckSeverity.Pass);
            if (addressableSettings != null)
            {
                if (addressableSettings.BuildRemoteCatalog)
                {
                    bool validRemoteLoadPath = TryValidateRemoteLoadPath(addressableSettings, out string remoteLoadPathMessage);
                    AddCheck(report, "Remote Catalog", validRemoteLoadPath ? $"Remote catalog is enabled. {remoteLoadPathMessage}" : remoteLoadPathMessage, validRemoteLoadPath ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Error);
                }
                else
                {
                    AddCheck(report, "Remote Catalog", "Remote catalog is disabled. Full local player builds will not request remote catalogs.", Auto1KeyBuildCheckSeverity.Pass);
                }

                AddCheck(report, "Unique Bundle IDs", addressableSettings.UniqueBundleIds ? "Unique bundle ids are enabled." : "Unique bundle ids are disabled. Enable them for safer content updates.", addressableSettings.UniqueBundleIds ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Warning);

                int groupsWithoutContentUpdateSchema = CountGroupsMissingContentUpdateSchema(addressableSettings);
                AddCheck(report, "Content Update Schemas", groupsWithoutContentUpdateSchema == 0 ? "All bundled Addressables groups have ContentUpdateGroupSchema." : $"{groupsWithoutContentUpdateSchema} bundled group(s) are missing ContentUpdateGroupSchema.", groupsWithoutContentUpdateSchema == 0 ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Warning);

                bool hasPreviousContentState = TryResolvePreviousContentStatePath(settings, out string previousStatePath, out string previousStateError);
                AddCheck(report, "Previous Content State", hasPreviousContentState ? $"Previous state file resolved: {previousStatePath}" : previousStateError, hasPreviousContentState ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Warning);
            }

            string[] registryGUIDs = AssetDatabase.FindAssets("t:LevelRegistry");
            AddCheck(report, "LevelRegistry", registryGUIDs == null || registryGUIDs.Length == 0 ? "No LevelRegistry asset found. Scene path baking will be skipped." : $"Found {registryGUIDs.Length} LevelRegistry asset(s).", registryGUIDs == null || registryGUIDs.Length == 0 ? Auto1KeyBuildCheckSeverity.Warning : Auto1KeyBuildCheckSeverity.Pass);
            ValidateLevelRegistryBuildRules(report, registryGUIDs);
            ValidateGeneratedAddressRules(report, settings);

            if (addressableSettings != null)
            {
                int duplicateAddressCount = CountDuplicateAddressables(addressableSettings);
                AddCheck(report, "Address Duplicates", duplicateAddressCount == 0 ? "No duplicate Addressables addresses detected." : $"{duplicateAddressCount} duplicate address(es) detected.", duplicateAddressCount == 0 ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Warning);
            }

            if (!TryResolveOutputDirectory(profile, out string outputDir, out string outputError))
            {
                AddCheck(report, "Output Directory", outputError, Auto1KeyBuildCheckSeverity.Error);
            }
            else
            {
                AddCheck(report, "Output Directory", $"Output directory: {outputDir}", Auto1KeyBuildCheckSeverity.Pass);

                if (profile.cleanOutputBeforeBuild && Directory.Exists(outputDir))
                {
                    bool safeToClean = IsSafeCleanOutputDirectory(outputDir, profile.buildTarget, out string safetyReason);
                    AddCheck(report, "Output Cleanup", safeToClean ? $"Clean-before-build is enabled and guarded for: {outputDir}" : $"Clean-before-build target is unsafe: {outputDir}. {safetyReason}", safeToClean ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Error);
                }
                else if (profile.cleanOutputBeforeBuild)
                {
                    AddCheck(report, "Output Cleanup", $"Clean-before-build is enabled. Directory does not exist yet: {outputDir}", Auto1KeyBuildCheckSeverity.Pass);
                }
            }

            report.summary = $"Preflight completed. Errors: {report.errorCount}, Warnings: {report.warningCount}, Scenes: {report.sceneCount}.";
            report.succeeded = report.errorCount == 0;
            return report;
        }

        public static string FormatPreflightReport(Auto1KeyBuildReport report)
        {
            var text = $"[Preflight] {report.summary}\n";
            foreach (var check in report.checks)
            {
                string prefix = check.severity == Auto1KeyBuildCheckSeverity.Error ? "✘" : check.severity == Auto1KeyBuildCheckSeverity.Warning ? "⚠" : "✔";
                text += $"{prefix} [{check.title}] {check.message}\n";
            }

            return text;
        }

        public static string WriteBuildReport(Auto1KeyBuildReport report, string rawLog)
        {
            if (report == null)
            {
                return string.Empty;
            }

            var settings = Auto1KeyBuildSettings.GetOrCreateSettings();
            if (!settings.ActiveProfile.writeBuildReport)
            {
                return string.Empty;
            }

            Directory.CreateDirectory(ReportRoot);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeProfileName = MakeSafeFileName(string.IsNullOrWhiteSpace(report.profileName) ? "Profile" : report.profileName);
            string jsonPath = Path.Combine(ReportRoot, $"{stamp}_{safeProfileName}.json");
            string logPath = Path.Combine(ReportRoot, $"{stamp}_{safeProfileName}.log");

            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
            File.WriteAllText(logPath, rawLog ?? string.Empty);
            AssetDatabase.Refresh();
            return jsonPath;
        }

        private static Auto1KeyBuildReport CreateFinalReport(Auto1KeyBuildSettings settings, List<string> scenes, string outputPath, bool succeeded, string summary)
        {
            var preflight = RunPreflight(settings);
            preflight.outputPath = outputPath;
            preflight.succeeded = succeeded;
            preflight.summary = summary;
            preflight.scenes = scenes != null ? new List<string>(scenes) : new List<string>();
            preflight.sceneCount = preflight.scenes.Count;
            return preflight;
        }

        private static void AddCheck(Auto1KeyBuildReport report, string title, string message, Auto1KeyBuildCheckSeverity severity)
        {
            report.checks.Add(new Auto1KeyBuildCheck(title, message, severity));
            if (severity == Auto1KeyBuildCheckSeverity.Error) report.errorCount++;
            if (severity == Auto1KeyBuildCheckSeverity.Warning) report.warningCount++;
        }

        private static void ValidateFolders(Auto1KeyBuildReport report, string title, List<string> folders, bool errorWhenEmpty)
        {
            if (folders == null || folders.Count == 0)
            {
                AddCheck(report, title, "No folders configured.", errorWhenEmpty ? Auto1KeyBuildCheckSeverity.Error : Auto1KeyBuildCheckSeverity.Warning);
                return;
            }

            foreach (var folder in folders)
            {
                string normalized = NormalizeAssetFolderPath(folder);
                bool valid = !string.IsNullOrWhiteSpace(normalized) && AssetDatabase.IsValidFolder(normalized);
                AddCheck(report, title, valid ? $"{normalized} is valid." : $"{folder} is invalid.", valid ? Auto1KeyBuildCheckSeverity.Pass : (errorWhenEmpty ? Auto1KeyBuildCheckSeverity.Error : Auto1KeyBuildCheckSeverity.Warning));
            }
        }

        private static int CountGroupsMissingContentUpdateSchema(AddressableAssetSettings settings)
        {
            if (settings == null) return 0;

            int count = 0;
            foreach (var group in settings.groups)
            {
                if (group == null || IsBuiltInAddressableGroup(group)) continue;
                if (group.GetSchema<BundledAssetGroupSchema>() == null) continue;
                if (group.GetSchema<ContentUpdateGroupSchema>() == null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ValidateLevelRegistryBuildRules(Auto1KeyBuildReport report, string[] registryGUIDs)
        {
            if (registryGUIDs == null || registryGUIDs.Length == 0)
            {
                return;
            }

            foreach (string guid in registryGUIDs)
            {
                string registryPath = AssetDatabase.GUIDToAssetPath(guid);
                var registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(registryPath);
                if (registry == null)
                {
                    AddCheck(report, "LevelRegistry Rules", $"Cannot load LevelRegistry at {registryPath}.", Auto1KeyBuildCheckSeverity.Error);
                    continue;
                }

                if (registry.levels == null || registry.levels.Count == 0)
                {
                    AddCheck(report, "LevelRegistry Rules", $"{registryPath} has no registered levels.", Auto1KeyBuildCheckSeverity.Warning);
                    continue;
                }

                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var duplicateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int missingSceneAssets = 0;
                int emptyNames = 0;

                foreach (var level in registry.levels)
                {
                    if (string.IsNullOrWhiteSpace(level.levelName))
                    {
                        emptyNames++;
                    }
                    else if (!seenNames.Add(level.levelName))
                    {
                        duplicateNames.Add(level.levelName);
                    }

                    if (level.sceneAsset == null)
                    {
                        missingSceneAssets++;
                    }
                }

                if (emptyNames > 0)
                {
                    AddCheck(report, "LevelRegistry Rules", $"{registryPath} contains {emptyNames} level(s) with empty names.", Auto1KeyBuildCheckSeverity.Error);
                }

                if (duplicateNames.Count > 0)
                {
                    AddCheck(report, "LevelRegistry Rules", $"{registryPath} contains duplicate level names: {string.Join(", ", duplicateNames)}.", Auto1KeyBuildCheckSeverity.Error);
                }

                if (missingSceneAssets > 0)
                {
                    AddCheck(report, "LevelRegistry Rules", $"{registryPath} contains {missingSceneAssets} level(s) without SceneAsset references. They cannot be auto-grouped.", Auto1KeyBuildCheckSeverity.Error);
                }

                if (emptyNames == 0 && duplicateNames.Count == 0 && missingSceneAssets == 0)
                {
                    AddCheck(report, "LevelRegistry Rules", $"{registryPath} level names and SceneAsset references are valid.", Auto1KeyBuildCheckSeverity.Pass);
                }
            }
        }

        private static void ValidateGeneratedAddressRules(Auto1KeyBuildReport report, Auto1KeyBuildSettings buildSettings)
        {
            var plannedAddresses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            string[] registryGUIDs = AssetDatabase.FindAssets("t:LevelRegistry");
            if (registryGUIDs != null)
            {
                foreach (string guid in registryGUIDs)
                {
                    string registryPath = AssetDatabase.GUIDToAssetPath(guid);
                    var registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(registryPath);
                    if (registry == null || registry.levels == null)
                    {
                        continue;
                    }

                    foreach (var level in registry.levels)
                    {
                        if (string.IsNullOrWhiteSpace(level.levelName))
                        {
                            continue;
                        }

                        AddPlannedAddress(plannedAddresses, level.levelName, $"LevelRegistry:{registryPath}");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(buildSettings.decalConfigSearchQuery))
            {
                AddCheck(report, "Generated Addresses", "Decal config search query is empty.", Auto1KeyBuildCheckSeverity.Warning);
            }
            else
            {
                string[] decalConfigGUIDs = AssetDatabase.FindAssets(buildSettings.decalConfigSearchQuery);
                foreach (string guid in decalConfigGUIDs)
                {
                    string configPath = AssetDatabase.GUIDToAssetPath(guid);
                    AddPlannedAddress(plannedAddresses, Path.GetFileNameWithoutExtension(configPath), configPath);
                }
            }

            if (buildSettings.shaderFolderPaths != null)
            {
                foreach (string folderPath in buildSettings.shaderFolderPaths)
                {
                    string normalizedFolder = NormalizeAssetFolderPath(folderPath);
                    if (string.IsNullOrWhiteSpace(normalizedFolder) || !AssetDatabase.IsValidFolder(normalizedFolder))
                    {
                        continue;
                    }

                    string[] shaderGUIDs = AssetDatabase.FindAssets("t:Shader", new[] { normalizedFolder });
                    foreach (string guid in shaderGUIDs)
                    {
                        string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
                        AddPlannedAddress(plannedAddresses, Path.GetFileNameWithoutExtension(shaderPath), shaderPath);
                    }
                }
            }

            var duplicates = new List<string>();
            foreach (var kvp in plannedAddresses)
            {
                if (kvp.Value.Count > 1)
                {
                    duplicates.Add($"{kvp.Key} ({string.Join(", ", kvp.Value)})");
                }
            }

            AddCheck(report, "Generated Addresses", duplicates.Count == 0 ? "Auto-generated Addressables addresses are unique." : $"Auto-generated Addressables address conflict(s): {string.Join("; ", duplicates)}.", duplicates.Count == 0 ? Auto1KeyBuildCheckSeverity.Pass : Auto1KeyBuildCheckSeverity.Error);
        }

        private static void AddPlannedAddress(Dictionary<string, List<string>> plannedAddresses, string address, string source)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            if (!plannedAddresses.TryGetValue(address, out var sources))
            {
                sources = new List<string>();
                plannedAddresses[address] = sources;
            }

            sources.Add(source);
        }

        private static int CountDuplicateAddressables(AddressableAssetSettings settings)
        {
            var addresses = new HashSet<string>();
            var duplicates = new HashSet<string>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.address)) continue;
                    if (!addresses.Add(entry.address))
                    {
                        duplicates.Add(entry.address);
                    }
                }
            }

            return duplicates.Count;
        }

        private static string BuildOutputDirectory(Auto1KeyBuildProfile profile)
        {
            return TryResolveOutputDirectory(profile, out string outputDir, out _) ? outputDir : string.Empty;
        }

        private static bool TryResolveOutputDirectory(Auto1KeyBuildProfile profile, out string outputDir, out string error)
        {
            outputDir = string.Empty;
            error = string.Empty;

            if (profile == null)
            {
                error = "Active build profile is missing.";
                return false;
            }

            string baseDirectory = string.IsNullOrWhiteSpace(profile.outputDirectory) ? "Builds" : profile.outputDirectory.Trim();
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                error = "Output directory is empty.";
                return false;
            }

            try
            {
                string resolvedBase = Path.IsPathRooted(baseDirectory)
                    ? baseDirectory
                    : Path.Combine(ProjectRoot, baseDirectory);

                outputDir = Path.GetFullPath(Path.Combine(resolvedBase, profile.buildTarget.ToString()));
                return true;
            }
            catch (Exception ex)
            {
                error = $"Output directory is invalid: {ex.Message}";
                return false;
            }
        }

        private static string ProjectRoot
        {
            get
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
        }

        private static bool IsSafeCleanOutputDirectory(string outputDir, BuildTarget target, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(outputDir))
            {
                reason = "Path is empty.";
                return false;
            }

            string fullPath;
            try
            {
                fullPath = NormalizeFullPath(outputDir);
            }
            catch (Exception ex)
            {
                reason = $"Path cannot be resolved: {ex.Message}";
                return false;
            }

            string root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && string.Equals(fullPath, NormalizeFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                reason = "Path resolves to a filesystem root.";
                return false;
            }

            if (!string.Equals(Path.GetFileName(fullPath), target.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Final directory must be named '{target}' so only target-specific build output can be cleaned.";
                return false;
            }

            string projectRoot = NormalizeFullPath(ProjectRoot);
            if (IsSamePath(fullPath, projectRoot))
            {
                reason = "Path resolves to the project root.";
                return false;
            }

            string[] protectedDirectories =
            {
                "Assets",
                "ProjectSettings",
                "Packages",
                "Library",
                "Temp",
                "Logs",
                "UserSettings"
            };

            foreach (string protectedDirectory in protectedDirectories)
            {
                string protectedPath = NormalizeFullPath(Path.Combine(projectRoot, protectedDirectory));
                if (IsSameOrChildPath(fullPath, protectedPath))
                {
                    reason = $"Path is inside protected project directory '{protectedDirectory}'.";
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeFullPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(NormalizeFullPath(left), NormalizeFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameOrChildPath(string candidate, string parent)
        {
            string normalizedCandidate = NormalizeFullPath(candidate);
            string normalizedParent = NormalizeFullPath(parent);

            if (string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidate.StartsWith(normalizedParent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value.Replace(' ', '_');
        }

        /// <summary>
        /// 收集 Player Build 场景：保留 Build Settings 显式启用场景，同时从配置目录自动扫描 .unity 场景。
        /// </summary>
        public static List<string> CollectPlayerBuildScenes(Auto1KeyBuildSettings buildSettings, out string reportMessage)
        {
            var scenes = new List<string>();
            var seen = new HashSet<string>();
            int buildSettingsCount = 0;
            int scannedCount = 0;

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && AddScenePath(scene.path, scenes, seen))
                {
                    buildSettingsCount++;
                }
            }

            if (buildSettings != null && buildSettings.sceneFolderPaths != null)
            {
                foreach (var folderPath in buildSettings.sceneFolderPaths)
                {
                    string normalizedFolder = NormalizeAssetFolderPath(folderPath);
                    if (string.IsNullOrWhiteSpace(normalizedFolder) || !AssetDatabase.IsValidFolder(normalizedFolder))
                    {
                        Debug.LogWarning($"<color=#FFB347><b>[Auto1KeyBuild]</b></color> Skipping invalid scene scan path: '{folderPath}'");
                        continue;
                    }

                    string[] sceneGUIDs = AssetDatabase.FindAssets("t:SceneAsset", new[] { normalizedFolder });
                    foreach (var guid in sceneGUIDs)
                    {
                        string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                        if (AddScenePath(scenePath, scenes, seen))
                        {
                            scannedCount++;
                        }
                    }

                    Debug.Log($"<color=#3DB8FF><b>[Auto1KeyBuild]</b></color> Scanned scene folder: '{normalizedFolder}' — found {sceneGUIDs.Length} scene(s).");
                }
            }

            reportMessage = $"✔ [Scene Resolver] Collected {scenes.Count} scene(s). Build Settings: {buildSettingsCount}, Scene Folders: {scannedCount}.\n";
            return scenes;
        }

        private static bool AddScenePath(string scenePath, List<string> scenes, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = scenePath.Replace('\\', '/');
            if (!seen.Add(normalized))
            {
                return false;
            }

            scenes.Add(normalized);
            return true;
        }

        private static string NormalizeAssetFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return string.Empty;
            }

            return folderPath.Trim().Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// 自动化寻找 LevelRegistry 资产并触发关卡路径预烘焙，规避打包后场景路径未解析 Bug #1
        /// </summary>
        public static int BakeLevelRegistryScenePaths()
        {
            string[] registryGUIDs = AssetDatabase.FindAssets("t:LevelRegistry");
            if (registryGUIDs == null || registryGUIDs.Length == 0)
            {
                Debug.LogWarning("[Auto1KeyBuild] No LevelRegistry asset found in project.");
                return 0;
            }

            string path = AssetDatabase.GUIDToAssetPath(registryGUIDs[0]);
            var registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(path);
            if (registry == null) return 0;

            LevelRegistryEditor.SyncRegistry(registry);
            return registry.levels.Count;
        }

        /// <summary>
        /// 基于项目资产组织形式，运行规则化自动寻址编组引擎 (Auto-Grouping)
        /// </summary>
        public static int ExecuteAutoGrouping()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            // 从持久化配置资产中读取所有可配置参数
            var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();

            int count = 0;

            // --- 规则 1：关卡场景自动隔离打包 (Level Scene Pack Separately) ---
            string[] registryGUIDs = AssetDatabase.FindAssets("t:LevelRegistry");
            if (registryGUIDs != null && registryGUIDs.Length > 0)
            {
                string regPath = AssetDatabase.GUIDToAssetPath(registryGUIDs[0]);
                var registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(regPath);
                if (registry != null)
                {
                    foreach (var level in registry.levels)
                    {
                        if (level.sceneAsset != null)
                        {
                            string scenePath = AssetDatabase.GetAssetPath(level.sceneAsset);
                            string sceneGUID = AssetDatabase.AssetPathToGUID(scenePath);
                            
                            string groupName = $"Level_{level.levelName}";
                            AddressableAssetGroup lvlGroup = GetOrCreateGroup(settings, groupName, true);
                            
                            AddressableAssetEntry entry = settings.CreateOrMoveEntry(sceneGUID, lvlGroup);
                            if (entry != null)
                            {
                                entry.address = level.levelName;
                                count++;
                            }
                        }
                    }
                }
            }

            // --- 规则 2：贴花系统资源统一组包 (Decals Pack Together) ---
            AddressableAssetGroup decalGroup = GetOrCreateGroup(settings, buildSettings.decalGroupName, false);
            
            // 通过可配置的搜索查询定位 DecalAtlasConfig 资产
            string[] decalConfigGUIDs = AssetDatabase.FindAssets(buildSettings.decalConfigSearchQuery);
            foreach (var guid in decalConfigGUIDs)
            {
                string configPath = AssetDatabase.GUIDToAssetPath(guid);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, decalGroup);
                if (entry != null)
                {
                    entry.address = Path.GetFileNameWithoutExtension(configPath);
                    count++;
                }
            }

            // 工业级多路径自适应 Shader 扫描：遍历配置的所有 Shader 文件夹
            foreach (var folderPath in buildSettings.shaderFolderPaths)
            {
                string normalizedFolder = NormalizeAssetFolderPath(folderPath);
                if (string.IsNullOrWhiteSpace(normalizedFolder) || !AssetDatabase.IsValidFolder(normalizedFolder))
                {
                    Debug.LogWarning($"<color=#FFB347><b>[Auto1KeyBuild]</b></color> Skipping invalid shader scan path: '{folderPath}'");
                    continue;
                }

                string[] shaderGUIDs = AssetDatabase.FindAssets("t:Shader", new[] { normalizedFolder });
                foreach (var guid in shaderGUIDs)
                {
                    string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, decalGroup);
                    if (entry != null)
                    {
                        entry.address = Path.GetFileNameWithoutExtension(shaderPath);
                        count++;
                    }
                }

                Debug.Log($"<color=#3DB8FF><b>[Auto1KeyBuild]</b></color> Scanned shader folder: '{normalizedFolder}' — found {shaderGUIDs.Length} shader(s).");
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            AssetDatabase.SaveAssets();

            return count;
        }

        /// <summary>
        /// 扫描依赖图谱，分析隐式冗余依赖 (Dependency Graph Analyzer)
        /// </summary>
        public static string AuditDependencies(out List<string> redundantAssets)
        {
            redundantAssets = new List<string>();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return "No Addressables Settings found.";

            // 格式：依赖资源物理路径 -> 引用了该资源的 Addressable 组集合
            var dependencyMap = new Dictionary<string, HashSet<string>>();

            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                if (IsBuiltInAddressableGroup(group)) continue;

                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;
                    if (string.IsNullOrEmpty(entry.AssetPath)) continue;

                    string[] deps = AssetDatabase.GetDependencies(entry.AssetPath, true);
                    foreach (var dep in deps)
                    {
                        if (ShouldSkipDependencyPath(dep)) continue;
                        if (dep == entry.AssetPath) continue;

                        // 仅检查当前依赖是否尚未被主动寻址 (即是隐式依赖)
                        var depGUID = AssetDatabase.AssetPathToGUID(dep);
                        var depEntry = settings.FindAssetEntry(depGUID);
                        if (depEntry == null)
                        {
                            if (!dependencyMap.ContainsKey(dep))
                            {
                                dependencyMap[dep] = new HashSet<string>();
                            }
                            dependencyMap[dep].Add(group.Name);
                        }
                    }
                }
            }

            // 筛选出被 2 个或以上不同分组隐式引用的重复依赖资源
            var reportText = "";
            foreach (var kvp in dependencyMap)
            {
                if (kvp.Value.Count >= 2)
                {
                    redundantAssets.Add(kvp.Key);
                    reportText += $"• [REDUNDANT] {Path.GetFileName(kvp.Key)} referenced by Groups: {string.Join(", ", kvp.Value)}\n";
                }
            }

            return string.IsNullOrEmpty(reportText) ? "Zero implicit redundancy detected." : reportText;
        }

        /// <summary>
        /// 自动化防爆提升：将重复的隐式依赖资源自动升级为公共组 Addressable，避免打包膨胀
        /// </summary>
        public static int AutoPromoteRedundancies(List<string> redundantAssets)
        {
            if (redundantAssets == null || redundantAssets.Count == 0) return 0;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return 0;

            var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
            AddressableAssetGroup sharedGroup = GetOrCreateGroup(settings, buildSettings.sharedGroupName, false);

            int count = 0;
            foreach (var assetPath in redundantAssets)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid)) continue;

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, sharedGroup);
                if (entry != null)
                {
                    entry.address = BuildStableSharedAddress(assetPath, guid);
                    count++;
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            AssetDatabase.SaveAssets();

            return count;
        }

        public static bool ResolveDependencyDuplicates(out string reportText, out int remainingCount)
        {
            reportText = string.Empty;
            remainingCount = 0;

            int normalizedBefore = NormalizeSharedGroupAddresses();
            string beforeReport = AuditDependencies(out var redundantAssets);

            if (redundantAssets.Count == 0)
            {
                reportText = "Zero implicit redundancy detected.\n";
                if (normalizedBefore > 0)
                {
                    reportText += $"Normalized {normalizedBefore} shared address(es).\n";
                }

                return true;
            }

            int promotedCount = AutoPromoteRedundancies(redundantAssets);
            int normalizedAfter = NormalizeSharedGroupAddresses();
            string afterReport = AuditDependencies(out var remainingAssets);
            remainingCount = remainingAssets.Count;

            reportText =
                $"Before:\n{beforeReport}\n" +
                $"Resolved:\n" +
                $"• Promoted {promotedCount} shared implicit asset(s).\n" +
                $"• Normalized {normalizedBefore + normalizedAfter} shared address(es).\n\n" +
                $"After:\n{afterReport}";

            return remainingCount == 0;
        }

        public static bool BuildIncrementalContent(out string reportMessage)
        {
            reportMessage = string.Empty;

            try
            {
                var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
                AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
                if (addressableSettings == null)
                {
                    reportMessage = "✘ [Incremental Content] Addressables settings asset is missing.\n";
                    return false;
                }

                int bakedCount = BakeLevelRegistryScenePaths();
                reportMessage += $"✔ [LevelRegistry] Path Pre-baking completed. Synced {bakedCount} scenes.\n";

                int groupedCount = ExecuteAutoGrouping();
                reportMessage += $"✔ [Auto-Grouping] Rules matching completed. Assigned {groupedCount} assets to Groups.\n";

                int protectedLocalCount = RestoreProtectedLocalAddressables();
                if (protectedLocalCount > 0)
                {
                    reportMessage += $"✔ [Boot Assets] Restored {protectedLocalCount} protected boot asset(s) to the local Addressables group.\n";
                }

                int contentUpdateChangeCount = 0;
                if (buildSettings.localContentUpdateTestMode)
                {
                    int localTestChanges = ApplyLocalIncrementalContentTestProfile(addressableSettings, buildSettings, out string localTestReport);
                    reportMessage += localTestReport;
                    contentUpdateChangeCount += localTestChanges;
                }

                contentUpdateChangeCount += EnsureContentUpdateSupport(out string contentUpdateReport);
                reportMessage += contentUpdateReport;

                if (!TryValidateRemoteLoadPath(addressableSettings, out string remoteLoadPathMessage))
                {
                    reportMessage += $"✘ [Content Update Settings] {remoteLoadPathMessage}\n";
                    return false;
                }

                ResolveDependencyDuplicates(out string dedupeReport, out int remainingDuplicateCount);
                reportMessage += remainingDuplicateCount == 0
                    ? "✔ [Dependency Deduplication] Shared dependency graph is normalized for content update.\n"
                    : $"⚠ [Dependency Deduplication] {remainingDuplicateCount} duplicated implicit asset(s) remain. See dependency audit for details.\n";

                if (!TryResolvePreviousContentStatePath(buildSettings, out string contentStatePath, out string contentStateError))
                {
                    reportMessage += $"✘ [Incremental Content] {contentStateError}\n";
                    return false;
                }

                reportMessage += $"✔ [Content State] Previous state file: {contentStatePath}\n";

                int movedCount = 0;
                if (buildSettings.autoMoveChangedStaticContent)
                {
                    movedCount = MoveChangedStaticContentToUpdateGroup(addressableSettings, contentStatePath, buildSettings.contentUpdateGroupName);
                    if (movedCount > 0)
                    {
                        reportMessage += $"✔ [Content Update Isolation] Moved {movedCount} changed static entry/entries into a remote content update group.\n";
                        contentUpdateChangeCount += EnsureContentUpdateSupport(out string postMoveContentUpdateReport);
                        reportMessage += postMoveContentUpdateReport;
                    }
                    else
                    {
                        reportMessage += "✔ [Content Update Isolation] No changed static entries needed relocation.\n";
                    }
                }

                SafeCloseLayoutWindows();
                Debug.Log("<color=#3DB8FF><b>[Auto1KeyBuild]</b></color> Starting Addressables incremental content update...");
                AddressablesPlayerBuildResult result = ContentUpdateScript.BuildContentUpdate(addressableSettings, contentStatePath);

                if (result == null)
                {
                    reportMessage += "✘ [Incremental Content] Addressables returned no build result.\n";
                    return false;
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    reportMessage += $"✘ [Incremental Content] Failed: {result.Error}\n";
                    return false;
                }

                reportMessage += $"★ [Incremental Content] Succeeded! Duration: {result.Duration:F2}s | Output: {result.OutputPath}\n";
                reportMessage += $"✔ [Content Update Settings] Applied {contentUpdateChangeCount} readiness change(s). Static entries moved: {movedCount}.\n";
                return true;
            }
            catch (Exception ex)
            {
                reportMessage += $"✘ [Incremental Content Fatal] {ex.Message}\n";
                Debug.LogException(ex);
                return false;
            }
        }

        public static bool PrepareLocalIncrementalContentTest(out string reportMessage)
        {
            reportMessage = string.Empty;

            try
            {
                var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
                AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
                if (addressableSettings == null)
                {
                    reportMessage = "✘ [Local Incremental Test] Addressables settings asset is missing.\n";
                    return false;
                }

                buildSettings.enableContentUpdateSupport = true;
                buildSettings.localContentUpdateTestMode = true;
                EditorUtility.SetDirty(buildSettings);

                int changes = ApplyLocalIncrementalContentTestProfile(addressableSettings, buildSettings, out string localReport);
                int contentUpdateChanges = EnsureContentUpdateSupport(out string contentUpdateReport);
                bool valid = TryValidateRemoteLoadPath(addressableSettings, out string remoteLoadPathMessage);

                AssetDatabase.SaveAssets();

                reportMessage += localReport;
                reportMessage += contentUpdateReport;
                reportMessage += valid
                    ? $"✔ [Local Incremental Test] {remoteLoadPathMessage}\n"
                    : $"✘ [Local Incremental Test] {remoteLoadPathMessage}\n";
                reportMessage += $"✔ [Local Incremental Test] Applied {changes + contentUpdateChanges} setting change(s). Build full content once, then build incremental content after changing a non-protected asset.\n";
                return valid;
            }
            catch (Exception ex)
            {
                reportMessage += $"✘ [Local Incremental Test Fatal] {ex.Message}\n";
                Debug.LogException(ex);
                return false;
            }
        }

        public static int EnsureContentUpdateSupport(out string reportText)
        {
            reportText = string.Empty;

            var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
            if (!buildSettings.enableContentUpdateSupport)
            {
                reportText = "⚠ [Content Update Settings] Content update support is disabled in Auto1KeyBuild settings.\n";
                return 0;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                reportText = "✘ [Content Update Settings] Addressables settings asset is missing.\n";
                return 0;
            }

            int changes = 0;
            if (!settings.BuildRemoteCatalog)
            {
                settings.BuildRemoteCatalog = true;
                changes++;
            }

            if (!settings.UniqueBundleIds)
            {
                settings.UniqueBundleIds = true;
                changes++;
            }

            if (settings.CheckForContentUpdateRestrictionsOption != buildSettings.contentUpdateRestrictionMode)
            {
                settings.CheckForContentUpdateRestrictionsOption = buildSettings.contentUpdateRestrictionMode;
                changes++;
            }

            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            EnsureProfileValue(settings, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
            EnsureProfileValue(settings, AddressableAssetSettings.kRemoteLoadPath, string.Empty);
            if (buildSettings.localContentUpdateTestMode)
            {
                changes += ApplyLocalIncrementalContentTestProfile(settings, buildSettings, out _);
            }

            foreach (var group in settings.groups)
            {
                if (group == null || IsBuiltInAddressableGroup(group))
                {
                    continue;
                }

                var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
                if (bundleSchema == null)
                {
                    continue;
                }

                if (buildSettings.useHashBundleNamesForContentUpdates && bundleSchema.BundleNaming != BundledAssetGroupSchema.BundleNamingStyle.AppendHash)
                {
                    bundleSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
                    changes++;
                }

                bool isContentUpdateGroup = group.Name.StartsWith(buildSettings.contentUpdateGroupName, StringComparison.OrdinalIgnoreCase);
                changes += ConfigureBundledGroupPaths(settings, bundleSchema, isContentUpdateGroup);

                var contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
                if (contentUpdateSchema == null)
                {
                    contentUpdateSchema = group.AddSchema<ContentUpdateGroupSchema>();
                    changes++;
                }

                bool shouldBeStatic = !isContentUpdateGroup;
                if (contentUpdateSchema != null && contentUpdateSchema.StaticContent != shouldBeStatic)
                {
                    contentUpdateSchema.StaticContent = shouldBeStatic;
                    changes++;
                }
            }

            if (changes > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
            }

            reportText = changes == 0
                ? "✔ [Content Update Settings] Remote catalog, hashed bundles, and content update schemas are already ready.\n"
                : $"✔ [Content Update Settings] Applied {changes} content update readiness change(s).\n";
            return changes;
        }

        private static int ApplyLocalIncrementalContentTestProfile(AddressableAssetSettings settings, Auto1KeyBuildSettings buildSettings, out string reportText)
        {
            reportText = string.Empty;
            if (settings == null || buildSettings == null || settings.profileSettings == null)
            {
                reportText = "✘ [Local Incremental Test] Addressables profile settings are missing.\n";
                return 0;
            }

            string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            string remoteRootTemplate = string.IsNullOrWhiteSpace(buildSettings.localContentUpdateServerDataPath)
                ? "ServerData/[BuildTarget]"
                : buildSettings.localContentUpdateServerDataPath;
            string remoteBuildPath = remoteRootTemplate.Replace("[BuildTarget]", buildTarget).Replace('\\', '/').Trim('/');
            string absoluteRemoteRoot = Path.IsPathRooted(remoteBuildPath)
                ? remoteBuildPath
                : Path.Combine(ProjectRoot, remoteBuildPath);
            absoluteRemoteRoot = Path.GetFullPath(absoluteRemoteRoot);
            Directory.CreateDirectory(absoluteRemoteRoot);

            string remoteLoadPath = new Uri(absoluteRemoteRoot + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');

            int changes = 0;
            changes += SetProfileValue(settings, AddressableAssetSettings.kRemoteBuildPath, remoteBuildPath);
            changes += SetProfileValue(settings, AddressableAssetSettings.kRemoteLoadPath, remoteLoadPath);
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            if (changes > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.ProfileModified, null, true, true);
            }

            reportText = $"✔ [Local Incremental Test] Remote.BuildPath = {remoteBuildPath}\n" +
                         $"✔ [Local Incremental Test] Remote.LoadPath = {remoteLoadPath}\n";
            return changes;
        }

        private static int ConfigureBundledGroupPaths(AddressableAssetSettings settings, BundledAssetGroupSchema schema, bool useRemotePath)
        {
            if (settings == null || schema == null)
            {
                return 0;
            }

            schema.BuildPath.SetVariableByName(settings, useRemotePath ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, useRemotePath ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);
            return 0;
        }

        private static int SetProfileValue(AddressableAssetSettings settings, string variableName, string value)
        {
            if (settings == null || settings.profileSettings == null)
            {
                return 0;
            }

            string current = settings.profileSettings.GetValueByName(settings.activeProfileId, variableName);
            if (string.Equals(current, value, StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                settings.profileSettings.CreateValue(variableName, value);
            }

            settings.profileSettings.SetValue(settings.activeProfileId, variableName, value);
            return 1;
        }

        private static void EnsureProfileValue(AddressableAssetSettings settings, string variableName, string defaultValue)
        {
            if (settings == null || settings.profileSettings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.profileSettings.GetValueByName(settings.activeProfileId, variableName)))
            {
                settings.profileSettings.CreateValue(variableName, defaultValue);
                settings.profileSettings.SetValue(settings.activeProfileId, variableName, defaultValue);
            }
        }

        private static bool TryValidateRemoteLoadPath(AddressableAssetSettings settings, out string message)
        {
            message = string.Empty;
            if (settings == null || settings.profileSettings == null)
            {
                message = "Addressables profile settings are missing.";
                return false;
            }

            string loadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath);
            if (string.IsNullOrWhiteSpace(loadPath))
            {
                message = "Remote.LoadPath is empty. Configure a CDN, HTTP server, or explicit local file load path before building content updates.";
                return false;
            }

            if (loadPath.Contains("[PrivateIpAddress]") || loadPath.Contains("[HostingServicePort]"))
            {
                message = $"Remote.LoadPath still uses Editor Hosting placeholders: {loadPath}. Built players cannot resolve this and will throw Invalid URI / Invalid port.";
                return false;
            }

            string evaluated = loadPath.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
            if (evaluated.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || evaluated.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(evaluated, UriKind.Absolute, out Uri uri) || string.IsNullOrWhiteSpace(uri.Host))
                {
                    message = $"Remote.LoadPath is not a valid absolute URI: {loadPath}";
                    return false;
                }
            }

            message = $"Remote.LoadPath: {loadPath}";
            return true;
        }

        private static bool TryResolvePreviousContentStatePath(Auto1KeyBuildSettings buildSettings, out string contentStatePath, out string error)
        {
            contentStatePath = string.Empty;
            error = string.Empty;

            string configuredPath = buildSettings != null ? buildSettings.previousContentStatePath : string.Empty;
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string resolved = Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(ProjectRoot, configuredPath);

                resolved = Path.GetFullPath(resolved);
                if (File.Exists(resolved))
                {
                    contentStatePath = resolved;
                    return true;
                }

                error = $"Configured previous content state file does not exist: {resolved}";
                return false;
            }

            string root = Path.Combine(ProjectRoot, "Assets", "AddressableAssetsData");
            if (!Directory.Exists(root))
            {
                error = $"AddressableAssetsData folder does not exist: {root}";
                return false;
            }

            string newestPath = string.Empty;
            DateTime newestWriteTime = DateTime.MinValue;
            foreach (string path in Directory.GetFiles(root, "addressables_content_state.bin", SearchOption.AllDirectories))
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(path);
                if (writeTime > newestWriteTime)
                {
                    newestWriteTime = writeTime;
                    newestPath = path;
                }
            }

            if (string.IsNullOrWhiteSpace(newestPath))
            {
                error = "No addressables_content_state.bin found. Run a full Addressables build first and archive the generated state file.";
                return false;
            }

            contentStatePath = Path.GetFullPath(newestPath);
            return true;
        }

        private static int MoveChangedStaticContentToUpdateGroup(AddressableAssetSettings settings, string contentStatePath, string groupNamePrefix)
        {
            if (settings == null || string.IsNullOrWhiteSpace(contentStatePath) || !File.Exists(contentStatePath))
            {
                return 0;
            }

            var modifiedMap = ContentUpdateScript.GatherModifiedEntriesWithDependencies(settings, contentStatePath);
            if (modifiedMap == null || modifiedMap.Count == 0)
            {
                return 0;
            }

            var entries = new List<AddressableAssetEntry>();
            var seen = new HashSet<string>();
            foreach (var kvp in modifiedMap)
            {
                AddUpdateEntry(entries, seen, kvp.Key, settings);
                if (kvp.Value == null) continue;
                foreach (var dependency in kvp.Value)
                {
                    AddUpdateEntry(entries, seen, dependency, settings);
                }
            }

            if (entries.Count == 0)
            {
                return 0;
            }

            string groupName = string.IsNullOrWhiteSpace(groupNamePrefix) ? "Content_Update" : groupNamePrefix;
            ContentUpdateScript.CreateContentUpdateGroup(settings, entries, $"{groupName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true, true);
            AssetDatabase.SaveAssets();
            return entries.Count;
        }

        private static void AddUpdateEntry(List<AddressableAssetEntry> entries, HashSet<string> seen, AddressableAssetEntry entry, AddressableAssetSettings settings)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.guid))
            {
                return;
            }

            if (IsProtectedLocalAddressableEntry(entry, settings))
            {
                return;
            }

            if (seen.Add(entry.guid))
            {
                entries.Add(entry);
            }
        }

        private static int RestoreProtectedLocalAddressables()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return 0;
            }

            var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
            if (buildSettings.protectedLocalAddressableKeys == null || buildSettings.protectedLocalAddressableKeys.Count == 0)
            {
                return 0;
            }

            AddressableAssetGroup localGroup = settings.DefaultGroup != null
                ? settings.DefaultGroup
                : settings.FindGroup("Default Local Group");

            if (localGroup == null)
            {
                localGroup = settings.CreateGroup("Default Local Group", false, false, true, null);
            }

            int count = 0;
            foreach (string key in buildSettings.protectedLocalAddressableKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string assetPath = key.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    Debug.LogWarning($"<color=#FFB347><b>[Auto1KeyBuild]</b></color> Protected local Addressable asset not found: {key}");
                    continue;
                }

                AddressableAssetEntry currentEntry = settings.FindAssetEntry(guid);
                bool needsMove = currentEntry == null || currentEntry.parentGroup != localGroup || !string.Equals(currentEntry.address, assetPath, StringComparison.Ordinal);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, localGroup);
                if (entry == null)
                {
                    continue;
                }

                entry.address = assetPath;
                if (needsMove)
                {
                    count++;
                }
            }

            if (count > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
                AssetDatabase.SaveAssets();
            }

            return count;
        }

        private static bool IsProtectedLocalAddressableEntry(AddressableAssetEntry entry, AddressableAssetSettings settings)
        {
            if (entry == null)
            {
                return false;
            }

            var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
            if (buildSettings.protectedLocalAddressableKeys == null || buildSettings.protectedLocalAddressableKeys.Count == 0)
            {
                return false;
            }

            string assetPath = entry.AssetPath;
            if (string.IsNullOrWhiteSpace(assetPath) && settings != null && !string.IsNullOrWhiteSpace(entry.guid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
            }

            foreach (string protectedKey in buildSettings.protectedLocalAddressableKeys)
            {
                if (IsSameAddressableKey(protectedKey, entry.address) || IsSameAddressableKey(protectedKey, assetPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameAddressableKey(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(NormalizeAddressableKey(left), NormalizeAddressableKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAddressableKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\\', '/').Trim();
        }

        public static int NormalizeSharedGroupAddresses()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return 0;

            var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
            AddressableAssetGroup sharedGroup = settings.FindGroup(buildSettings.sharedGroupName);
            if (sharedGroup == null) return 0;

            int count = 0;
            foreach (var entry in sharedGroup.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.guid)) continue;

                string assetPath = entry.AssetPath;
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                }

                string stableAddress = BuildStableSharedAddress(assetPath, entry.guid);
                if (!string.Equals(entry.address, stableAddress, StringComparison.Ordinal))
                {
                    entry.address = stableAddress;
                    count++;
                }
            }

            if (count > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
                AssetDatabase.SaveAssets();
            }

            return count;
        }

        private static bool IsBuiltInAddressableGroup(AddressableAssetGroup group)
        {
            if (group == null) return true;
            if (group.GetSchema<PlayerDataGroupSchema>() != null) return true;
            return string.Equals(group.Name, "Built In Data", StringComparison.OrdinalIgnoreCase)
                || string.Equals(group.Name, "Built-In Materials", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipDependencyPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return true;
            string normalized = assetPath.Replace('\\', '/');
            return normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Assets/AddressableAssetsData/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/Resources/");
        }

        private static string BuildStableSharedAddress(string assetPath, string guid)
        {
            string name = string.IsNullOrWhiteSpace(assetPath)
                ? "Asset"
                : Path.GetFileNameWithoutExtension(assetPath);

            string safeName = MakeSafeAddressSegment(name);
            string suffix = string.IsNullOrWhiteSpace(guid)
                ? "noguid"
                : guid.Substring(0, Math.Min(8, guid.Length));

            return $"Shared/{safeName}_{suffix}";
        }

        private static string MakeSafeAddressSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Asset";
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value.Replace('\\', '_').Replace('/', '_').Replace(' ', '_');
        }

        /// <summary>
        /// 极度安全地关闭有文件锁嫌疑的 UI 窗口，解决 Bug #4 (Cannot read BuildLayout)
        /// </summary>
        public static void SafeCloseLayoutWindows()
        {
            string[] targetTypes = new string[]
            {
                "UnityEditor.AddressableAssets.GUI.AddressableAssetsWindow, Unity.Addressables.Editor",
                "UnityEditor.AddressableAssets.Diagnostics.AddressablesReportWindow, Unity.Addressables.Editor",
                "UnityEditor.AddressableAssets.Build.Layout.BuildLayoutWindow, Unity.Addressables.Editor",
                "UnityEditor.AddressableAssets.Diagnostics.AddressablesEventViewer, Unity.Addressables.Editor"
            };

            foreach (var typeName in targetTypes)
            {
                try
                {
                    Type winType = Type.GetType(typeName);
                    if (winType != null)
                    {
                        var windows = Resources.FindObjectsOfTypeAll(winType);
                        foreach (var obj in windows)
                        {
                            var win = obj as EditorWindow;
                            if (win != null)
                            {
                                win.Close();
                                Debug.Log($"[Auto1KeyBuild] Safely closed conflicting window: {typeName}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Auto1KeyBuild] Error closing window {typeName}: {ex.Message}");
                }
            }

            // 强制触发物理资源的解构与垃圾回收，确保没有文件句柄残留
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
        }

        /// <summary>
        /// 快速寻找或创建 Addressables 分组，并自动绑定对应的 Bundle 压缩模式 Schema
        /// </summary>
        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name, bool packSeparately)
        {
            AddressableAssetGroup group = settings.FindGroup(name);
            if (group == null)
            {
                group = settings.CreateGroup(name, false, false, true, null);
            }

            // 自动装配 BundledAssetGroupSchema 保证包粒度符合设计
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                schema = group.AddSchema<BundledAssetGroupSchema>();
            }

            if (schema != null)
            {
                var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();

                // SBP 打包规范：设置压缩格式、打组还是打散
                schema.BundleNaming = buildSettings.enableContentUpdateSupport && buildSettings.useHashBundleNamesForContentUpdates
                    ? BundledAssetGroupSchema.BundleNamingStyle.AppendHash
                    : BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                schema.BundleMode = packSeparately 
                    ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately 
                    : BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                
                // 默认将构建路径设置为本地，由 C# 发布模块统一分流
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            }

            if (!IsBuiltInAddressableGroup(group))
            {
                var contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
                if (contentUpdateSchema == null)
                {
                    contentUpdateSchema = group.AddSchema<ContentUpdateGroupSchema>();
                }

                if (contentUpdateSchema != null)
                {
                    var buildSettings = Auto1KeyBuildSettings.GetOrCreateSettings();
                    contentUpdateSchema.StaticContent = !group.Name.StartsWith(buildSettings.contentUpdateGroupName, StringComparison.OrdinalIgnoreCase);
                }
            }

            return group;
        }
    }
}
