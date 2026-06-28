# InteractionSystem

InteractionSystem is a small runtime module for discovering and triggering nearby interactable objects.

## Runtime

- Assembly: `InteractionSystem.Runtime`
- Root namespace: `InteractionSystem.Runtime`
- Runtime dependencies: UnityEngine only.
- `IInteractable` is the stable contract for anything that can be interacted with.
- `ProximityInteractor` scans nearby colliders, tracks the current best target, and can interact automatically or manually.

Basic use:

```csharp
var target = interactor.Scan();
if (target != null)
{
    interactor.InteractCurrent();
}
```

For button-driven interaction, disable `Auto Trigger` and call `Scan()` for target prompts, then `InteractCurrent()` when input is confirmed.

## Design Rules

- Keep this module free of Portal, WorldScene, UI, camera, character, and mainboard references.
- Gameplay modules should adapt to `IInteractable`; InteractionSystem should not know their concrete types.
- Prefer explicit adapters when another module needs to become interactable.
- Keep detection allocation-free during scanning.

## Tests

- Test assembly: `InteractionSystem.Editor.Tests`
- Command line:

```bash
Tools/CI/run-interactionsystem-editmode-tests.sh
```

Remaining gaps are tracked in `INDUSTRIALIZATION_GAPS.md`.

## Editor

- `ProximityInteractor` has a small IMGUI Inspector for runtime target state.
- The Inspector can call `Scan()` and `InteractCurrent()` in Play Mode for quick debugging.
