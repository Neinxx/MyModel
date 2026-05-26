using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DecalMini
{
    public class DecalPassMini : ScriptableRenderPass
    {
        private Material _material;
        private float _maxDrawDistance;
        private float _fadeRange;
        private ComputeBuffer _decalBuffer;
        private DecalDataMini[] _dataArray;
        private LayerMask _layerMask;

        public DecalPassMini(Material mat)
        {
            _material = mat;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void SetParams(
            float maxDist,
            float fade,
            LayerMask layerMask,
            ComputeBuffer buffer,
            DecalDataMini[] dataArray
        )
        {
            _maxDrawDistance = maxDist;
            _fadeRange = fade;
            _layerMask = layerMask;
            _decalBuffer = buffer;
            _dataArray = dataArray;
        }

        private static class ShaderIDs
        {
            public static readonly int DecalDataBuffer = Shader.PropertyToID("_DecalDataBuffer");
            public static readonly int DecalAtlasArray = Shader.PropertyToID("_DecalAtlasArray");
            public static readonly int DecalNormalArray = Shader.PropertyToID("_DecalNormalArray");
            public static readonly int DecalFadeParams = Shader.PropertyToID("_DecalFadeParams");
            public static readonly int InvVP = Shader.PropertyToID("_InvVP");
            public static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
        }

        private static readonly Plane[] _frustumPlanes = new Plane[6];

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData
        )
        {
            if (_material == null || _decalBuffer == null)
                return;

            // 0-GC 优化：使用静态预分配数组提取视锥面
            GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera, _frustumPlanes);

            Vector3 camPos = renderingData.cameraData.camera.transform.position;
            int count = DecalSystemMini.FillData(
                camPos,
                _maxDrawDistance,
                _layerMask,
                _dataArray,
                _frustumPlanes
            );
            if (count <= 0)
                return;

            _decalBuffer.SetData(_dataArray, 0, 0, count);

            Texture2DArray texArray = DecalSystemMini.GetTextureArray();
            if (texArray == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Decal_Render_Mini");

            cmd.SetGlobalBuffer(ShaderIDs.DecalDataBuffer, _decalBuffer);
            cmd.SetGlobalTexture(ShaderIDs.DecalAtlasArray, texArray);

            Texture2DArray normArray = DecalSystemMini.CurrentConfig.bakedNormalArray;
            if (normArray != null)
            {
                cmd.SetGlobalTexture(ShaderIDs.DecalNormalArray, normArray);
            }

            cmd.SetGlobalVector(
                ShaderIDs.DecalFadeParams,
                new Vector4(_maxDrawDistance, _fadeRange, 0, 0)
            );

            // 手动传递逆投影矩阵，确保在所有平台和模式下重建坐标正确
            Matrix4x4 viewMat = renderingData.cameraData.GetViewMatrix();
            Matrix4x4 projMat = renderingData.cameraData.GetGPUProjectionMatrix();
            Matrix4x4 vpMat = projMat * viewMat;
            cmd.SetGlobalMatrix(ShaderIDs.InvVP, vpMat.inverse);

            // 已移至 CPU 剔除，不再传递 _CameraFrustumPlanes

            // 关键：不再需要 Mesh，通过 Procedural 模式让 Shader 自动生成 36 个顶点
            cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 36, count);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose() { }
    }
}
