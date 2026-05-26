using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System;

namespace SlimeSystem
{
    public class SlimeSimulationSystem : IDisposable
    {
        public struct Particle
        {
            public float3 position;
            public float3 velocity;
            public float3 predicted;
            public float density;
            public float lambda;
        }

        public NativeArray<Particle> Particles;

        private int _particleCount;
        private float _particleRadius;
        private float _restDensity;

        public SlimeSimulationSystem(int count, float radius, float restDensity)
        {
            _particleCount = count;
            _particleRadius = radius;
            _restDensity = restDensity;

            Particles = new NativeArray<Particle>(count, Allocator.Persistent);
        }

        public JobHandle Simulate(float deltaTime, float3 gravity, float3 shapeCenter, float shapeForce)
        {
            // 1. 预测位置
            var predictJob = new PredictJob
            {
                Particles = Particles,
                DeltaTime = deltaTime,
                Gravity = gravity
            };
            var handle = predictJob.Schedule(_particleCount, 64);

            // 2. 计算密度 (Brute Force)
            var densityJob = new DensityJob
            {
                Particles = Particles,
                RestDensity = _restDensity,
                ParticleRadius = _particleRadius
            };
            handle = densityJob.Schedule(_particleCount, 32, handle);

            // 3. 求解约束 (Brute Force)
            var solveJob = new SolveJob
            {
                Particles = Particles,
                ParticleRadius = _particleRadius,
                ShapeCenter = shapeCenter,
                ShapeForce = shapeForce,
                DeltaTime = deltaTime
            };
            handle = solveJob.Schedule(_particleCount, 32, handle);

            return handle;
        }

        [BurstCompile]
        struct PredictJob : IJobParallelFor
        {
            public NativeArray<Particle> Particles;
            public float DeltaTime;
            public float3 Gravity;

            public void Execute(int index)
            {
                var p = Particles[index];
                p.velocity += Gravity * DeltaTime;
                p.predicted = p.position + p.velocity * DeltaTime;
                Particles[index] = p;
            }
        }

        [BurstCompile]
        struct DensityJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Particle> Particles;
            public float RestDensity;
            public float ParticleRadius;

            public void Execute(int index)
            {
                var p_i = Particles[index];
                float3 pos_i = p_i.predicted;
                float density = 0;
                float h = ParticleRadius * 2.5f;

                // 暴力搜索邻居 (Burst 优化后非常快)
                for (int j = 0; j < Particles.Length; j++)
                {
                    if (index == j) continue;
                    float r = math.distance(pos_i, Particles[j].predicted);
                    if (r < h)
                    {
                        float w = 1.0f - r / h;
                        density += w * w * w;
                    }
                }

                p_i.density = density;
                float C = (density / RestDensity) - 1.0f;
                p_i.lambda = -C * 0.5f;
                Particles[index] = p_i;
            }
        }

        [BurstCompile]
        struct SolveJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Particle> Particles;
            public float ParticleRadius;
            public float3 ShapeCenter;
            public float ShapeForce;
            public float DeltaTime;

            public void Execute(int index)
            {
                var p_i = Particles[index];
                float3 pos_i = p_i.predicted;
                float3 deltaP = float3.zero;
                float h = ParticleRadius * 2.5f;

                for (int j = 0; j < Particles.Length; j++)
                {
                    if (index == j) continue;
                    float3 r_vec = pos_i - Particles[j].predicted;
                    float r = math.length(r_vec);
                    if (r < h && r > 0.001f)
                    {
                        float w = 1.0f - r / h;
                        float scorr = -0.1f * math.pow(w, 4);
                        deltaP += (p_i.lambda + Particles[j].lambda + scorr) * math.normalize(r_vec) * w;
                    }
                }

                // 形状维持力
                float3 toCenter = ShapeCenter - pos_i;
                deltaP += math.normalize(toCenter) * math.length(toCenter) * ShapeForce * 0.01f;

                p_i.predicted += deltaP;

                // 地面碰撞
                if (p_i.predicted.y < 0.05f)
                {
                    p_i.predicted.y = 0.05f;
                    p_i.velocity.y *= -0.1f;
                }

                // 速度更新与阻尼
                p_i.velocity = ((p_i.predicted - p_i.position) / DeltaTime) * 0.98f;
                p_i.position = p_i.predicted;
                Particles[index] = p_i;
            }
        }

        public void Dispose()
        {
            if (Particles.IsCreated) Particles.Dispose();
        }
    }
}
