using UnityEngine;

namespace DecalMini
{
    public enum FootprintMode
    {
        Step,   // 传统步进（脚印）
        [InspectorName("Track (Legacy - use Decal Tire Track)")]
        Track   // 兼容旧数据；新的车辙请使用 DecalTireTrackComponent。
    }

    /// <summary>
    /// 脚印逻辑模块：封装脚印生成的物理采样、坐标转换及特效触发逻辑
    /// </summary>
    [System.Serializable]
    public class DecalFootprintModule
    {
        // ========================================================================
        // 1. 模式与外观配置 (Visuals)
        // ========================================================================

        public FootprintMode mode = FootprintMode.Step;

        [Header("Common Appearance")]
        public float lifeTime = 5f;
        public Color tintColor = Color.white;
        [Range(0, 1)] public float softFade = 0.5f;

        [Header("Step Mode (Footprints)")]
        public Texture2D leftFootTex;
        public Texture2D rightFootTex;
        public ParticleSystem leftFootParticle; 
        public ParticleSystem rightFootParticle;
        public float footprintSize = 0.3f;
        [Tooltip("左右脚之间的水平间距")]
        public float stepSideOffset = 0.15f; 

        [Header("Track Mode (Wheel/Rut)")]
        public Texture2D trackTexture;
        public float trackWidth = 0.4f;
        public float trackLength = 0.5f;
        [Tooltip("纹理对应的真实世界长度（米），用于计算平铺连续性")]
        public float tilingSize = 1.0f;
        [Tooltip("生成的采样距离阈值")]
        public float sampleInterval = 0.2f;
        [Tooltip("是否根据速度动态拉伸贴花长度")]
        public bool stretchingWithSpeed = false;
        [Range(1, 4)] public int wheelCount = 2;
        public float wheelSpacing = 1.5f;

        [Header("Physics")]
        public float raycastDistance = 1.5f;
        public LayerMask groundLayer = -1;

        // ========================================================================
        // 3. 运行时状态
        // ========================================================================

        private bool _isLeftNext = true;
        private float[] _wheelDistances = new float[4];
        private const float SurfaceOffset = 0.01f;

        // ========================================================================
        // 4. 核心逻辑 (API)
        // ========================================================================

        /// <summary>
        /// 执行一步脚印：包含射线探测、贴花生成与粒子播放
        /// </summary>
        public void ExecuteStep(Vector3 originWS, Vector3 forwardWS, Vector3 rightWS)
        {
            float offset = stepSideOffset * (_isLeftNext ? -1f : 1f);
            Vector3 footOrigin = originWS + rightWS * offset + Vector3.up * 0.5f;

            if (!Physics.Raycast(footOrigin, Vector3.down, out var hit, raycastDistance, groundLayer)) return;

            Quaternion rot = CalculateStepRotation(hit.normal, forwardWS);
            
            Vector3 size = Vector3.one * footprintSize;
            Texture2D tex = _isLeftNext ? leftFootTex : rightFootTex;
            if (tex != null)
            {
                DecalSystemMini.Spawn(hit.point + hit.normal * SurfaceOffset, rot, size, tex, lifeTime, tintColor, softFade);
            }

            ParticleSystem psPrefab = _isLeftNext ? leftFootParticle : rightFootParticle;
            if (psPrefab != null)
            {
                DecalParticlePoolMini.Play(psPrefab, hit.point + hit.normal * 0.05f, rot, lifeTime);
            }

            _isLeftNext = !_isLeftNext;
        }

        /// <summary>
        /// 执行一段连续轨迹：支持 UV 平铺连续性
        /// </summary>
        public void ExecuteTrack(Vector3 originWS, Vector3 forwardWS, float speed)
        {
            if (trackTexture == null) return;

            // 为了实现无缝对接，盒子长度应等于或略大于采样间距
            float finalLength = trackLength;
            if (stretchingWithSpeed) finalLength += speed * 0.05f; 
            float projectionDepth = 0.5f;
            Vector3 size = new(trackWidth, finalLength, projectionDepth);

            for (int i = 0; i < wheelCount; i++)
            {
                float horizontalOffset = (wheelCount > 1) 
                    ? (i / (float)(wheelCount - 1) - 0.5f) * wheelSpacing 
                    : 0f;

                Vector3 wheelPos = originWS + Vector3.Cross(Vector3.up, forwardWS).normalized * horizontalOffset + Vector3.up * 0.5f;
                
                if (Physics.Raycast(wheelPos, Vector3.down, out var hit, raycastDistance, groundLayer))
                {
                    Quaternion rot = CalculateStepRotation(hit.normal, forwardWS);
                    
                    // 核心修复：计算 UV 连续偏移量
                    // 我们根据盒子长度占平铺长度的比例来计算 scale，根据累计路程来计算 offset
                    float uvScaleY = finalLength / Mathf.Max(0.01f, tilingSize);
                    float vOffset = (_wheelDistances[i] / Mathf.Max(0.01f, tilingSize)) % 1.0f;
                    Vector4 uvScaleOffset = new(1, uvScaleY, 0, vOffset);

                    DecalSystemMini.Spawn(hit.point + hit.normal * SurfaceOffset, rot, size, trackTexture, lifeTime, tintColor, softFade, 10000, 0, 0, 0, 0, uvScaleOffset);
                    
                    // 累加路程
                    _wheelDistances[i] += sampleInterval;
                }
            }
        }

        // ========================================================================
        // 5. 辅助方法 (Helpers)
        // ========================================================================

        private Quaternion CalculateStepRotation(Vector3 normal, Vector3 forward)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(forward, normal);
            if (tangent == Vector3.zero) tangent = forward;
            
            // 商业级规范：Z 轴必须指向投影方向（地心），Y 轴指向前进方向（控制贴图朝向）
            return Quaternion.LookRotation(-normal, tangent.normalized);
        }

        /// <summary>
        /// 编辑器调试 Gizmos：统一使用现代简约角点视觉规范
        /// </summary>
        public void DrawGizmos(Transform t)
        {
            if (mode == FootprintMode.Step)
            {
                DrawFootGizmo(t, -stepSideOffset);
                DrawFootGizmo(t, stepSideOffset);
            }
            else
            {
                for (int i = 0; i < wheelCount; i++)
                {
                    float horizontalOffset = (wheelCount > 1) ? (i / (float)(wheelCount - 1) - 0.5f) * wheelSpacing : 0f;
                    Vector3 pos = t.position + t.right * horizontalOffset;
                    Quaternion gizmoRot = t.rotation * Quaternion.Euler(90, 0, 0); 
                    Vector3 size = new(
                        Mathf.Max(0.01f, trackWidth),
                        Mathf.Max(0.01f, trackLength),
                        0.5f
                    );
                    Matrix4x4 matrix = Matrix4x4.TRS(pos, gizmoRot, size);
                    
                    DecalGizmoUtility.DrawModernGizmo(matrix, pos, "Icon_DecalMini_1");
                }
            }
        }

        private void DrawFootGizmo(Transform t, float offset)
        {
            Vector3 origin = t.position + t.right * offset + Vector3.up * 0.5f;
            Quaternion gizmoRot = t.rotation * Quaternion.Euler(90, 0, 0);
            float size = Mathf.Max(0.01f, footprintSize);
            Matrix4x4 matrix = Matrix4x4.TRS(
                origin + Vector3.down * 0.5f,
                gizmoRot,
                new Vector3(size, size, 0.5f)
            );
            
            // 使用现代简约接口
            DecalGizmoUtility.DrawModernGizmo(matrix, origin, "Icon_DecalMini_1");
        }
    }
}
