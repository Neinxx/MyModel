# PortalSystem Composition

PortalSystem should stay small: a portal declares intent, while the game or demo decides how to load scenes.

## Recommended Flow

1. Place `PortalHub` in the scene.
2. Set the target level and spawn ID in the Inspector.
3. Add `PortalInteractionAdapter` only when InteractionSystem should trigger the portal.
4. In demo/game composition code, subscribe to `PortalHub.OnPortalTriggeredAction`.
5. Forward the request to WorldScene, Mainboard, or another scene orchestration service.

Example:

```csharp
private void OnEnable()
{
    _portalHub.OnPortalTriggeredAction += HandlePortalTriggered;
}

private void OnDisable()
{
    _portalHub.OnPortalTriggeredAction -= HandlePortalTriggered;
}

private void HandlePortalTriggered(string levelName, string spawnPointId)
{
    // Demo or game layer decides what loading service to call.
}
```

## Ownership

- `PortalSystem.Runtime` owns portal destination data and trigger events.
- `PortalSystem.InteractionSystemIntegration` owns InteractionSystem adaptation.
- Demo/game code owns scene loading, fade screens, save gates, analytics, and player repositioning.

## Anti-Patterns

- Do not make `PortalHub` call WorldScene directly.
- Do not put Mainboard references into PortalSystem.
- Do not make PortalSystem mutate registries, build settings, or scene assets.
- Do not hide loading policy inside the Inspector.
