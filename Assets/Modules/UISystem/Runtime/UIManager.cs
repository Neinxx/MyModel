using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UISystem.Runtime
{
    public enum UILayerType
    {
        Background = 0,
        HUD = 1,
        MainPanel = 2,
        Popup = 3,
        Toast = 4,
        System = 5
    }

    /// <summary>
    /// 全局 UI 管理器 (Universal UI Manager)
    /// 负责视图注册、分层与导航导航。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Layers (Mount Points)")]
        [FormerlySerializedAs("backgroundLayer")]
        [SerializeField] private Transform _backgroundLayer;
        [FormerlySerializedAs("hudLayer")]
        [SerializeField] private Transform _hudLayer;
        [FormerlySerializedAs("mainPanelLayer")]
        [SerializeField] private Transform _mainPanelLayer;
        [FormerlySerializedAs("popupLayer")]
        [SerializeField] private Transform _popupLayer;
        [FormerlySerializedAs("toastLayer")]
        [SerializeField] private Transform _toastLayer;
        [FormerlySerializedAs("systemLayer")]
        [SerializeField] private Transform _systemLayer;

        [Header("Runtime Safety")]
        [Tooltip("When enabled, UIManager creates a missing EventSystem at runtime. Prefer an explicit EventSystem in production prefabs.")]
        [FormerlySerializedAs("createEventSystemIfMissing")]
        [SerializeField] private bool _createEventSystemIfMissing;

        private readonly Dictionary<string, UIView> _views = new Dictionary<string, UIView>();
        private readonly Stack<UIView> _history = new Stack<UIView>();

        public Transform BackgroundLayer => _backgroundLayer;
        public Transform HudLayer => _hudLayer;
        public Transform MainPanelLayer => _mainPanelLayer;
        public Transform PopupLayer => _popupLayer;
        public Transform ToastLayer => _toastLayer;
        public Transform SystemLayer => _systemLayer;
        public bool CreateEventSystemIfMissing => _createEventSystemIfMissing;

        public Transform GetLayer(UILayerType type)
        {
            return type switch
            {
                UILayerType.Background => _backgroundLayer,
                UILayerType.HUD => _hudLayer,
                UILayerType.MainPanel => _mainPanelLayer,
                UILayerType.Popup => _popupLayer,
                UILayerType.Toast => _toastLayer,
                UILayerType.System => _systemLayer,
                _ => _mainPanelLayer
            };
        }

        public void SetLayer(UILayerType type, Transform layer)
        {
            switch (type)
            {
                case UILayerType.Background:
                    _backgroundLayer = layer;
                    break;
                case UILayerType.HUD:
                    _hudLayer = layer;
                    break;
                case UILayerType.MainPanel:
                    _mainPanelLayer = layer;
                    break;
                case UILayerType.Popup:
                    _popupLayer = layer;
                    break;
                case UILayerType.Toast:
                    _toastLayer = layer;
                    break;
                case UILayerType.System:
                    _systemLayer = layer;
                    break;
            }
        }

        public void SetCreateEventSystemIfMissing(bool value)
        {
            _createEventSystemIfMissing = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
                EnsureEventSystemIfRequested();
            }

            var views = GetComponentsInChildren<UIView>(true);
            foreach (var view in views)
            {
                RegisterView(view);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void EnsureEventSystemIfRequested()
        {
            if (!_createEventSystemIfMissing)
                return;

            var eventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (eventSystem == null)
            {
                var esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

                if (inputModuleType != null)
                {
                    esObj.AddComponent(inputModuleType);
                    Debug.Log("[UIManager] Created missing EventSystem with InputSystemUIInputModule.");
                }
                else
                {
                    esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    Debug.Log("[UIManager] Created missing EventSystem with StandaloneInputModule.");
                }

                esObj.transform.SetParent(transform);
            }
            else
            {
                var inputModule = eventSystem.GetComponent<UnityEngine.EventSystems.BaseInputModule>();
                if (inputModule == null)
                {
                    if (inputModuleType != null)
                    {
                        eventSystem.gameObject.AddComponent(inputModuleType);
                        Debug.Log("[UIManager] Attached missing InputSystemUIInputModule to existing EventSystem.");
                    }
                    else
                    {
                        eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                        Debug.Log("[UIManager] Attached missing StandaloneInputModule to existing EventSystem.");
                    }
                }
            }
        }

        public void RegisterView(UIView view)
        {
            if (view == null)
                return;

            view.EnsureInitialized();
            if (string.IsNullOrWhiteSpace(view.ViewId))
                return;

            if (_views.TryGetValue(view.ViewId, out var existingView))
            {
                if (existingView != view)
                    Debug.LogWarning($"[UIManager] Duplicate view id ignored: {view.ViewId}", view);
                return;
            }

            _views.Add(view.ViewId, view);
        }

        public T GetView<T>(string id) where T : UIView
        {
            if (_views.TryGetValue(id, out var view))
            {
                return view as T;
            }
            return null;
        }

        public void OpenView(string id, bool addToHistory = true)
        {
            if (_views.TryGetValue(id, out var view))
            {
                view.Open();
                if (addToHistory && (_history.Count == 0 || _history.Peek() != view))
                    _history.Push(view);
            }
        }

        public void CloseTopView()
        {
            if (_history.Count > 0)
            {
                var view = _history.Pop();
                view.Close();
            }
        }

        public void CloseAll()
        {
            foreach (var view in _views.Values)
            {
                view.Close();
            }
            _history.Clear();
        }
    }
}
