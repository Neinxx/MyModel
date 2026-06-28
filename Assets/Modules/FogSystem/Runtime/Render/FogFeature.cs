using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FogSystem.Runtime
{
    public sealed class FogFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class FogSettings
        {
            public Shader shader;
            public Gradient fogGradient = new();

            [Header("Distance Fog")]
            public float distanceDensity = 0.015f;
            public float fogStartDistance = 0f;
            public float fogEndDistance = 150f;
            public bool useExponentialDistance = false;

            [Header("Height Fog")]
            public float heightBase = 0f;
            [Tooltip("Height attenuation factor (k)")]
            public float heightDensity = 0.05f;
            [Tooltip("Height base density (rho_0)")]
            public float heightFogDensity = 0.1f;

            [Header("Skybox Fog")]
            public bool affectSkybox = true;
            [Tooltip("Virtual distance used to compute fog for skybox pixels")]
            public float skyboxFogDistance = 1000f;
            [Tooltip("Additional multiplier for skybox fog opacity")]
            [Range(0f, 1f)]
            public float skyboxFogFill = 1.0f;

            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public FogSettings settings = new();

        private Material _material;
        private Texture2D _gradientTex;
        private FogPass _pass;

        public override void Create()
        {
            if (settings.shader == null)
            {
                settings.shader = Shader.Find("Hidden/FogSystem/Fog");
            }

            if (settings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            BakeGradient();

            _pass = new FogPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (_material == null || _gradientTex == null)
                return;

            if (data.cameraData.renderType == CameraRenderType.Overlay)
                return;

            var cameraType = data.cameraData.camera.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;

#if UNITY_EDITOR
            // Automatically rebake in Editor so visual changes in Inspector are immediate
            BakeGradient();
#endif

            _pass.renderPassEvent = settings.renderPassEvent;
            _pass.Setup(_material, _gradientTex, settings);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();

            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }

            if (_gradientTex != null)
            {
                CoreUtils.Destroy(_gradientTex);
                _gradientTex = null;
            }
        }

        private void BakeGradient()
        {
            if (settings.fogGradient == null)
                return;

            if (_gradientTex == null)
            {
                _gradientTex = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
                _gradientTex.hideFlags = HideFlags.HideAndDontSave;
                _gradientTex.wrapMode = TextureWrapMode.Clamp;
                _gradientTex.filterMode = FilterMode.Bilinear;
            }

            Color[] colors = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                colors[i] = settings.fogGradient.Evaluate(i / 255.0f);
            }
            _gradientTex.SetPixels(colors);
            _gradientTex.Apply();
        }
    }

    public sealed class FogPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new("FogSystem_Pass");

        private Material _material;
        private Texture2D _gradientTex;
        private FogFeature.FogSettings _settings;
        
        private RTHandle _cameraColor;
        private RTHandle _tempColor;
        private Vector4 _distanceFogParams;
        private Vector4 _heightFogParams;
        private Vector4 _skyboxFogParams;

        private static readonly int DistanceFogParamsId = Shader.PropertyToID("_DistanceFogParams");
        private static readonly int HeightFogParamsId = Shader.PropertyToID("_HeightFogParams");
        private static readonly int SkyboxFogParamsId = Shader.PropertyToID("_SkyboxFogParams");
        private static readonly int FogGradientTexId = Shader.PropertyToID("_FogGradientTex");
        private static readonly int InvVPId = Shader.PropertyToID("_InvVP");

        public void Setup(Material material, Texture2D gradientTex, FogFeature.FogSettings settings)
        {
            _material = material;
            _gradientTex = gradientTex;
            _settings = settings;

            // Request camera depth texture
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            _cameraColor = data.cameraData.renderer.cameraColorTargetHandle;

            var desc = data.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref _tempColor,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_FogSystemTempColor"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (_material == null || _cameraColor == null || _tempColor == null || _gradientTex == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                // Set uniform properties
                float isExp = _settings.useExponentialDistance ? 1.0f : 0.0f;
                _distanceFogParams.x = _settings.distanceDensity;
                _distanceFogParams.y = _settings.fogStartDistance;
                _distanceFogParams.z = _settings.fogEndDistance;
                _distanceFogParams.w = isExp;

                _heightFogParams.x = _settings.heightBase;
                _heightFogParams.y = _settings.heightDensity;
                _heightFogParams.z = _settings.heightFogDensity;
                _heightFogParams.w = 0f;

                _skyboxFogParams.x = _settings.skyboxFogDistance;
                _skyboxFogParams.y = _settings.skyboxFogFill;
                _skyboxFogParams.z = _settings.affectSkybox ? 1.0f : 0.0f;
                _skyboxFogParams.w = 0f;

                cmd.SetGlobalVector(DistanceFogParamsId, _distanceFogParams);
                cmd.SetGlobalVector(HeightFogParamsId, _heightFogParams);
                cmd.SetGlobalVector(SkyboxFogParamsId, _skyboxFogParams);
                cmd.SetGlobalTexture(FogGradientTexId, _gradientTex);

                // Compute and set Inverse View-Projection Matrix
                var cameraData = data.cameraData;
                var gpuProj = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                var view = cameraData.GetViewMatrix();
                var invVP = (gpuProj * view).inverse;
                cmd.SetGlobalMatrix(InvVPId, invVP);

                // Perform full-screen blit
                Blitter.BlitCameraTexture(cmd, _cameraColor, _tempColor, _material, 0);
                Blitter.BlitCameraTexture(cmd, _tempColor, _cameraColor);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _tempColor?.Release();
            _tempColor = null;
        }
    }
}
