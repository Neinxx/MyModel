# DecalSystem

DecalSystem is a lightweight URP decal module for mobile-focused Unity projects. It keeps runtime rendering code, editor tools, samples, and diagnostics separated so the module can be tested and maintained without hidden editor dependencies.

## Runtime

- Assembly: `DecalSystem.Runtime`
- Root namespace: `DecalMini`
- Runtime code must not reference `UnityEditor`, `AssetDatabase`, `EditorApplication`, or `Handles`.
- Core data flow:
  - `DecalSystemMini` owns registration, spatial indexing, runtime decal pools, and render data filling.
  - `DecalPassMini` and `DecalFeatureMini` bridge the data into URP.
  - `DecalPoolMini` and `DecalParticlePoolMini` handle short-lived projector and particle reuse.

## Editor

- Assembly: `DecalSystem.Editor`
- Tools live under `Editor/`.
- Editor-only responsibilities include diagnostics windows, scene debug drawing, baking tools, atlas generation, inspector UI, and settings discovery.
- Risky editor actions should ask for confirmation and prefer reversible defaults.

## Tests

- Test assembly: `DecalSystem.Editor.Tests`
- Menu diagnostics: `Tools/Decal System/Run Diagnostics`
- Package boundary tests prevent runtime editor API references and core-to-sample asset references.
- Command line:

```bash
Tools/CI/run-decal-editmode-tests.sh
```

The command writes:

- `Logs/TestResults/decal-editmode-results.xml`
- `Logs/TestResults/decal-editmode.log`

## CI

The workflow template is `.github/workflows/decal-system-tests.yml`.

It expects a self-hosted runner with Unity installed. If Unity is not in the default Unity Hub path, set `UNITY_EXECUTABLE` on the runner.

## Industrialization Rules

- Keep runtime and editor assembly boundaries clean.
- Treat assets and `.meta` files as pairs.
- Keep Project/Scene drawing allocation-light.
- Add diagnostics for behavior that may fail silently.
- Keep samples useful, but avoid coupling core runtime behavior to sample assets.
