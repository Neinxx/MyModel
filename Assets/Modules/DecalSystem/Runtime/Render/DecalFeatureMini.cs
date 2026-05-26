using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DecalMini
{
    /// <summary>
    /// 贴花渲染特征：管理 GPU 缓冲区生命周期，提交 Stencil 排除通道与贴花投影通道
    /// </summary>
    [DisallowMultipleRendererFeature("Decal Feature Mini")]
    public class DecalFeatureMini : ScriptableRendererFeature
    {
        public string FeatureName => "Decal Feature Mini";
        public string FeatureDescription =>
            "Renders runtime decals from the decal atlas, including optional stencil exclusion for surfaces that should not receive projected decals.";
        public string FeatureCategory => "World Rendering";

        // ========================================================================
        // 1. 序列化配置 (Inspector)
        // ========================================================================

        [Header("Assets")]
        public DecalAtlasConfigMini atlasConfig;
        public Shader decalShader;
        public Shader stencilShader;
        public RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingOpaques;

        [Header("Exclusion Settings (Blacklist)")]
        [Tooltip("勾选此掩码的物体将不再接收贴花")]
        [DecalRenderingLayerMask]
        public uint exclusionLayerMask = 0;

        [Header("Global Constraints")]
        [Range(10, 2000)]
        public int maxDecals = 500;
        public LayerMask decalLayer = -1;

        [Range(10, 500)]
        public float maxDrawDistance = 100f;

        [Range(0, 50)]
        public float fadeRange = 10f;

        public bool includeSceneView = true;

        // ========================================================================
        // 2. 内部运行时状态 & Shader IDs
        // ========================================================================
        
        private static class ShaderIDs
        {
            public static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
            public static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        }

        private const int EXCLUSION_STENCIL_REF = 255;
        private DecalPassMini _pass;
        private ExcludeStencilPassMini _exclusionPass;
        private Material _stencilMaterial;
        private Material _material;
        private ComputeBuffer _decalBuffer;
        private DecalDataMini[] _dataArray;

        // ========================================================================
        // 3. 特征生命周期 (Feature Lifecycle)
        // ========================================================================

        public override void Create() { }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData
        )
        {
            if (!ShouldRenderCamera(ref renderingData, includeSceneView))
                return;

            if (atlasConfig == null || atlasConfig.bakedArray == null)
                return;

            EnsureResources();
            if (_pass == null)
                return;

            DecalSystemMini.SetAtlasConfig(atlasConfig);

            // 1. 排除标记通道
            if (exclusionLayerMask != 0 && _exclusionPass != null)
            {
                _exclusionPass.renderPassEvent = renderEvent;
                _exclusionPass.SetMask(exclusionLayerMask);
                renderer.EnqueuePass(_exclusionPass);
            }

            // 2. 贴花主通道
            int totalCount = DecalSystemMini.TotalCount;
            if (totalCount <= 0)
                return;

            UpdateBuffer(totalCount);

            _pass.renderPassEvent = renderEvent;
            _pass.SetParams(maxDrawDistance, fadeRange, decalLayer, _decalBuffer, _dataArray);
            renderer.EnqueuePass(_pass);
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
            _pass?.Dispose();
            _pass = null;

            if (_material != null)
                CoreUtils.Destroy(_material);
            _material = null;

            if (_stencilMaterial != null)
                CoreUtils.Destroy(_stencilMaterial);
            _stencilMaterial = null;

            _decalBuffer?.Release();
            _decalBuffer = null;
        }

        // ========================================================================
        // 4. 资源自愈逻辑 (Auto-Healing)
        // ========================================================================

        private void EnsureResources()
        {
            if (_material == null && decalShader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(decalShader);
                if (_material != null)
                {
                    _material.SetInt(ShaderIDs.StencilRef, EXCLUSION_STENCIL_REF);
                    _material.SetFloat(ShaderIDs.StencilComp, (float)CompareFunction.NotEqual);
                }
            }

            if (_stencilMaterial == null && stencilShader != null)
            {
                _stencilMaterial = CoreUtils.CreateEngineMaterial(stencilShader);
            }

            if (_pass == null && _material != null)
                _pass = new DecalPassMini(_material);

            if (_exclusionPass == null && _stencilMaterial != null)
                _exclusionPass = new ExcludeStencilPassMini(_stencilMaterial, EXCLUSION_STENCIL_REF);
        }

        private void UpdateBuffer(int count)
        {
            // 商业级 0-GC 优化：使用预分配的固定最大容量
            int requiredSize = Mathf.Max(maxDecals, 64);

            if (_decalBuffer == null || _decalBuffer.count < requiredSize)
            {
                _decalBuffer?.Release();
                _decalBuffer = new ComputeBuffer(requiredSize, DecalDataMini.Stride);
                _dataArray = new DecalDataMini[requiredSize];
            }
        }
    }

    // ========================================================================
    // 5. STENCIL EXCLUSION PASS
    // ========================================================================

    public class ExcludeStencilPassMini : ScriptableRenderPass
    {
        private uint _mask;
        private readonly Material _stencilMaterial;
        private readonly int _stencilRef;
        private static readonly ProfilingSampler _profilingSampler = new("Decal_Exclusion_Pass");
        private static readonly List<ShaderTagId> _shaderTagIds = new()
        {
            new("UniversalForward"),
            new("UniversalForwardOnly"),
            new("LightweightForward"),
            new("SRPDefaultUnlit"),
        };

        public ExcludeStencilPassMini(Material mat, int stencilRef)
        {
            _stencilMaterial = mat;
            _stencilRef = stencilRef;
        }

        public void SetMask(uint mask) => _mask = mask;

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData
        )
        {
            if (_mask == 0 || _stencilMaterial == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.SetGlobalInt("_StencilRef", _stencilRef);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var filteringSettings = new FilteringSettings(RenderQueueRange.opaque)
                {
                    renderingLayerMask = _mask,
                };
                var drawSettings = CreateDrawingSettings(
                    _shaderTagIds,
                    ref renderingData,
                    SortingCriteria.CommonOpaque
                );
                drawSettings.overrideMaterial = _stencilMaterial;

                context.DrawRenderers(
                    renderingData.cullResults,
                    ref drawSettings,
                    ref filteringSettings
                );
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
