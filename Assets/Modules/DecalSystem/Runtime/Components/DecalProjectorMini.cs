using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 挂载场景物体上的投影器组件：支持动画、序列帧与径向裁剪
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("ShaderMini/Decal Projector")]
    public class DecalProjectorMini : MonoBehaviour, IDecalProvider
    {
        private static int _animatedPreviewProjectorCount;

        public static bool HasAnimatedPreviewProjectors => _animatedPreviewProjectorCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _animatedPreviewProjectorCount = 0;
        }

        // ========================================================================
        // 1. 序列化字段 (Inspector)
        // ========================================================================

        [Header("Basic Settings")]
        public Texture2D decalTexture;

        [ColorUsage(true, true)]
        public Color color = Color.white;

        [field: SerializeField]
        public int sortingOrder { get; set; } = 0;

        [Header("Animation")]
        public float rotationSpeed = 0f;
        public bool pulseEffect = false;
        public float pulseSpeed = 2f;
        public float pulseRange = 0.2f;
        public float flipbookSpeed = 15f;

        [Header("Shape")]
        public bool useRadialMask = false;

        [Range(0.01f, 0.5f)]
        public float radialSoftness = 0.1f;

        [Header("Projection Config")]
        public Vector4 uvScaleOffset = new(1, 1, 0, 0);

        [Range(0, 1)]
        public float angleFadeStart = 0.5f;

        [Range(1, 100)]
        public float distanceFade = 50f;

        [Range(0, 0.5f)]
        public float softFade = 0.1f;

        // ========================================================================
        // 2. 内部运行时状态
        // ========================================================================

        private float _currentLifeTime = -1f;
        private DecalDataMini _cachedData;
        private bool _isDirty = true;
        private bool _isTrackedAsAnimatedPreview;

        private Transform _transform;
        public new Transform transform => _transform != null ? _transform : (_transform = GetComponent<Transform>());
        
        private GameObject _gameObject;
        public new GameObject gameObject => _gameObject != null ? _gameObject : (_gameObject = base.gameObject);
        public new bool isActiveAndEnabled => base.isActiveAndEnabled;
        
        private Vector3 _lastPos;
        private Quaternion _lastRot;
        private Vector3 _lastScale;

        // ========================================================================
        // 3. 生命周期与核心逻辑 (Unity Lifecycle)
        // ========================================================================

        private void Awake()
        {
            _transform = transform;
            SyncLastTransform();
        }

        private void OnEnable()
        {
            _isDirty = true;
            DecalSystemMini.Register(this);
            RefreshAnimatedPreviewTracking();
        }

        private void Update()
        {
            // 自动回收逻辑
            if (_currentLifeTime >= 0)
            {
                _currentLifeTime -= Time.deltaTime;
                if (_currentLifeTime <= 0)
                {
                    _currentLifeTime = -1f;
                    DecalPoolMini.Release(this);
                }
            }

            // 变换检测：空间网格同步
            if (HasTransformChanged())
            {
                DecalSystemMini.UpdateGridPosition(this, _lastPos, _transform.position);
                SyncLastTransform();
                _isDirty = true;
            }
        }

        private void OnDisable()
        {
            SetAnimatedPreviewTracked(false);
            DecalSystemMini.Unregister(this);
        }

        private void OnValidate()
        {
            _isDirty = true;
            RefreshAnimatedPreviewTracking();
            if (!Application.isPlaying)
                DecalEditorRuntimeBridge.RequestSceneRepaint();
        }

        // ========================================================================
        // 4. 公共 API
        // ========================================================================

        /// <summary>
        /// 启动计时器（由对象池驱动）
        /// </summary>
        public void Play(float lifeTime)
        {
            _currentLifeTime = lifeTime;
            _isDirty = true;
        }

        /// <summary>
        /// 将组件属性转换为渲染内核所需的 DecalDataMini
        /// </summary>
        public DecalDataMini ToDecalData()
        {
            // 如果缓存已失效，或者缓存从未初始化（投影行权重为零），则重新计算
            if (!_isDirty && _cachedData.wtd3.w != 0)
                return _cachedData;

            _cachedData = new DecalDataMini
            {
                color = color,
                uvScaleOffset = uvScaleOffset,
                fadeParams = new Vector4(
                    angleFadeStart,
                    distanceFade,
                    DecalSystemMini.GetTextureIndex(decalTexture),
                    softFade
                ),
                animParams = new Vector4(
                    rotationSpeed,
                    pulseEffect ? pulseSpeed : 0,
                    pulseRange,
                    useRadialMask ? radialSoftness : 0
                ),
                animParams2 = new Vector4(
                    DecalSystemMini.GetFlipbookCount(decalTexture),
                    flipbookSpeed,
                    0,
                    0
                ),
            };

            // 矩阵计算：轴心点偏移对齐
            if (_transform == null)
                _transform = transform;

            Matrix4x4 dtw =
                _transform.localToWorldMatrix * Matrix4x4.Translate(new Vector3(0, 0, 0.5f));
            _cachedData.SetMatrices(dtw.inverse, dtw);

            _isDirty = false;
            return _cachedData;
        }

        // ========================================================================
        // 5. 内部辅助逻辑
        // ========================================================================

        private bool HasTransformChanged()
        {
            if (_transform == null)
                return false;
            return _transform.position != _lastPos
                || _transform.rotation != _lastRot
                || _transform.localScale != _lastScale;
        }

        private void SyncLastTransform()
        {
            if (_transform == null)
                return;
            _lastPos = _transform.position;
            _lastRot = _transform.rotation;
            _lastScale = _transform.localScale;
        }

        private void RefreshAnimatedPreviewTracking()
        {
            SetAnimatedPreviewTracked(isActiveAndEnabled && NeedsAnimatedPreview());
        }

        private bool NeedsAnimatedPreview() =>
            Mathf.Abs(rotationSpeed) > 0.001f || pulseEffect || HasFlipbookPreview();

        private bool HasFlipbookPreview() =>
            decalTexture != null && flipbookSpeed > 0.001f && DecalSystemMini.GetFlipbookCount(decalTexture) > 1;

        private void SetAnimatedPreviewTracked(bool shouldTrack)
        {
            if (_isTrackedAsAnimatedPreview == shouldTrack)
                return;

            _isTrackedAsAnimatedPreview = shouldTrack;
            _animatedPreviewProjectorCount += shouldTrack ? 1 : -1;
            if (_animatedPreviewProjectorCount < 0)
                _animatedPreviewProjectorCount = 0;
        }

        // ========================================================================
        // 6. 编辑器可视化 (Gizmos)
        // ========================================================================

#if UNITY_EDITOR
        private void OnDrawGizmos() =>
            DecalGizmoUtility.DrawIcon(transform.position, "Icon_DecalMini_1");

        private void OnDrawGizmosSelected() =>
            DecalGizmoUtility.DrawModernGizmo(
                transform.localToWorldMatrix,
                transform.position,
                "Icon_DecalMini_1"
            );
#endif
    }
}
