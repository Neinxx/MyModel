using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CharacterController.Runtime
{
    /// <summary>
    /// 工业级角色动画调度器 (Playables API 版)
    /// 完全摒弃了连线复杂的 Animator Controller！
    /// 通过纯代码动态构建 PlayableGraph，实现切片 (Clip) 的精准控制与丝滑混合 (Blending)。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("References")]
        public MonoBehaviour motorReference;
        private ICharacterMotor _motor;
        private Animator _animator;

        [Header("Animation Clips")]
        public AnimationClip idleClip;
        public AnimationClip walkClip;
        public AnimationClip slowRunClip;
        public AnimationClip runClip;
        public AnimationClip jumpClip;

        // 核心 Playables 组件
        private PlayableGraph _graph;
        private AnimationMixerPlayable _locomotionMixer;
        private AnimationMixerPlayable _rootMixer;

        private AnimationClipPlayable _jumpPlayable;

        [Header("Locomotion Settings")]
        [Tooltip("达到此速度时，Walk 动画权重达到 100%")]
        public float walkSpeedThreshold = 1.5f;

        [Tooltip("达到此速度时，SlowRun 动画权重达到 100%")]
        public float slowRunSpeedThreshold = 3.5f;

        [Tooltip("达到此速度时，Run 动画权重达到 100%")]
        public float runSpeedThreshold = 5f;

        [Tooltip("状态混合平滑速度")]
        public float locomotionBlendSpeed = 12f;

        [Tooltip("低于此速度时视为完全站立，避免 CharacterController 微小速度导致脚步抖动")]
        public float idleDeadZone = 0.08f;

        private float _smoothedSpeed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false; // 强行关闭 Root Motion，防止动画与 CharacterController 物理位移产生冲突与粘滞感
            _motor = motorReference as ICharacterMotor ?? GetComponentInParent<ICharacterMotor>();

            // 1. 初始化纯代码动画图 (Playable Graph)
            _graph = PlayableGraph.Create("CharacterAnimationGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // 2. 创建输出节点并绑定给 Animator
            var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            // 3. 构建混合器 (Mixer)
            _rootMixer = AnimationMixerPlayable.Create(_graph, 2);
            output.SetSourcePlayable(_rootMixer);

            // 4 个端口的 LocomotionMixer (0: Idle, 1: Walk, 2: SlowRun, 3: Run)
            _locomotionMixer = AnimationMixerPlayable.Create(_graph, 4);

            // 4. 将切片注入图并连接
            if (idleClip != null)
            {
                var idlePlayable = AnimationClipPlayable.Create(_graph, idleClip);
                _graph.Connect(idlePlayable, 0, _locomotionMixer, 0);
            }
            if (walkClip != null)
            {
                var walkPlayable = AnimationClipPlayable.Create(_graph, walkClip);
                _graph.Connect(walkPlayable, 0, _locomotionMixer, 1);
            }
            if (runClip != null)
            {
                var runPlayable = AnimationClipPlayable.Create(_graph, runClip);
                _graph.Connect(runPlayable, 0, _locomotionMixer, 3);
            }
            if (slowRunClip != null)
            {
                var slowRunPlayable = AnimationClipPlayable.Create(_graph, slowRunClip);
                _graph.Connect(slowRunPlayable, 0, _locomotionMixer, 2);
            }
            if (jumpClip != null)
            {
                _jumpPlayable = AnimationClipPlayable.Create(_graph, jumpClip);
                _graph.Connect(_jumpPlayable, 0, _rootMixer, 1);
            }

            // 将 Locomotion 接入 Root 的 0 号端口
            _graph.Connect(_locomotionMixer, 0, _rootMixer, 0);

            // 设置初始权重 (完全播放 Locomotion 的 Idle)
            _rootMixer.SetInputWeight(0, 1f);
            _rootMixer.SetInputWeight(1, 0f);

            _locomotionMixer.SetInputWeight(0, 1f);
            _locomotionMixer.SetInputWeight(1, 0f);
            _locomotionMixer.SetInputWeight(2, 0f);
            _locomotionMixer.SetInputWeight(3, 0f);

            // 启动引擎！
            _graph.Play();
        }

        private void Update()
        {
            if (_motor == null)
            {
                _motor =
                    motorReference as ICharacterMotor ?? GetComponentInParent<ICharacterMotor>();
            }

            if (_motor == null || !_graph.IsValid())
                return;

            // 1. 获取当前水平移动速度
            Vector3 horizontalVelocity = _motor.Velocity;
            horizontalVelocity.y = 0;
            float currentSpeed = horizontalVelocity.magnitude;

            // 2. 平滑过滤速度 (防止输入抖动导致动画抽搐)
            _smoothedSpeed = Mathf.Lerp(
                _smoothedSpeed,
                currentSpeed,
                Time.deltaTime * locomotionBlendSpeed
            );

            // 3. 动态状态解析：Idle / Walk / SlowRun / Run 按速度锚点连续混合
            float targetIdleWeight = 0f;
            float targetWalkWeight = 0f;
            float targetSlowRunWeight = 0f;
            float targetRunWeight = 0f;

            float speed = currentSpeed < idleDeadZone && _smoothedSpeed < idleDeadZone ? 0f : _smoothedSpeed;
            ResolveLocomotionWeights(
                speed,
                out targetIdleWeight,
                out targetWalkWeight,
                out targetSlowRunWeight,
                out targetRunWeight
            );

            // 缺失 SlowRun 切片时自动退化为 Walk/Run 二段混合，不让权重掉空
            if (slowRunClip == null && targetSlowRunWeight > 0f)
            {
                float t = Mathf.InverseLerp(walkSpeedThreshold, runSpeedThreshold, speed);
                targetWalkWeight += targetSlowRunWeight * (1f - t);
                targetRunWeight += targetSlowRunWeight * t;
                targetSlowRunWeight = 0f;
            }

            float finalIdle;
            float finalWalk;
            float finalSlowRun;
            float finalRun;

            // 检测起步瞬间：如果上一次完全处于 Idle，且当前已经判定为移动，则进行“瞬发打断”，不进行 Lerp 慢过渡
            bool wasFullyIdle = _locomotionMixer.GetInputWeight(0) > 0.99f;
            bool isMovingNow = targetIdleWeight == 0f;

            if (wasFullyIdle && isMovingNow)
            {
                finalIdle = 0f;
                finalWalk = Mathf.Max(targetWalkWeight, 0.001f);
                finalSlowRun = targetSlowRunWeight;
                finalRun = targetRunWeight;
            }
            else
            {
                // 其它状态过渡（如加速、减速）使用平滑 Lerp
                float lerpDelta = Time.deltaTime * locomotionBlendSpeed;
                finalIdle = Mathf.Lerp(
                    _locomotionMixer.GetInputWeight(0),
                    targetIdleWeight,
                    lerpDelta
                );
                finalWalk = Mathf.Lerp(
                    _locomotionMixer.GetInputWeight(1),
                    targetWalkWeight,
                    lerpDelta
                );
                finalRun = Mathf.Lerp(
                    _locomotionMixer.GetInputWeight(3),
                    targetRunWeight,
                    lerpDelta
                );
                finalSlowRun = Mathf.Lerp(
                    _locomotionMixer.GetInputWeight(2),
                    targetSlowRunWeight,
                    lerpDelta
                );
            }

            NormalizeWeights(ref finalIdle, ref finalWalk, ref finalSlowRun, ref finalRun);

            // 注入权重
            _locomotionMixer.SetInputWeight(0, finalIdle);
            _locomotionMixer.SetInputWeight(1, finalWalk);
            _locomotionMixer.SetInputWeight(2, finalSlowRun);
            _locomotionMixer.SetInputWeight(3, finalRun);

            // 4. 空中状态处理 (如果是跳跃等特殊状态，可以在这里通过调整 RootMixer 的权重来覆写日常动作)
            if (!_motor.IsGrounded)
            {
                _rootMixer.SetInputWeight(
                    0,
                    Mathf.Lerp(_rootMixer.GetInputWeight(0), 0f, Time.deltaTime * 10f)
                );
                _rootMixer.SetInputWeight(
                    1,
                    Mathf.Lerp(_rootMixer.GetInputWeight(1), 1f, Time.deltaTime * 10f)
                );
            }
            else
            {
                _rootMixer.SetInputWeight(
                    0,
                    Mathf.Lerp(_rootMixer.GetInputWeight(0), 1f, Time.deltaTime * 10f)
                );
                _rootMixer.SetInputWeight(
                    1,
                    Mathf.Lerp(_rootMixer.GetInputWeight(1), 0f, Time.deltaTime * 10f)
                );
            }
        }

        public void TriggerJump()
        {
            if (!_graph.IsValid() || jumpClip == null)
                return;

            // 纯代码重置跳跃动画时间，重新播放
            _jumpPlayable.SetTime(0f);
        }

        private void ResolveLocomotionWeights(
            float speed,
            out float idleWeight,
            out float walkWeight,
            out float slowRunWeight,
            out float runWeight
        )
        {
            idleWeight = 0f;
            walkWeight = 0f;
            slowRunWeight = 0f;
            runWeight = 0f;

            float walkPoint = Mathf.Max(idleDeadZone + 0.01f, walkSpeedThreshold);
            float slowRunPoint = Mathf.Max(walkPoint + 0.01f, slowRunSpeedThreshold);
            float runPoint = Mathf.Max(slowRunPoint + 0.01f, runSpeedThreshold);

            if (speed <= idleDeadZone)
            {
                idleWeight = 1f;
                return;
            }

            if (speed < walkPoint)
            {
                float t = Mathf.InverseLerp(idleDeadZone, walkPoint, speed);
                idleWeight = 1f - t;
                walkWeight = t;
                return;
            }

            if (speed < slowRunPoint)
            {
                float t = Mathf.InverseLerp(walkPoint, slowRunPoint, speed);
                walkWeight = 1f - t;
                slowRunWeight = t;
                return;
            }

            if (speed < runPoint)
            {
                float t = Mathf.InverseLerp(slowRunPoint, runPoint, speed);
                slowRunWeight = 1f - t;
                runWeight = t;
                return;
            }

            runWeight = 1f;
        }

        private static void NormalizeWeights(
            ref float idleWeight,
            ref float walkWeight,
            ref float slowRunWeight,
            ref float runWeight
        )
        {
            float total = idleWeight + walkWeight + slowRunWeight + runWeight;
            if (total <= 0.0001f)
            {
                idleWeight = 1f;
                walkWeight = 0f;
                slowRunWeight = 0f;
                runWeight = 0f;
                return;
            }

            idleWeight /= total;
            walkWeight /= total;
            slowRunWeight /= total;
            runWeight /= total;
        }

        private void OnDestroy()
        {
            // 必须手动销毁 Graph 防止内存泄漏
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

    }
}
