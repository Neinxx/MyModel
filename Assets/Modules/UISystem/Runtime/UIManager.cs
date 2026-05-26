using System.Collections.Generic;
using UnityEngine;

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
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Layers (Mount Points)")]
        public Transform backgroundLayer;
        public Transform hudLayer;
        public Transform mainPanelLayer;
        public Transform popupLayer;
        public Transform toastLayer;
        public Transform systemLayer;

        private Dictionary<string, UIView> _views = new Dictionary<string, UIView>();
        private Stack<UIView> _history = new Stack<UIView>();

        public Transform GetLayer(UILayerType type)
        {
            return type switch
            {
                UILayerType.Background => backgroundLayer,
                UILayerType.HUD => hudLayer,
                UILayerType.MainPanel => mainPanelLayer,
                UILayerType.Popup => popupLayer,
                UILayerType.Toast => toastLayer,
                UILayerType.System => systemLayer,
                _ => mainPanelLayer
            };
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
                
                // 🚀 核心自愈：动态布设/接管唯一的 EventSystem 环境，支持新老输入系统自适应交互
                EnsureEventSystem();
            }

            // 自动注册当前节点下的所有视图
            var views = GetComponentsInChildren<UIView>(true);
            foreach (var view in views)
            {
                RegisterView(view);
            }
        }

        /// <summary>
        /// 动态确保场景中存在唯一且与输入系统版本匹配的 EventSystem 环境
        /// </summary>
        private void EnsureEventSystem()
        {
            var eventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (eventSystem == null)
            {
                var esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

                if (inputModuleType != null)
                {
                    esObj.AddComponent(inputModuleType);
                    Debug.Log("<color=#3FB950><b>[UIManager]</b></color> Dynamically created EventSystem with InputSystemUIInputModule (New Input System).");
                }
                else
                {
                    esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    Debug.Log("<color=#3FB950><b>[UIManager]</b></color> Dynamically created EventSystem with StandaloneInputModule (Legacy Input System).");
                }

                // 使其作为 UIManager 的子物体，随 DontDestroyOnLoad 自动常驻
                esObj.transform.SetParent(transform);
            }
            else
            {
                // 如果已存在 EventSystem，检查是否缺少 InputModule
                var inputModule = eventSystem.GetComponent<UnityEngine.EventSystems.BaseInputModule>();
                if (inputModule == null)
                {
                    if (inputModuleType != null)
                    {
                        eventSystem.gameObject.AddComponent(inputModuleType);
                        Debug.Log("<color=#3FB950><b>[UIManager]</b></color> Existing EventSystem had no InputModule. Attached InputSystemUIInputModule.");
                    }
                    else
                    {
                        eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                        Debug.Log("<color=#3FB950><b>[UIManager]</b></color> Existing EventSystem had no InputModule. Attached StandaloneInputModule.");
                    }
                }
                else if (inputModuleType != null && inputModule.GetType().Name == "StandaloneInputModule")
                {
                    // 在新输入系统环境下，如果是老的 StandaloneInputModule，点击事件会被忽略，自动升级
                    Destroy(inputModule);
                    eventSystem.gameObject.AddComponent(inputModuleType);
                    Debug.Log("<color=#BC8CFF><b>[UIManager]</b></color> Upgraded existing StandaloneInputModule to InputSystemUIInputModule for compatibility.");
                }
            }
        }

        public void RegisterView(UIView view)
        {
            if (view == null) return;
            if (!_views.ContainsKey(view.viewID))
            {
                _views.Add(view.viewID, view);
            }
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
                if (addToHistory && _history.Count > 0 && _history.Peek() != view)
                {
                    // 可以根据需要决定是否关闭当前最顶层的视图
                }
                
                view.Open();
                if (addToHistory) _history.Push(view);
                Debug.Log($"<color=#BC8CFF><b>[UI]</b></color> Opening View: {id}");
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
