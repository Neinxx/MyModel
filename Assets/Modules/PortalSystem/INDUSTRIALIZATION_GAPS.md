# PortalSystem Industrialization Gaps

This document tracks the remaining gaps before PortalSystem can be treated as a small, portable, production-ready module.

## Current Status

- `PortalSystem.Runtime` no longer references `InteractionSystem.Runtime`.
- Interaction support is isolated in `PortalSystem.InteractionSystemIntegration`.
- `PortalHub` uses private serialized fields with migration from the old public field names.
- Inspector discovery is isolated in an editor-only service.
- EditMode test assembly and CI entry point exist.
- Inspector warns when the selected target is empty or missing from discovered registry levels.
- A composition note documents how demo/game code should wire portal events to loading.

## Incomplete Items

- Batchmode tests still need one successful run after the Unity Editor is closed.
- Existing scenes that relied on `PortalHub` directly implementing `IInteractable` need a `PortalInteractionAdapter` added where interaction triggering is required.
- Spawn ID discovery still scans scene YAML for `_hubID`; this is editor-only but should eventually become a typed provider or demo-level integration.
- Inspector supports multiple selected objects only at a basic level; dropdown edits use the current serialized object value.

## Keep Out

- Scene loading orchestration.
- WorldScene compile references.
- Mainboard compile references.
- Character/controller assumptions.
- Automatic scene or registry mutation from the Inspector.

## Next Pass

- Add stale spawn diagnostics that warn when a selected spawn ID is not present in discovery.
- Consider moving InteractionSystem adapter into demo composition if package portability becomes stricter.
