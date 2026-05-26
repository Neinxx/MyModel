using CharacterController.Runtime;
using DecalMini.Runtime.Modules.Dynamic;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// DECAL AURA COMPONENT (RGBA Restore)
    /// V4: Advanced 4-Channel Persistent Projector.
    /// Handles spatial registration, socket snapping, and GPU data injection.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("ShaderMini/Decal Aura")]
    [DefaultExecutionOrder(110)]
    public class DecalAuraComponent : MonoBehaviour, IDecalProvider
    {
        // ========================================================================
        // 1. CONFIGURATION (RGBA MODULE)
        // ========================================================================

        [SerializeField]
        private DecalAuraModule _auraModule = new();
        public DecalAuraModule auraModule
        {
            get => _auraModule;
            set
            {
                _auraModule = value;
                _isDirty = true;
            }
        }

        // --- Compatibility Bridges for AuraFeatureSO & Tests ---
        public Texture2D auraTexture
        {
            get => _auraModule.auraTexture;
            set
            {
                _auraModule.auraTexture = value;
                _isDirty = true;
            }
        }
        public Color tintColor
        {
            get => _auraModule.layerR.color;
            set
            {
                _auraModule.layerR.color = value;
                _isDirty = true;
            }
        }
        public float radius
        {
            get => transform.localScale.x;
            set
            {
                transform.localScale = new Vector3(value, value, transform.localScale.z);
                _isDirty = true;
            }
        }
        public float projectionDepth
        {
            get => transform.localScale.z;
            set
            {
                transform.localScale = new Vector3(
                    transform.localScale.x,
                    transform.localScale.y,
                    value
                );
                _isDirty = true;
            }
        }

        // -------------------------------------------------------

        [field: SerializeField]
        public int sortingOrder { get; set; } = 100;

        [Header("Character Socket")]
        public CharacterSocketId socketId = CharacterSocketId.Aura;
        public bool autoSnapToSocket = true;

        [Header("Orientation")]
        public bool lockRotation = true;
        public bool stickToGround = false;
        public LayerMask groundLayer = -1;
        public float heightOffset = 0f;
        public float raycastDistance = 5.0f;

        // ========================================================================
        // 2. INTERNAL STATE
        // ========================================================================

        private Transform _transform;
        private Transform _centerTransform;
        private Vector3 _lastWorldPos;
        private bool _isDirty = true;
        private DecalDataMini _cachedData;

        // Implementation of IDecalProvider
        public new bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;

        private void OnEnable()
        {
            _transform = transform;
            _centerTransform = transform;
            RefreshAnchorLink();
            _lastWorldPos = _transform.position; // 确保在锚定对齐后，再记录初始世界位置
            DecalSystemMini.Register(this);
        }

        private void OnDisable() => DecalSystemMini.Unregister(this);

        private void OnDestroy() => DecalSystemMini.Unregister(this);

        private void OnValidate()
        {
            _isDirty = true;
            // Immediate feedback for socket changes in editor
            if (!Application.isPlaying)
            {
                _transform = transform;
                RefreshAnchorLink();

#if UNITY_EDITOR
                // 核心修复：强制编辑器重绘，解决参数修改后显示不及时的问题
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
#endif
            }
        }

        public void RefreshAnchorLink()
        {
            var socketRegistry = GetComponentInParent<CharacterSocketRegistry>();
            if (socketRegistry != null && autoSnapToSocket)
            {
                if (socketRegistry.TryGet(socketId, out var socketT) && socketT != null)
                {
                    // 智能防御：如果我们直接挂载在 Socket 槽位节点本身上，无需任何重父对齐，防止自循环 parenting 报错
                    if (transform == socketT)
                    {
                        _centerTransform = socketT;
                        _isDirty = true;
                        _lastWorldPos = transform.position;
                        return;
                    }

                    if (transform.parent != socketT)
                    {
                        transform.SetParent(socketT);
                        transform.localPosition = Vector3.zero;
                        transform.localRotation = Quaternion.identity;
                    }
                    _centerTransform = socketT;
                    _isDirty = true;
                    _lastWorldPos = transform.position; // 同步世界位置缓存，防止网格更新时旧键值漂移
                    return;
                }
            }
            _centerTransform = _transform;
            _isDirty = true;
            _lastWorldPos = _transform.position; // 同步世界位置缓存，防止网格更新时旧键值漂移
        }

        // ========================================================================
        // 4. CORE LOOP & RENDERING
        // ========================================================================

        private void Update()
        {
            if (_transform.hasChanged)
            {
                if (Vector3.SqrMagnitude(_transform.position - _lastWorldPos) > 0.0001f)
                {
                    DecalSystemMini.UpdateGridPosition(this, _lastWorldPos, _transform.position);
                    _lastWorldPos = _transform.position;
                }

                _isDirty = true;
                _transform.hasChanged = false;
            }
        }

        public DecalDataMini ToDecalData()
        {
            if (!_isDirty)
                return _cachedData;

            _cachedData = new DecalDataMini();

            // 4-Channel Module Injection
            if (auraModule != null)
            {
                auraModule.FillDecalData(ref _cachedData);
            }

            Vector3 finalPos = _centerTransform.position;
            Quaternion finalRot = lockRotation
                ? Quaternion.Euler(90, 0, 0)
                : _centerTransform.rotation;

            if (stickToGround)
            {
                Vector3 rayOrigin = finalPos + Vector3.up * (raycastDistance * 0.5f);
                if (
                    Physics.Raycast(
                        rayOrigin,
                        Vector3.down,
                        out var hit,
                        raycastDistance + 1.0f,
                        groundLayer
                    )
                )
                {
                    finalPos = hit.point + hit.normal * heightOffset;
                    if (!lockRotation)
                        finalRot = Quaternion.LookRotation(
                            Vector3.ProjectOnPlane(_centerTransform.forward, hit.normal),
                            hit.normal
                        );
                }
            }

            Matrix4x4 dtw = Matrix4x4.TRS(finalPos, finalRot, _transform.localScale);
            _cachedData.SetMatrices(dtw.inverse, dtw);
            _isDirty = false;
            return _cachedData;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() =>
            DecalGizmoUtility.DrawIcon(transform.position, "Icon_DecalMini_1");

        private void OnDrawGizmosSelected() =>
            DecalGizmoUtility.DrawModernGizmo(
                Matrix4x4.TRS(
                    _centerTransform != null ? _centerTransform.position : transform.position,
                    _centerTransform != null ? _centerTransform.rotation : transform.rotation,
                    transform.localScale
                ),
                transform.position,
                "Icon_DecalMini_1"
            );
#endif
    }
}
