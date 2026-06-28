using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace UISystem.Runtime
{
    /// <summary>
    /// 工业级 UI 视图基类 (Modular UI View)
    /// 处理生命周期、显示/隐藏动画以及画布组管理。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIView : MonoBehaviour
    {
        [Header("View Settings")]
        [FormerlySerializedAs("viewID")]
        [SerializeField] private string _viewId;
        [FormerlySerializedAs("hideOnAwake")]
        [SerializeField] private bool _hideOnAwake = true;
        [FormerlySerializedAs("fadeDuration")]
        [Min(0f)]
        [SerializeField] private float _fadeDuration = 0.25f;

        [Header("Events")]
        [FormerlySerializedAs("OnBeforeOpen")]
        [SerializeField] private UnityEvent _onBeforeOpen = new UnityEvent();
        [FormerlySerializedAs("OnAfterClose")]
        [SerializeField] private UnityEvent _onAfterClose = new UnityEvent();

        protected CanvasGroup canvasGroup;
        protected bool isVisible;
        private Coroutine _fadeRoutine;
        private bool _isInitialized;

        public bool IsVisible => isVisible;
        public string ViewId => _viewId;
        public bool HideOnAwake => _hideOnAwake;
        public float FadeDuration => _fadeDuration;
        public UnityEvent OnBeforeOpen => _onBeforeOpen;
        public UnityEvent OnAfterClose => _onAfterClose;

        [System.Obsolete("Use ViewId for reading and SetViewId for writing.")]
        public string viewID
        {
            get => _viewId;
            set => _viewId = value;
        }

        [System.Obsolete("Use HideOnAwake for reading and SetHideOnAwake for writing.")]
        public bool hideOnAwake
        {
            get => _hideOnAwake;
            set => _hideOnAwake = value;
        }

        [System.Obsolete("Use FadeDuration for reading and SetFadeDuration for writing.")]
        public float fadeDuration
        {
            get => _fadeDuration;
            set => _fadeDuration = Mathf.Max(0f, value);
        }

        protected virtual void Awake()
        {
            EnsureInitialized();
            
            if (_hideOnAwake)
            {
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                isVisible = false;
            }
        }

        public void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            canvasGroup = GetComponent<CanvasGroup>();
            if (string.IsNullOrWhiteSpace(_viewId))
                _viewId = gameObject.name;

            _isInitialized = true;
        }

        public void SetViewId(string id)
        {
            _viewId = id;
        }

        public void SetHideOnAwake(bool value)
        {
            _hideOnAwake = value;
        }

        public void SetFadeDuration(float duration)
        {
            _fadeDuration = Mathf.Max(0f, duration);
        }

        public virtual void Open()
        {
            EnsureInitialized();
            if (isVisible && canvasGroup.alpha >= 0.999f)
                return;

            gameObject.SetActive(true);
            StopFadeRoutine();
            isVisible = true;
            _onBeforeOpen?.Invoke();
            _fadeRoutine = StartCoroutine(FadeRoutine(1.0f, true));
        }

        public virtual void Close()
        {
            EnsureInitialized();
            if (!isVisible && canvasGroup.alpha <= 0.001f)
                return;

            StopFadeRoutine();
            isVisible = false;
            _fadeRoutine = StartCoroutine(FadeRoutine(0.0f, false));
        }

        private IEnumerator FadeRoutine(float targetAlpha, bool show)
        {
            float startAlpha = canvasGroup.alpha;
            float time = 0;
            float duration = Mathf.Max(0f, _fadeDuration);

            if (show)
            {
                gameObject.SetActive(true);
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            while (duration > 0f && time < duration)
            {
                time += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            isVisible = show;

            if (!show)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                gameObject.SetActive(false);
                _onAfterClose?.Invoke();
            }

            _fadeRoutine = null;
        }

        private void StopFadeRoutine()
        {
            if (_fadeRoutine == null)
                return;

            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }
}
