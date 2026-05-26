using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 脚印组件：负责运动监测、触发模式调度以及 Module 生命周期管理
    /// </summary>
    [AddComponentMenu("ShaderMini/Decal Footprint")]
    public class DecalFootprintComponent : MonoBehaviour
    {
        // ========================================================================
        // 1. 枚举与辅助结构
        // ========================================================================

        public enum TriggerMode
        {
            Distance, // 基于位移触发
            AnimationEvent, // 动画事件触发
            Scripting, // 外部脚本触发
        }

        // ========================================================================
        // 2. 序列化字段 (Inspector)
        // ========================================================================

        [Header("Pool Config")]
        [Tooltip("指定投影器预制体以自动初始化对象池")]
        public DecalProjectorMini decalPrefab;

        [Header("Trigger Settings")]
        public TriggerMode triggerMode = TriggerMode.Distance;
        public DecalFootprintModule footprintModule;

        [Header("Distance Mode Only")]
        public float minStepDistance = 0.8f;

        // ========================================================================
        // 3. 内部运行时状态
        // ========================================================================

        private Vector3 _lastStepPos;

        // ========================================================================
        // 4. 生命周期 (Unity Lifecycle)
        // ========================================================================

        private void Awake()
        {
            // 延迟初始化对象池，确保单例安全性
            if (decalPrefab != null)
            {
                DecalPoolMini.Init(decalPrefab);
            }
        }

        private void Start()
        {
            _lastStepPos = transform.position;
        }

        private FootprintMode _lastMode;

        private void Update()
        {
            if (triggerMode != TriggerMode.Distance)
                return;

            // 模式切换检测与状态重置
            if (footprintModule != null && footprintModule.mode != _lastMode)
            {
                _lastStepPos = transform.position;
                _lastMode = footprintModule.mode;
            }

            CheckDistanceTrigger();
        }

        // ========================================================================
        // 5. 公共 API
        // ========================================================================

        /// <summary>
        /// 手动触发一步或一段轨迹（支持外部脚本或动画事件回调）
        /// </summary>
        public void TriggerStep()
        {
            if (footprintModule == null)
                return;

            // 根据模式分发逻辑
            if (footprintModule.mode == FootprintMode.Step)
            {
                footprintModule.ExecuteStep(transform.position, transform.forward, transform.right);
            }
            else
            {
                // 计算当前速度 (增加极小值保护)
                float dist = Vector3.Distance(transform.position, _lastStepPos);
                float speed = dist / Mathf.Max(0.0001f, Time.deltaTime);
                footprintModule.ExecuteTrack(transform.position, transform.forward, speed);
            }
        }

        /// <summary>
        /// 编辑器专用的手动 Tick (用于在 Scene 窗口不进入运行模式时预览脚印)
        /// </summary>
        public bool ManualUpdateEditor()
        {
            if (Application.isPlaying)
                return false;

            if (_lastStepPos == Vector3.zero && transform.position != Vector3.zero)
            {
                _lastStepPos = transform.position;
            }

            return triggerMode == TriggerMode.Distance && CheckDistanceTrigger();
        }

        // ========================================================================
        // 6. 内部逻辑实现
        // ========================================================================

        private bool CheckDistanceTrigger()
        {
            if (footprintModule == null) return false;

            // 动态选择采样间隔
            float threshold = footprintModule.mode == FootprintMode.Step 
                ? minStepDistance 
                : footprintModule.sampleInterval;

            if (Vector3.Distance(transform.position, _lastStepPos) < threshold)
                return false;

            TriggerStep();
            _lastStepPos = transform.position;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() => DecalGizmoUtility.DrawIcon(transform.position, "Icon_DecalMini_1");

        private void OnDrawGizmosSelected()
        {
            footprintModule?.DrawGizmos(transform);
        }
#endif
    }
}
