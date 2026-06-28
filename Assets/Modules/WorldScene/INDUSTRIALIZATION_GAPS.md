# WorldScene Industrialization Gaps

This document tracks the remaining gaps before WorldScene can be treated as a small, portable, production-ready module.

## Current Status

- Runtime is editor-free and uses cached scene paths.
- Editor discovery is isolated under `Editor/`.
- Registry sync is deterministic and asks for confirmation from the Inspector.
- Build Settings validation exists and can append missing registered scenes explicitly.
- EditMode test assembly and CI entry point exist.

## Incomplete Items

- Batchmode tests still need one successful run after the Unity Editor is closed.
- The Registry Inspector health row refreshes on open and after sync; it does not live-track every serialized field edit.
- Scene path validation reports missing Build Settings entries, but does not yet report missing scene assets referenced by stale registry entries.
- `LevelRegistry` has no package sample registry or minimal demo setup document yet.
- Runtime loading policy is still simple additive loading; unload transitions and loading-screen integration should stay outside this module unless a concrete demo proves the need.

## Keep Out

- Gameplay module wiring.
- Decal, UI, camera, character, or resource manager dependencies.
- Demo-specific bootstrap logic.
- Automatic destructive cleanup of registry entries, assets, or Build Settings.

## Next Pass

- Run `Tools/CI/run-worldscene-editmode-tests.sh` after closing Unity.
- Add one demo composition note showing how a game/demo should assemble WorldScene with Mainboard.
- Add stale registry diagnostics that only warn; do not auto-delete.
