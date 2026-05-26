using System.Collections;
using UnityEngine;
using UnityEngine.Events;

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
        public string viewID;
        public bool hideOnAwake = true;
        public float fadeDuration = 0.25f;

        [Header("Events")]
        public UnityEvent OnBeforeOpen;
        public UnityEvent OnAfterClose;

        protected CanvasGroup canvasGroup;
        protected bool isVisible;

        public bool IsVisible => isVisible;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (string.IsNullOrEmpty(viewID)) viewID = gameObject.name;
            
            if (hideOnAwake)
            {
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                isVisible = false;
            }
        }

        public virtual void Open()
        {
            if (isVisible) return;
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(1.0f, true));
            OnBeforeOpen?.Invoke();
        }

        public virtual void Close()
        {
            if (!isVisible) return;
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(0.0f, false));
        }

        private IEnumerator FadeRoutine(float targetAlpha, bool show)
        {
            float startAlpha = canvasGroup.alpha;
            float time = 0;

            if (show)
            {
                gameObject.SetActive(true);
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            isVisible = show;

            if (!show)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                gameObject.SetActive(false);
                OnAfterClose?.Invoke();
            }
        }
    }
}
