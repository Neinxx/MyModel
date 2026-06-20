# CI Tools

Run DecalSystem EditMode tests locally:

```bash
Tools/CI/run-decal-editmode-tests.sh
```

If Unity is not installed in the default Unity Hub location, set:

```bash
UNITY_EXECUTABLE="/path/to/Unity.app/Contents/MacOS/Unity" Tools/CI/run-decal-editmode-tests.sh
```

The script runs the `DecalSystem.Editor.Tests` assembly and writes:

- `Logs/TestResults/decal-editmode-results.xml`
- `Logs/TestResults/decal-editmode.log`

Use the same command in CI after restoring the Unity license and project cache.
