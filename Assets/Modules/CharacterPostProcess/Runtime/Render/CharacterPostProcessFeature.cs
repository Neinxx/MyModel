using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CharacterPostProcess.Runtime
{
    [DisallowMultipleRendererFeature("Character Post Process")]
    public sealed class CharacterPostProcessFeature : ScriptableRendererFeature
    {
        public string FeatureName => "Character Post Process";
        public string FeatureDescription =>
            "Builds a stable character mask texture, extracts character pixels, applies character-only bloom and glow, then composites the result back into the camera color target.";
        public string FeatureCategory => "Character Rendering";

        [System.Serializable]
        public class Settings
        {
            public enum QualityPreset
            {
                MobileLow,
                MobileBalanced,
                High
            }

            public Shader shader;
            public RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
            public LayerMask characterLayer = 1 << 3;
            public bool includeSceneView = false;
            public QualityPreset qualityPreset = QualityPreset.MobileBalanced;
            public bool useCustomQuality = false;

            [Range(1, 4)] public int downsample = 3;
            [Range(0, 4)] public int blurIterations = 1;
            [Range(0f, 8f)] public float blurRadius = 1.35f;
            [Range(0f, 5f)] public float bloomIntensity = 0.45f;
            [Range(0f, 5f)] public float characterColorBoost = 0.04f;
            [Range(0f, 4f)] public float edgeGlowIntensity = 0.18f;
            [Range(0f, 3f)] public float bloomThreshold = 0.65f;
            public Color bloomTint = new Color(0.95f, 1f, 1f, 1f);

            [Header("Debug Options")]
            public bool debugStencil = false;

            [Header("Screen Space Outline")]
            [Range(0f, 1f)] public float outlineIntensity = 0.0f;
            public Color outlineColor = Color.black;
            [Range(0.5f, 3f)] public float outlineThickness = 1.0f;
            [Range(0f, 5f)] public float outlineDepthThreshold = 1.5f;
            [Range(0f, 1f)] public float outlineNormalThreshold = 0.4f;

            public bool IsActive =>
                bloomIntensity > 0f || characterColorBoost > 0f || edgeGlowIntensity > 0f || debugStencil;

            public RuntimeSettings ResolveRuntimeSettings()
            {
                if (useCustomQuality)
                {
                    return new RuntimeSettings(
                        Mathf.Clamp(downsample, 1, 4),
                        Mathf.Clamp(blurIterations, 0, 4),
                        Mathf.Max(0f, blurRadius),
                        Mathf.Max(0f, bloomIntensity),
                        Mathf.Max(0f, characterColorBoost),
                        Mathf.Max(0f, edgeGlowIntensity),
                        Mathf.Max(0f, bloomThreshold),
                        bloomTint
                    );
                }

                switch (qualityPreset)
                {
                    case QualityPreset.MobileLow:
                        return new RuntimeSettings(4, 1, 1.25f, 0.30f, 0.02f, 0.10f, bloomThreshold, bloomTint);
                    case QualityPreset.High:
                        return new RuntimeSettings(2, 2, 1.25f, 0.70f, 0.08f, 0.35f, bloomThreshold, bloomTint);
                    default:
                        return new RuntimeSettings(3, 1, 1.35f, 0.45f, 0.04f, 0.18f, bloomThreshold, bloomTint);
                }
            }
        }

        public readonly struct RuntimeSettings
        {
            public readonly int downsample;
            public readonly int blurIterations;
            public readonly float blurRadius;
            public readonly float bloomIntensity;
            public readonly float characterColorBoost;
            public readonly float edgeGlowIntensity;
            public readonly float bloomThreshold;
            public readonly Color bloomTint;

            public bool IsActive => bloomIntensity > 0f || characterColorBoost > 0f || edgeGlowIntensity > 0f;

            public RuntimeSettings(
                int downsample,
                int blurIterations,
                float blurRadius,
                float bloomIntensity,
                float characterColorBoost,
                float edgeGlowIntensity,
                float bloomThreshold,
                Color bloomTint
            )
            {
                this.downsample = downsample;
                this.blurIterations = blurIterations;
                this.blurRadius = blurRadius;
                this.bloomIntensity = bloomIntensity;
                this.characterColorBoost = characterColorBoost;
                this.edgeGlowIntensity = edgeGlowIntensity;
                this.bloomThreshold = bloomThreshold;
                this.bloomTint = bloomTint;
            }
        }

        public Settings settings = new Settings();

        private Material _material;
        private CharacterMaskPass _maskPass;
        private CharacterExtractPass _extractPass;
        private CharacterBloomPass _bloomPass;
        private CharacterCompositePass _compositePass;

        public override void Create()
        {
            EnsureResources();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!ShouldRenderCamera(ref renderingData, settings.includeSceneView))
                return;

            if (settings.characterLayer.value == 0)
                return;

            var runtimeSettings = settings.ResolveRuntimeSettings();
            if (!runtimeSettings.IsActive && !settings.debugStencil)
                return;

            EnsureResources();
            if (_material == null || _maskPass == null || _extractPass == null || _bloomPass == null || _compositePass == null)
                return;

            _maskPass.renderPassEvent = settings.renderEvent;
            _extractPass.renderPassEvent = settings.renderEvent;
            _bloomPass.renderPassEvent = settings.renderEvent;
            _compositePass.renderPassEvent = settings.renderEvent;

            _maskPass.Setup(settings.characterLayer);
            _extractPass.Setup(_material, _maskPass);
            _bloomPass.Setup(
                _material,
                _extractPass,
                runtimeSettings.downsample,
                runtimeSettings.blurIterations,
                runtimeSettings.blurRadius,
                runtimeSettings.bloomThreshold,
                runtimeSettings.bloomTint
            );
            _compositePass.Setup(
                _material,
                runtimeSettings.bloomIntensity,
                runtimeSettings.characterColorBoost,
                runtimeSettings.edgeGlowIntensity,
                settings.debugStencil,
                settings.outlineIntensity,
                settings.outlineColor,
                settings.outlineThickness,
                settings.outlineDepthThreshold,
                settings.outlineNormalThreshold
            );

            renderer.EnqueuePass(_maskPass);
            renderer.EnqueuePass(_extractPass);
            renderer.EnqueuePass(_bloomPass);
            renderer.EnqueuePass(_compositePass);
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

        protected override void Dispose(bool disposing)
        {
            _maskPass?.Dispose();
            _extractPass?.Dispose();
            _bloomPass?.Dispose();
            _compositePass?.Dispose();
            _maskPass = null;
            _extractPass = null;
            _bloomPass = null;
            _compositePass = null;

            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }

        private void EnsureResources()
        {
            if (settings.shader == null)
            {
                settings.shader = Shader.Find("Hidden/CharacterPostProcess/Composite");
            }

            if (_material == null && settings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _maskPass ??= new CharacterMaskPass(settings.characterLayer);
            _extractPass ??= new CharacterExtractPass();
            _bloomPass ??= new CharacterBloomPass();
            _compositePass ??= new CharacterCompositePass();
        }
    }

    internal static class CharacterPostProcessIds
    {
        public static readonly int CharacterColorTex = Shader.PropertyToID("_CharacterPostProcess_ColorTex");
        public static readonly int CharacterMaskTex = Shader.PropertyToID("_CharacterPostProcess_MaskTex");
        public static readonly int CharacterBloomTex = Shader.PropertyToID("_CharacterPostProcess_BloomTex");
        public static readonly int BloomThreshold = Shader.PropertyToID("_CharacterBloomThreshold");
        public static readonly int BloomIntensity = Shader.PropertyToID("_CharacterBloomIntensity");
        public static readonly int CharacterColorBoost = Shader.PropertyToID("_CharacterColorBoost");
        public static readonly int EdgeGlowIntensity = Shader.PropertyToID("_CharacterEdgeGlowIntensity");
        public static readonly int BlurRadius = Shader.PropertyToID("_CharacterBlurRadius");
        public static readonly int BlurTexelSize = Shader.PropertyToID("_CharacterBlurTexelSize");
        public static readonly int BloomTint = Shader.PropertyToID("_CharacterBloomTint");
        public static readonly int OutlineIntensity = Shader.PropertyToID("_CharacterOutlineIntensity");
        public static readonly int OutlineColor = Shader.PropertyToID("_CharacterOutlineColor");
        public static readonly int OutlineThickness = Shader.PropertyToID("_CharacterOutlineThickness");
        public static readonly int OutlineDepthThreshold = Shader.PropertyToID("_CharacterOutlineDepthThreshold");
        public static readonly int OutlineNormalThreshold = Shader.PropertyToID("_CharacterOutlineNormalThreshold");
    }

    internal static class CharacterPostProcessRtUtility
    {
        public static bool IsValid(RenderTextureDescriptor desc)
        {
            return desc.width > 0 && desc.height > 0;
        }

        public static void ConfigureColorTarget(ref RenderTextureDescriptor desc, RenderTextureFormat format)
        {
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.colorFormat = format;
        }

        public static void Release(ref RTHandle handle)
        {
            handle?.Release();
            handle = null;
        }
    }

    internal sealed class CharacterMaskPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("CharacterPostProcess_Mask");
        private LayerMask _layerMask;
        private FilteringSettings _filteringSettings;
        private readonly List<ShaderTagId> _shaderTagIds;
        private RTHandle _characterMask;
        private RTHandle _characterMaskResolved;
        private RTHandle _cameraDepth;

        public RTHandle CharacterMask =>
            (_characterMaskResolved != null && _characterMask != null && _characterMask.rt.descriptor.msaaSamples > 1)
                ? _characterMaskResolved
                : _characterMask;

        public CharacterMaskPass(LayerMask layerMask)
        {
            _layerMask = layerMask;
            _filteringSettings = new FilteringSettings(RenderQueueRange.all, _layerMask);
            _shaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("CharacterMask")
            };
        }

        public void Setup(LayerMask layerMask)
        {
            _layerMask = layerMask;
            _filteringSettings = new FilteringSettings(RenderQueueRange.all, _layerMask);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _cameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.colorFormat = RenderTextureFormat.ARGB32;
            if (!CharacterPostProcessRtUtility.IsValid(desc))
                return;

            if (_cameraDepth != null && _cameraDepth.rt != null)
            {
                desc.msaaSamples = _cameraDepth.rt.descriptor.msaaSamples;
                desc.width = _cameraDepth.rt.width;
                desc.height = _cameraDepth.rt.height;

                if (_cameraDepth.useScaling)
                    RenderingUtils.ReAllocateIfNeeded(ref _characterMask, Vector2.one, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_MaskRT");
                else
                    RenderingUtils.ReAllocateIfNeeded(ref _characterMask, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_MaskRT");
            }

            if (_characterMask != null && _characterMask.rt.descriptor.msaaSamples > 1)
            {
                var resolveDesc = desc;
                resolveDesc.msaaSamples = 1;
                if (_characterMask.useScaling)
                    RenderingUtils.ReAllocateIfNeeded(ref _characterMaskResolved, Vector2.one, resolveDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_MaskResolvedRT");
                else
                    RenderingUtils.ReAllocateIfNeeded(ref _characterMaskResolved, resolveDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_MaskResolvedRT");
            }

            ConfigureTarget(_characterMask, _cameraDepth);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_layerMask.value == 0 || _characterMask == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("CharacterPostProcess_Mask");
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                CoreUtils.SetRenderTarget(cmd, _characterMask, _cameraDepth);
                cmd.ClearRenderTarget(false, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(_shaderTagIds[0], ref renderingData, sortFlags);

                var stateBlock = new RenderStateBlock(RenderStateMask.Depth)
                {
                    depthState = new DepthState(false, CompareFunction.LessEqual)
                };

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings, ref stateBlock);

                if (_characterMaskResolved != null && _characterMask.rt.descriptor.msaaSamples > 1)
                {
                    cmd.Blit(_characterMask, _characterMaskResolved);
                }

                cmd.SetGlobalTexture(CharacterPostProcessIds.CharacterMaskTex, CharacterMask);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CharacterPostProcessRtUtility.Release(ref _characterMask);
            CharacterPostProcessRtUtility.Release(ref _characterMaskResolved);
        }
    }

    internal sealed class CharacterExtractPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("CharacterPostProcess_Extract");

        private RTHandle _characterColor;
        private RTHandle _cameraColor;
        private Material _material;
        private CharacterMaskPass _maskPass;

        public RTHandle CharacterColor => _characterColor;

        public void Setup(Material material, CharacterMaskPass maskPass)
        {
            _material = material;
            _maskPass = maskPass;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; 
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.colorFormat = RenderTextureFormat.ARGBHalf; 
            if (!CharacterPostProcessRtUtility.IsValid(desc))
                return;

            if (_cameraColor != null && _cameraColor.rt != null)
            {
                desc.width = _cameraColor.rt.width;
                desc.height = _cameraColor.rt.height;
                if (_cameraColor != null && _cameraColor.useScaling)
                    RenderingUtils.ReAllocateIfNeeded(ref _characterColor, Vector2.one, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_ColorRT");
                else
                    RenderingUtils.ReAllocateIfNeeded(ref _characterColor, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_ColorRT");
            }

            ConfigureTarget(_characterColor);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _cameraColor == null || _characterColor == null || _maskPass?.CharacterMask == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("CharacterPostProcess_Extract");
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                CoreUtils.SetRenderTarget(cmd, _characterColor);
                cmd.ClearRenderTarget(false, true, Color.clear);
                
                cmd.SetGlobalTexture("_BlitTexture", _cameraColor);
                cmd.SetGlobalTexture(CharacterPostProcessIds.CharacterMaskTex, _maskPass.CharacterMask);
                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3);
            }

            cmd.SetGlobalTexture(CharacterPostProcessIds.CharacterColorTex, CharacterColor);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CharacterPostProcessRtUtility.Release(ref _characterColor);
        }
    }

    internal sealed class CharacterBloomPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("CharacterPostProcess_Bloom");
        private RTHandle _ping;
        private RTHandle _pong;
        private CharacterExtractPass _extractPass;
        private Material _material;
        private int _downsample = 2;
        private int _blurIterations = 2;
        private float _blurRadius = 1.25f;
        private float _bloomThreshold = 0.65f;
        private Color _bloomTint = Color.white;

        public void Setup(
            Material material,
            CharacterExtractPass extractPass,
            int downsample,
            int blurIterations,
            float blurRadius,
            float bloomThreshold,
            Color bloomTint
        )
        {
            _material = material;
            _extractPass = extractPass;
            _downsample = downsample;
            _blurIterations = blurIterations;
            _blurRadius = Mathf.Max(0f, blurRadius);
            _bloomThreshold = Mathf.Max(0f, bloomThreshold);
            _bloomTint = bloomTint;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.colorFormat = RenderTextureFormat.ARGBHalf; // Force alpha channel

            desc.width = Mathf.Max(1, desc.width >> _downsample);
            desc.height = Mathf.Max(1, desc.height >> _downsample);
            if (!CharacterPostProcessRtUtility.IsValid(desc))
                return;

            RenderingUtils.ReAllocateIfNeeded(ref _ping, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_BloomPing");
            RenderingUtils.ReAllocateIfNeeded(ref _pong, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_BloomPong");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _ping == null || _pong == null || _extractPass?.CharacterColor == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                _material.SetFloat(CharacterPostProcessIds.BloomThreshold, _bloomThreshold);
                _material.SetFloat(CharacterPostProcessIds.BlurRadius, _blurRadius);
                _material.SetColor(CharacterPostProcessIds.BloomTint, _bloomTint);

                cmd.SetGlobalVector(
                    CharacterPostProcessIds.BlurTexelSize,
                    new Vector4(1f / _ping.rt.width, 1f / _ping.rt.height, 0f, 0f)
                );

                Blitter.BlitCameraTexture(cmd, _extractPass.CharacterColor, _ping, _material, 1);

                for (int i = 0; i < _blurIterations; i++)
                {
                    Blitter.BlitCameraTexture(cmd, _ping, _pong, _material, 2);
                    Blitter.BlitCameraTexture(cmd, _pong, _ping, _material, 3);
                }

                cmd.SetGlobalTexture(CharacterPostProcessIds.CharacterBloomTex, _ping);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CharacterPostProcessRtUtility.Release(ref _ping);
            CharacterPostProcessRtUtility.Release(ref _pong);
        }
    }

    internal sealed class CharacterCompositePass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("CharacterPostProcess_Composite");
        private RTHandle _cameraColor;
        private RTHandle _cameraDepth;
        private RTHandle _tempColor;
        private Material _material;
        private float _bloomIntensity;
        private float _characterColorBoost;
        private float _edgeGlowIntensity;
        private bool _debugStencil;
        
        private float _outlineIntensity;
        private Color _outlineColor;
        private float _outlineThickness;
        private float _outlineDepthThreshold;
        private float _outlineNormalThreshold;

        public void Setup(
            Material material,
            float bloomIntensity,
            float characterColorBoost,
            float edgeGlowIntensity,
            bool debugStencil,
            float outlineIntensity,
            Color outlineColor,
            float outlineThickness,
            float outlineDepthThreshold,
            float outlineNormalThreshold
        )
        {
            _material = material;
            _bloomIntensity = Mathf.Max(0f, bloomIntensity);
            _characterColorBoost = Mathf.Max(0f, characterColorBoost);
            _edgeGlowIntensity = Mathf.Max(0f, edgeGlowIntensity);
            _debugStencil = debugStencil;
            
            _outlineIntensity = Mathf.Max(0f, outlineIntensity);
            _outlineColor = outlineColor;
            _outlineThickness = Mathf.Max(0.1f, outlineThickness);
            _outlineDepthThreshold = Mathf.Max(0.001f, outlineDepthThreshold);
            _outlineNormalThreshold = Mathf.Max(0.001f, outlineNormalThreshold);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (_outlineIntensity > 0f)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            _cameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1; // Kept at 1 so Blit resolves MSAA automatically
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            if (!CharacterPostProcessRtUtility.IsValid(desc))
                return;
            
            // _tempColor must MATCH _cameraColor EXACTLY in dimension and scaling mode
            if (_cameraColor != null && _cameraColor.rt != null)
            {
                var colorDesc = desc;
                colorDesc.width = _cameraColor.rt.width;
                colorDesc.height = _cameraColor.rt.height;

                if (_cameraColor.useScaling)
                    RenderingUtils.ReAllocateIfNeeded(ref _tempColor, Vector2.one, colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_CompositeTemp");
                else
                    RenderingUtils.ReAllocateIfNeeded(ref _tempColor, colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CharacterPostProcess_CompositeTemp");
            }

            // Always configure target to the camera's native color and depth targets.
            // Since they are from the same camera, their dimensions are guaranteed to match!
            ConfigureTarget(_cameraColor, _cameraDepth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _cameraColor == null || _tempColor == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                if (_debugStencil)
                {
                    // 1. Copy the original, unmodified color texture to _tempColor
                    Blitter.BlitCameraTexture(cmd, _cameraColor, _tempColor);

                    // 2. Render the dimmed grayscale background onto _cameraColor
                    cmd.SetGlobalTexture("_BlitTexture", _tempColor);
                    CoreUtils.SetRenderTarget(cmd, _cameraColor, _cameraDepth);
                    cmd.DrawProcedural(Matrix4x4.identity, _material, 5, MeshTopology.Triangles, 3);

                    // 3. Render the neon cyan highlights onto _cameraColor using the stable character mask.
                    cmd.SetGlobalTexture("_BlitTexture", _tempColor);
                    CoreUtils.SetRenderTarget(cmd, _cameraColor, _cameraDepth);
                    cmd.DrawProcedural(Matrix4x4.identity, _material, 6, MeshTopology.Triangles, 3);
                }
                else
                {
                    _material.SetFloat(CharacterPostProcessIds.BloomIntensity, _bloomIntensity);
                    _material.SetFloat(CharacterPostProcessIds.CharacterColorBoost, _characterColorBoost);
                    _material.SetFloat(CharacterPostProcessIds.EdgeGlowIntensity, _edgeGlowIntensity);
                    
                    if (_outlineIntensity > 0f)
                    {
                        _material.SetFloat(CharacterPostProcessIds.OutlineIntensity, _outlineIntensity);
                        _material.SetColor(CharacterPostProcessIds.OutlineColor, _outlineColor);
                        _material.SetFloat(CharacterPostProcessIds.OutlineThickness, _outlineThickness);
                        _material.SetFloat(CharacterPostProcessIds.OutlineDepthThreshold, _outlineDepthThreshold);
                        _material.SetFloat(CharacterPostProcessIds.OutlineNormalThreshold, _outlineNormalThreshold);
                        _material.EnableKeyword("_CHARACTER_OUTLINE_ON");
                    }
                    else
                    {
                        _material.DisableKeyword("_CHARACTER_OUTLINE_ON");
                    }

                    Blitter.BlitCameraTexture(cmd, _cameraColor, _tempColor, _material, 4);
                    Blitter.BlitCameraTexture(cmd, _tempColor, _cameraColor);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CharacterPostProcessRtUtility.Release(ref _tempColor);
        }
    }
}
