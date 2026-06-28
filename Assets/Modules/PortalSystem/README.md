# PortalSystem

PortalSystem is a small runtime module for declaring scene transition points.

## Runtime

- Assembly: `PortalSystem.Runtime`
- Root namespace: `PortalSystem.Runtime`
- Core runtime dependencies: UnityEngine only.
- `PortalHub` stores a target level name and spawn point ID, then raises events when triggered.
- Runtime core does not depend on InteractionSystem, WorldScene, Mainboard, or demo code.

Basic use:

```csharp
portalHub.ConfigureDestination("LevelA", "Start");
portalHub.OnPortalTriggeredAction += HandlePortal;
```

## Optional Integrations

- `PortalSystem.InteractionSystemIntegration` adapts `PortalHub` to `InteractionSystem.Runtime`.
- Add `PortalInteractionAdapter` beside a `PortalHub` when the InteractionSystem should trigger the portal.
- Demo-specific scene loading should listen to `PortalHub` events from demo/game composition code.
- Composition guidance lives in `COMPOSITION.md`.

## Editor

- Assembly: `PortalSystem.Editor`
- Inspector UI uses UI Toolkit assets under `Editor/Styles`.
- Destination discovery is isolated in `PortalDestinationDiscovery`.
- Discovery reads `LevelRegistry` assets by serialized shape and scans scene YAML for spawn IDs; this is editor-only and intentionally outside runtime.

## Tests

- Test assembly: `PortalSystem.Editor.Tests`
- Command line:

```bash
Tools/CI/run-portalsystem-editmode-tests.sh
```

## Rules

- Keep `PortalSystem.Runtime` free of optional gameplay module references.
- Keep scene loading outside PortalSystem.
- Keep InteractionSystem support as an adapter, not a core dependency.
- Do not auto-delete or rewrite scene/registry data from the Inspector.

Remaining gaps are tracked in `INDUSTRIALIZATION_GAPS.md`.
