using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace SlimeSystem
{
    public class SlimeController : MonoBehaviour
    {
        [Header("仿真设置")]
        public int particleCount = 2048;
        public float particleRadius = 0.3f;
        public float restDensity = 1.0f;
        public float stiffness = 0.5f;
        public Vector3 gravity = new Vector3(0, -9.81f, 0);

        public enum ShapeType { None = 0, Sphere = 1, Box = 2 }
        [Header("形状约束")]
        public ShapeType targetShape = ShapeType.None;
        public Vector3 shapeCenter = Vector3.zero;
        public float shapeForce = 20f;

        [Header("引用")]
        public Material stylizedMaterial;
        
        private SlimeSimulationSystem _simulation;
        private ComputeBuffer _particleBuffer;
        private JobHandle _lastJobHandle;

        void Start()
        {
            _simulation = new SlimeSimulationSystem(particleCount, particleRadius, restDensity);
            
            // 初始化粒子位置
            var particles = _simulation.Particles;
            for (int i = 0; i < particleCount; i++)
            {
                var p = new SlimeSimulationSystem.Particle();
                p.position = (float3)transform.position + (float3)UnityEngine.Random.insideUnitSphere * 2f;
                p.velocity = Vector3.zero;
                p.predicted = p.position;
                particles[i] = p;
            }

            // GPU 缓冲区仅用于渲染 (数据布局需与 SlimeSimulationSystem.Particle 匹配)
            // position(12) + velocity(12) + predicted(12) + density(4) + lambda(4) = 44 bytes
            _particleBuffer = new ComputeBuffer(particleCount, 44); 
            
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void Update()
        {
            // 1. 确保上一帧完成并同步到 GPU
            _lastJobHandle.Complete();
            _particleBuffer.SetData(_simulation.Particles);

            // 2. 调度新一帧仿真
            Vector3 worldCenter = transform.TransformPoint(shapeCenter);
            _lastJobHandle = _simulation.Simulate(Time.deltaTime, gravity, worldCenter, shapeForce);
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (stylizedMaterial == null || _particleBuffer == null || !Application.isPlaying) return;

            stylizedMaterial.SetBuffer("_Particles", _particleBuffer);
            stylizedMaterial.SetFloat("_ParticleRadius", particleRadius);
            stylizedMaterial.SetInt("_ParticleCount", particleCount);

            stylizedMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, particleCount);
        }

        void OnDestroy()
        {
            _lastJobHandle.Complete();
            _simulation?.Dispose();
            if (_particleBuffer != null) _particleBuffer.Dispose();
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 worldPos = transform.TransformPoint(shapeCenter);
            Gizmos.DrawWireSphere(worldPos, 1.0f);
        }
    }
}
