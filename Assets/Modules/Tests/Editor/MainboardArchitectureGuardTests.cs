using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class MainboardArchitectureGuardTests
{
    private static readonly string[] ArchitectureSourceRoots =
    {
        "Assets/Modules",
        "Assets/Demo",
    };

    private static readonly string[] MainboardSourceRoots =
    {
        "Assets/Modules/Mainboard/Runtime",
        "Assets/Modules/Mainboard/Integrations",
    };

    private static readonly string[] RetiredWorldSceneFragments =
    {
        "World" + "Orchestrator",
        "World" + "Orchestrator" + "Module",
        "World" + "Orc",
        "Orchestrator" + "State",
    };

    private static readonly string[] ForbiddenMainboardFragments =
    {
        "ModularDemo.Runtime.",
        "Type.GetType(",
        "GameObject.Find(",
        "ResourceHandle<",
        "UIManager.Instance",
        "GameMainboard.Instance",
    };

    [Test]
    public void Mainboard_DoesNotDependOnDemoReflectionOrSceneSearchFallbacks()
    {
        var violations = new List<string>();
        foreach (var root in MainboardSourceRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var fragment in ForbiddenMainboardFragments)
                {
                    if (text.Contains(fragment))
                        violations.Add($"{file}: contains '{fragment}'");
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Mainboard architecture guard failed:\n" + string.Join("\n", violations)
        );
    }

    [Test]
    public void Project_DoesNotUseRetiredWorldSceneNames()
    {
        var violations = new List<string>();
        foreach (var root in ArchitectureSourceRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == nameof(MainboardArchitectureGuardTests) + ".cs")
                    continue;

                if (!IsTextAsset(file))
                    continue;

                var text = File.ReadAllText(file);
                foreach (var fragment in RetiredWorldSceneFragments)
                {
                    if (text.Contains(fragment))
                        violations.Add($"{file}: contains '{fragment}'");
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Retired WorldScene naming guard failed:\n" + string.Join("\n", violations)
        );
    }

    private static bool IsTextAsset(string file)
    {
        var extension = Path.GetExtension(file);
        return extension == ".cs"
            || extension == ".asmdef"
            || extension == ".asset"
            || extension == ".prefab"
            || extension == ".unity";
    }
}
