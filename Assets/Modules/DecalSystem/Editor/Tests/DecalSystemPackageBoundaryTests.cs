using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor.Tests
{
    public sealed class DecalSystemPackageBoundaryTests
    {
        private const string ModuleRoot = "Assets/Modules/DecalSystem";
        private const string SamplesRoot = ModuleRoot + "/Samples";
        private static readonly string[] ForbiddenRuntimeTokens =
        {
            "UnityEditor",
            "EditorApplication",
            "AssetDatabase",
            "Handles."
        };

        private static readonly HashSet<string> TextExtensions = new()
        {
            ".asmdef",
            ".asset",
            ".cs",
            ".hlsl",
            ".mat",
            ".md",
            ".prefab",
            ".shader",
            ".uss",
            ".uxml",
            ".yaml"
        };

        [Test]
        public void PackageAssemblies_DoNotReferenceCharacterController()
        {
            var violations = new StringBuilder();
            string absoluteModuleRoot = Path.Combine(Directory.GetCurrentDirectory(), ModuleRoot);
            foreach (string file in Directory.EnumerateFiles(absoluteModuleRoot, "*.asmdef", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("CharacterController.Runtime"))
                    violations.AppendLine($"{ToAssetPath(file)} references CharacterController.Runtime");
            }

            Assert.IsEmpty(
                violations.ToString(),
                "DecalSystem must stay portable; character-controller integrations belong outside the package."
            );
        }

        [Test]
        public void DecalSystemSettings_Instance_IsAvailableWithoutResourcesAsset()
        {
            Assert.IsNotNull(DecalSystemSettings.Instance);
        }

        [Test]
        public void GeneratedHlsl_DoesNotContainVolatileTimestamp()
        {
            string path = ModuleRoot + "/Shaders/DecalDataMini.generated.hlsl";
            string text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("// Date:"), "Generated HLSL must be deterministic for clean git diffs.");
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceEditorApis()
        {
            var violations = new StringBuilder();
            string runtimePath = Path.Combine(ModuleRoot, "Runtime");
            string absoluteRuntimePath = Path.Combine(Directory.GetCurrentDirectory(), runtimePath);

            foreach (string file in Directory.EnumerateFiles(absoluteRuntimePath, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (string token in ForbiddenRuntimeTokens)
                {
                    if (text.Contains(token))
                        violations.AppendLine($"{ToAssetPath(file)} contains forbidden runtime token: {token}");
                }
            }

            Assert.IsEmpty(violations.ToString(), violations.ToString());
        }

        [Test]
        public void CoreAssets_DoNotReferenceSampleAssetGuids()
        {
            var sampleGuids = new HashSet<string>(AssetDatabase.FindAssets(string.Empty, new[] { SamplesRoot }));
            Assert.IsNotEmpty(sampleGuids, "Sample GUID set should not be empty; boundary test cannot run.");

            var violations = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { ModuleRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.StartsWith(SamplesRoot))
                    continue;

                if (!ShouldScanTextFile(path))
                    continue;

                string text = File.ReadAllText(path);
                foreach (string sampleGuid in sampleGuids)
                {
                    if (text.Contains(sampleGuid))
                        violations.AppendLine($"{path} references sample asset GUID {sampleGuid}");
                }
            }

            Assert.IsEmpty(violations.ToString(), violations.ToString());
        }

        private static bool ShouldScanTextFile(string path)
        {
            if (!File.Exists(path))
                return false;

            string extension = Path.GetExtension(path);
            return TextExtensions.Contains(extension);
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", System.StringComparison.Ordinal);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }
    }
}
