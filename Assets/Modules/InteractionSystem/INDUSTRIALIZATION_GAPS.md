# InteractionSystem Industrialization Gaps

This document tracks the remaining gaps before InteractionSystem can be treated as a small, portable, production-ready base module.

## Current Status

- Runtime has no compile references to other gameplay modules.
- `ProximityInteractor` separates scanning from manual interaction.
- `CurrentInteractable`, `HasTarget`, `TargetChanged`, and buffer-full diagnostics exist.
- `ProximityInteractor` has a lightweight Inspector for target and buffer state.
- EditMode test assembly exists.

## Incomplete Items

- Batchmode tests still need one successful run after the Unity Editor is closed.
- The interactor does not expose target distance or target transform yet.
- There is no input adapter; game/demo code should call `InteractCurrent()` from its own input layer.
- Tie-breaking for equal priority uses first discovered collider order.

## Keep Out

- UI prompts and input bindings.
- Portal, WorldScene, Mainboard, CameraSystem, or CharacterController dependencies.
- Automatic scene or object mutation.
- Module-specific interaction types.

## Next Pass

- Add a tiny UI Toolkit Inspector only if field layout becomes hard to read.
- Add optional target metadata if UI prompts need distance or display labels.
- Add PlayMode coverage for real physics scanning after EditMode tests are stable in batchmode.
