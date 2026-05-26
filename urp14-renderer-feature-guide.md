# URP 14 RendererFeature Guide for Codex

This project uses Unity 2022.3 and URP 14. Use this guide whenever adding or modifying `ScriptableRendererFeature` / `ScriptableRenderPass` code.

## Core Rule

`ScriptableRendererFeature` owns configuration and long-lived resources. `ScriptableRenderPass` owns per-camera targets, temporary RTs, and rendering commands.

Do not read camera targets in `AddRenderPasses`.

```csharp
// Wrong in URP 14
pass.Setup(renderer.cameraColorTargetHandle);
```

Read camera targets inside the pass lifecycle instead.

```csharp
public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
{
    _cameraColor = data.cameraData.renderer.cameraColorTargetHandle;
}
```

## Feature Responsibilities

Use `ScriptableRendererFeature` for:

- Serialized settings shown in the URP Renderer asset.
- Creating long-lived materials with `CoreUtils.CreateEngineMaterial`.
- Creating pass instances.
- Filtering cameras before enqueueing passes.
- Passing simple settings to passes.
- Enqueueing passes.
- Releasing long-lived resources in `Dispose`.

Avoid in `ScriptableRendererFeature`:

- Accessing `renderer.cameraColorTargetHandle`.
- Allocating per-camera RTs.
- Reading camera descriptor.
- Running `CommandBuffer` work.
- Doing per-frame heavy allocations.

## Pass Responsibilities

Use `ScriptableRenderPass` for:

- Reading `renderingData.cameraData.renderer.cameraColorTargetHandle`.
- Reading `renderingData.cameraData.cameraTargetDescriptor`.
- Allocating RTHandles via `RenderingUtils.ReAllocateIfNeeded`.
- Calling `ConfigureTarget`, `ConfigureClear`, and `ConfigureInput`.
- Drawing renderers.
- Blitting with `Blitter`.
- Setting global textures for later passes.
- Releasing RTHandles in `Dispose`.

## Camera Filtering

Most custom effects should skip overlay cameras.

```csharp
var cameraData = renderingData.cameraData;

if (cameraData.renderType == CameraRenderType.Overlay)
    return;

if (cameraData.camera.cameraType != CameraType.Game &&
    cameraData.camera.cameraType != CameraType.SceneView)
    return;
```

Scene View support should usually be a setting.

## RTHandle Allocation

Use `RTHandle`, not old `RenderTargetHandle`.

```csharp
private RTHandle _temp;

public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
{
    var desc = data.cameraData.cameraTargetDescriptor;
    desc.depthBufferBits = 0;
    desc.msaaSamples = 1;

    RenderingUtils.ReAllocateIfNeeded(
        ref _temp,
        desc,
        FilterMode.Bilinear,
        TextureWrapMode.Clamp,
        name: "_MyTemp"
    );
}
```

Release it:

```csharp
public void Dispose()
{
    _temp?.Release();
}
```

## Blitting

Use `Blitter` with RTHandles.

```csharp
Blitter.BlitCameraTexture(cmd, source, destination, material, passIndex);
Blitter.BlitCameraTexture(cmd, destination, source);
```

Avoid old `cmd.Blit` unless there is a specific compatibility reason.

## RenderPassEvent

Choose the event deliberately.

Common choices:

- `BeforeRenderingOpaques`
- `AfterRenderingOpaques`
- `BeforeRenderingTransparents`
- `AfterRenderingTransparents`
- `BeforeRenderingPostProcessing`
- `AfterRenderingPostProcessing`

For character-specific post processing, prefer:

```csharp
RenderPassEvent.AfterRenderingTransparents
```

If the effect should be affected by final color grading, use:

```csharp
RenderPassEvent.BeforeRenderingPostProcessing
```

## ConfigureInput

Declare required pipeline textures.

```csharp
ConfigureInput(ScriptableRenderPassInput.Depth);
ConfigureInput(ScriptableRenderPassInput.Normal);
ConfigureInput(ScriptableRenderPassInput.Color);
```

Do not assume `_CameraDepthTexture`, normals, or opaque color are available unless requested.

## DrawRenderers Pattern

Use explicit shader tags and filtering.

```csharp
private static readonly List<ShaderTagId> ShaderTagIds = new()
{
    new ShaderTagId("UniversalForward"),
    new ShaderTagId("UniversalForwardOnly"),
    new ShaderTagId("LightweightForward"),
    new ShaderTagId("SRPDefaultUnlit")
};

var drawSettings = CreateDrawingSettings(
    ShaderTagIds,
    ref renderingData,
    SortingCriteria.CommonTransparent
);

var filtering = new FilteringSettings(RenderQueueRange.all, layerMask);
context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filtering);
```

## Shader Include Rule

If a fullscreen shader uses `Blit.hlsl`, include URP Core first so `TEXTURE2D_X` and related macros exist.

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
```

Use `SAMPLE_TEXTURE2D_X` for RTHandle / XR-safe sampling.

## Minimal Feature Template

```csharp
public sealed class MyFeature : ScriptableRendererFeature
{
    public Shader shader;
    private Material _material;
    private MyPass _pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find("Hidden/MyFeature");

        if (shader != null)
            _material = CoreUtils.CreateEngineMaterial(shader);

        _pass = new MyPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_material == null)
            return;

        if (data.cameraData.renderType == CameraRenderType.Overlay)
            return;

        _pass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        _pass.Setup(_material);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();

        if (_material != null)
            CoreUtils.Destroy(_material);
    }
}
```

## Minimal Pass Template

```csharp
public sealed class MyPass : ScriptableRenderPass
{
    private Material _material;
    private RTHandle _cameraColor;
    private RTHandle _temp;

    public void Setup(Material material)
    {
        _material = material;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
    {
        _cameraColor = data.cameraData.renderer.cameraColorTargetHandle;

        var desc = data.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;

        RenderingUtils.ReAllocateIfNeeded(
            ref _temp,
            desc,
            FilterMode.Bilinear,
            TextureWrapMode.Clamp,
            name: "_MyFeatureTemp"
        );
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData data)
    {
        if (_material == null || _cameraColor == null || _temp == null)
            return;

        var cmd = CommandBufferPool.Get("MyFeature");

        Blitter.BlitCameraTexture(cmd, _cameraColor, _temp, _material, 0);
        Blitter.BlitCameraTexture(cmd, _temp, _cameraColor);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        _temp?.Release();
    }
}
```

## Project-Specific Notes

- Current URP asset: `Assets/Settings/URP-HighFidelity.asset`.
- Current renderer asset: `Assets/Settings/URP-HighFidelity-Renderer.asset`.
- Existing RendererFeature reference: `Assets/Modules/DecalSystem/Runtime/Render/DecalFeatureMini.cs`.
- Character layer is Layer 3, named `Character`.
- Character post process module lives at `Assets/Modules/CharacterPostProcess`.

## Common Errors

### cameraColorTargetHandle scope error

Cause: Reading `renderer.cameraColorTargetHandle` in `AddRenderPasses`.

Fix: Read it in `ScriptableRenderPass.OnCameraSetup`.

### TEXTURE2D_X unrecognized

Cause: Including `Blit.hlsl` without URP `Core.hlsl`.

Fix:

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
```

### Effect appears on UI

Cause: Overlay cameras are not filtered.

Fix:

```csharp
if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
    return;
```
