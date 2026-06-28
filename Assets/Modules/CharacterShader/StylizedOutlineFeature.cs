using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Stylized Outline")]
public sealed class StylizedOutlineFeature : ScriptableRendererFeature
{
    public string FeatureName => "Stylized Outline";
    public string FeatureDescription =>
        "Draws meshes that expose the StylizedOutline shader pass, producing a front-face culled silhouette outline for character materials.";
    public string FeatureCategory => "Character Rendering";

    [System.Serializable]
    public class OutlineSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public LayerMask layerMask = -1;
        public bool includeSceneView = false;
    }

    public OutlineSettings settings = new OutlineSettings();
    private StylizedOutlinePass outlinePass;

    public override void Create()
    {
        outlinePass = new StylizedOutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldRenderCamera(ref renderingData, settings.includeSceneView))
            return;

        if (settings.layerMask.value == 0)
            return;

        outlinePass ??= new StylizedOutlinePass();
        outlinePass.Setup(settings.layerMask);
        outlinePass.renderPassEvent = settings.renderPassEvent;
        renderer.EnqueuePass(outlinePass);
    }

    private static bool ShouldRenderCamera(ref RenderingData renderingData, bool includeSceneView)
    {
        var cameraData = renderingData.cameraData;
        if (cameraData.renderType == CameraRenderType.Overlay)
            return false;

        var cameraType = cameraData.camera.cameraType;
        return cameraType == CameraType.Game ||
            (includeSceneView && cameraType == CameraType.SceneView);
    }

    private sealed class StylizedOutlinePass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("Draw Stylized Outline");
        private static readonly ShaderTagId ShaderTagId = new("StylizedOutline");

        private FilteringSettings filteringSettings;

        public void Setup(LayerMask layerMask)
        {
            // Render opaque queue only. If you have transparent characters, you may need a separate pass for transparent outlines.
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(ShaderTagId, ref renderingData, sortFlags);

                // The actual rendering state (Cull Front, ZWrite, Blend) is handled by the Shader Pass itself.
                // This pass merely filters objects with the "StylizedOutline" LightMode tag and draws them.
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
