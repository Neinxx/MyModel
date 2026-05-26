using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CharacterPostProcess.Runtime
{
    [DisallowMultipleRendererFeature("Anime Post Process")]
    public sealed class AnimePostProcessFeature : ScriptableRendererFeature
    {
        public string FeatureName => "Anime Post Process";
        public string FeatureDescription =>
            "Builds a character mask, applies Dual Kawase bloom, optional screen-space outline, and optional cinematic background focus for anime-style character presentation.";
        public string FeatureCategory => "Character Rendering";

        [Serializable]
        public class AnimeSettings
        {
            public RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
            public LayerMask characterLayer = -1;
            public bool includeSceneView = false;
            public Shader shader;

            [Header("Dual Kawase Bloom")]
            [Range(1, 6)] public int bloomIterations = 4;
            [Range(0f, 5f)] public float bloomRadius = 1.5f;
            [Range(0f, 2f)] public float bloomThreshold = 1.0f;
            public Color bloomTint = Color.white;

            [Header("Screen Space Outline")]
            public bool enableOutline = true;
            [Range(0f, 1f)] public float outlineIntensity = 1.0f;
            public Color outlineColor = Color.black;
            [Range(0f, 3f)] public float outlineThickness = 1.0f;
            public float depthThreshold = 0.5f;
            public float normalThreshold = 0.5f;

            [Header("Cinematic Focus (Ultimate)")]
            public bool enableCinematic = false;
            [Range(0f, 1f)] public float radialBlurIntensity = 0.0f;
            public Vector2 radialBlurCenter = new Vector2(0.5f, 0.5f);
            [Range(0f, 1f)] public float backgroundDesat = 0.0f;
        }

        public AnimeSettings settings = new AnimeSettings();

        private Material _material;
        private AnimeMaskPass _maskPass;
        private AnimeCompositePass _compositePass;

        public override void Create()
        {
            DisposePasses();

            if (settings.shader == null)
            {
                settings.shader = Shader.Find("Hidden/AnimePostProcess");
            }

            if (settings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _maskPass = new AnimeMaskPass();
            _compositePass = new AnimeCompositePass();

            _maskPass.renderPassEvent = settings.renderEvent;
            _compositePass.renderPassEvent = settings.renderEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            EnsureResources();
            if (_material == null) return;
            if (settings.characterLayer.value == 0) return;
            if (!ShouldRenderCamera(ref renderingData, settings.includeSceneView)) return;

            _maskPass.renderPassEvent = settings.renderEvent;
            _compositePass.renderPassEvent = settings.renderEvent;

            _maskPass.Setup(settings.characterLayer);
            _compositePass.Setup(_material, settings, _maskPass);

            renderer.EnqueuePass(_maskPass);
            renderer.EnqueuePass(_compositePass);
        }

        private static bool ShouldRenderCamera(ref RenderingData renderingData, bool includeSceneView)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.renderType == CameraRenderType.Overlay)
                return false;

            CameraType cameraType = cameraData.camera.cameraType;
            return cameraType == CameraType.Game ||
                (includeSceneView && cameraType == CameraType.SceneView);
        }

        protected override void Dispose(bool disposing)
        {
            DisposePasses();
        }

        private void EnsureResources()
        {
            if (settings.shader == null)
            {
                settings.shader = Shader.Find("Hidden/AnimePostProcess");
            }

            if (_material == null && settings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _maskPass ??= new AnimeMaskPass();
            _compositePass ??= new AnimeCompositePass();
        }

        private void DisposePasses()
        {
            _maskPass?.Dispose();
            _compositePass?.Dispose();
            _maskPass = null;
            _compositePass = null;

            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }
    }

    internal sealed class AnimeMaskPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("AnimePostProcess_Mask");
        private static readonly List<ShaderTagId> ShaderTagIds = new()
        {
            new ShaderTagId("CharacterMask")
        };

        private LayerMask _layerMask;
        private FilteringSettings _filteringSettings;
        private RTHandle _characterMask;
        public RTHandle CharacterMask => _characterMask;

        public void Setup(LayerMask layerMask)
        {
            _layerMask = layerMask;
            _filteringSettings = new FilteringSettings(RenderQueueRange.all, _layerMask);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.ARGB32;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1; // Mask pass does not need MSAA
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            if (!CharacterPostProcessRtUtility.IsValid(desc))
                return;

            RenderingUtils.ReAllocateIfNeeded(ref _characterMask, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_AnimeCharacterMask");
            
            // We use camera depth to only draw visible parts
            ConfigureTarget(_characterMask, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_characterMask == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("AnimePostProcess_Mask");
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(ShaderTagIds[0], ref renderingData, sortFlags);

                var stateBlock = new RenderStateBlock(RenderStateMask.Depth)
                {
                    depthState = new DepthState(false, CompareFunction.LessEqual) // ZWrite Off
                };

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings, ref stateBlock);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CharacterPostProcessRtUtility.Release(ref _characterMask);
        }
    }

    internal sealed class AnimeCompositePass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("AnimePostProcess_Composite");

        private Material _material;
        private AnimePostProcessFeature.AnimeSettings _settings;
        private AnimeMaskPass _maskPass;
        
        private RTHandle _bloomPyramid;
        private RTHandle[] _mipUp;
        private RTHandle[] _mipDown;
        private RTHandle _tempTarget;

        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        
        // Bloom
        private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        private static readonly int BloomThresholdId = Shader.PropertyToID("_BloomThreshold");
        private static readonly int BloomRadiusId = Shader.PropertyToID("_BloomRadius");
        
        // Outline
        private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        private static readonly int OutlineDepthThresholdId = Shader.PropertyToID("_OutlineDepthThreshold");
        private static readonly int OutlineNormalThresholdId = Shader.PropertyToID("_OutlineNormalThreshold");
        
        // Cinematic
        private static readonly int RadialBlurIntensityId = Shader.PropertyToID("_RadialBlurIntensity");
        private static readonly int RadialBlurCenterId = Shader.PropertyToID("_RadialBlurCenter");
        private static readonly int BackgroundDesatId = Shader.PropertyToID("_BackgroundDesat");

        public void Setup(Material material, AnimePostProcessFeature.AnimeSettings settings, AnimeMaskPass maskPass)
        {
            _material = material;
            _settings = settings;
            _maskPass = maskPass;

            int maxIters = 6;
            if (_mipUp == null || _mipUp.Length != maxIters)
            {
                _mipUp = new RTHandle[maxIters];
                _mipDown = new RTHandle[maxIters];
            }
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (_settings != null && _settings.enableOutline && _settings.outlineIntensity > 0f)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            if (!CharacterPostProcessRtUtility.IsValid(desc))
                return;
            
            // Extract / Bloom target
            RenderingUtils.ReAllocateIfNeeded(ref _bloomPyramid, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_AnimeBloomPyramid");

            int iterations = Mathf.Clamp(_settings != null ? _settings.bloomIterations : 0, 1, 6);
            for (int i = 0; i < iterations; i++)
            {
                desc.width = Mathf.Max(1, desc.width >> 1);
                desc.height = Mathf.Max(1, desc.height >> 1);
                RenderingUtils.ReAllocateIfNeeded(ref _mipDown[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"_AnimeBloomDown{i}");
                RenderingUtils.ReAllocateIfNeeded(ref _mipUp[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"_AnimeBloomUp{i}");
            }

            ReleaseUnusedMipTail(iterations);

            var tempDesc = renderingData.cameraData.cameraTargetDescriptor;
            tempDesc.depthBufferBits = 0;
            tempDesc.msaaSamples = 1;
            tempDesc.useMipMap = false;
            tempDesc.autoGenerateMips = false;
            if (CharacterPostProcessRtUtility.IsValid(tempDesc))
            {
                RenderingUtils.ReAllocateIfNeeded(ref _tempTarget, tempDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_AnimeTempTarget");
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _settings == null || _maskPass?.CharacterMask == null || _bloomPyramid == null || _tempTarget == null)
                return;

            var cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (cameraTarget == null || cameraTarget.rt == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("AnimePostProcess_Composite");
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                int iterations = Mathf.Clamp(_settings.bloomIterations, 1, 6);
                
                SetSourceSize(cmd, cameraTarget);
                cmd.SetGlobalTexture(MaskTexId, _maskPass.CharacterMask);
                
                // --- Bloom Params ---
                _material.SetColor(BloomTintId, _settings.bloomTint);
                _material.SetFloat(BloomThresholdId, _settings.bloomThreshold);
                _material.SetFloat(BloomRadiusId, _settings.bloomRadius);
                
                // --- Outline Params ---
                if (_settings.enableOutline)
                {
                    _material.EnableKeyword("_CHARACTER_OUTLINE_ON");
                    _material.SetFloat(OutlineIntensityId, _settings.outlineIntensity);
                    _material.SetColor(OutlineColorId, _settings.outlineColor);
                    _material.SetFloat(OutlineThicknessId, _settings.outlineThickness);
                    _material.SetFloat(OutlineDepthThresholdId, _settings.depthThreshold);
                    _material.SetFloat(OutlineNormalThresholdId, _settings.normalThreshold);
                }
                else
                {
                    _material.DisableKeyword("_CHARACTER_OUTLINE_ON");
                }
                
                // --- Cinematic Params ---
                if (_settings.enableCinematic)
                {
                    _material.EnableKeyword("_CINEMATIC_MODE_ON");
                    _material.SetFloat(RadialBlurIntensityId, _settings.radialBlurIntensity);
                    _material.SetVector(RadialBlurCenterId, _settings.radialBlurCenter);
                    _material.SetFloat(BackgroundDesatId, _settings.backgroundDesat);
                }
                else
                {
                    _material.DisableKeyword("_CINEMATIC_MODE_ON");
                }

                // 1. Extract Masked Color
                SetSourceSize(cmd, cameraTarget);
                Blitter.BlitCameraTexture(cmd, cameraTarget, _bloomPyramid, _material, 0);

                // 2. Dual Kawase Downsample
                RTHandle lastDown = _bloomPyramid;
                for (int i = 0; i < iterations; i++)
                {
                    SetSourceSize(cmd, lastDown);
                    Blitter.BlitCameraTexture(cmd, lastDown, _mipDown[i], _material, 1);
                    lastDown = _mipDown[i];
                }

                // 3. Dual Kawase Upsample
                RTHandle lastUp = _mipDown[iterations - 1];
                for (int i = iterations - 2; i >= 0; i--)
                {
                    SetSourceSize(cmd, lastUp);
                    Blitter.BlitCameraTexture(cmd, lastUp, _mipUp[i], _material, 2);
                    lastUp = _mipUp[i];
                }
                
                // Final upsample to _bloomPyramid
                SetSourceSize(cmd, lastUp);
                Blitter.BlitCameraTexture(cmd, lastUp, _bloomPyramid, _material, 2);
                
                // 4. Final Composite
                cmd.SetGlobalTexture(BloomTexId, _bloomPyramid);
                
                SetSourceSize(cmd, cameraTarget);
                Blitter.BlitCameraTexture(cmd, cameraTarget, _tempTarget, _material, 3);
                Blitter.BlitCameraTexture(cmd, _tempTarget, cameraTarget);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void SetSourceSize(CommandBuffer cmd, RTHandle source)
        {
            if (cmd == null || source == null || source.rt == null)
            {
                return;
            }

            int width = Mathf.Max(1, source.rt.width);
            int height = Mathf.Max(1, source.rt.height);
            cmd.SetGlobalVector(SourceSizeId, new Vector4(width, height, 1f / width, 1f / height));
        }

        private void ReleaseUnusedMipTail(int activeIterations)
        {
            if (_mipUp == null || _mipDown == null)
            {
                return;
            }

            for (int i = Mathf.Clamp(activeIterations, 0, _mipUp.Length); i < _mipUp.Length; i++)
            {
                CharacterPostProcessRtUtility.Release(ref _mipUp[i]);
                CharacterPostProcessRtUtility.Release(ref _mipDown[i]);
            }
        }

        public void Dispose()
        {
            CharacterPostProcessRtUtility.Release(ref _bloomPyramid);
            CharacterPostProcessRtUtility.Release(ref _tempTarget);
            if (_mipUp != null)
            {
                for (int i = 0; i < _mipUp.Length; i++)
                {
                    CharacterPostProcessRtUtility.Release(ref _mipUp[i]);
                    CharacterPostProcessRtUtility.Release(ref _mipDown[i]);
                }
            }
        }
    }
}
