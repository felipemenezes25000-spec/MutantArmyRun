using System;
using System.Collections;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Base abstrata de tela cheia (SCR-01..09, doc 09 §2.1). Entra/sai com slide de
    /// 200 ms + fade (doc 09 §6), SEMPRE em unscaled time — a coreografia roda mesmo
    /// com timeScale 0 ou durante o slow motion canônico (doc 12 §4.13).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private float _transitionSeconds = 0.2f;   // slide 200 ms (doc 09 §6)
        [SerializeField] private float _slideOffsetX = 90f;

        private CanvasGroup _group;
        private RectTransform _rect;
        private Vector2 _restPosition;
        private bool _restCaptured;
        private Coroutine _transition;

        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            EnsureRefs();
        }

        public void Show(Action onDone = null)
        {
            EnsureRefs();
            CaptureRestPosition();
            gameObject.SetActive(true);
            IsVisible = true;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            StartTransition(_restPosition + new Vector2(_slideOffsetX, 0f), _restPosition, 0f, 1f,
                            deactivateOnEnd: false,
                            onDone: () =>
                            {
                                OnShown();
                                if (onDone != null) onDone();
                            });
        }

        public void Hide(Action onDone = null)
        {
            EnsureRefs();
            if (!gameObject.activeSelf)
            {
                IsVisible = false;
                if (onDone != null) onDone();
                return;
            }

            CaptureRestPosition();
            IsVisible = false;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            StartTransition(_rect.anchoredPosition, _restPosition - new Vector2(_slideOffsetX, 0f), _group.alpha, 0f,
                            deactivateOnEnd: true,
                            onDone: () =>
                            {
                                OnHidden();
                                if (onDone != null) onDone();
                            });
        }

        /// <summary>Hook chamado quando a animação de entrada termina.</summary>
        protected virtual void OnShown() { }

        /// <summary>Hook chamado quando a animação de saída termina (objeto já inativo).</summary>
        protected virtual void OnHidden() { }

        private void EnsureRefs()
        {
            // Lazy: a tela pode começar inativa na cena (Awake ainda não rodou).
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_rect == null) _rect = (RectTransform)transform;
        }

        private void CaptureRestPosition()
        {
            if (_restCaptured) return;
            _restPosition = _rect.anchoredPosition;
            _restCaptured = true;
        }

        private void StartTransition(Vector2 from, Vector2 to, float alphaFrom, float alphaTo,
                                     bool deactivateOnEnd, Action onDone)
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(from, to, alphaFrom, alphaTo, deactivateOnEnd, onDone));
        }

        private IEnumerator TransitionRoutine(Vector2 from, Vector2 to, float alphaFrom, float alphaTo,
                                              bool deactivateOnEnd, Action onDone)
        {
            float t = 0f;
            while (t < _transitionSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = _transitionSeconds > 0f ? Mathf.Clamp01(t / _transitionSeconds) : 1f;
                k = 1f - (1f - k) * (1f - k);   // ease-out quadrático
                _rect.anchoredPosition = Vector2.Lerp(from, to, k);
                _group.alpha = Mathf.Lerp(alphaFrom, alphaTo, k);
                yield return null;
            }

            _rect.anchoredPosition = to;
            _group.alpha = alphaTo;
            _transition = null;
            if (deactivateOnEnd) gameObject.SetActive(false);
            if (onDone != null) onDone();
        }
    }
}
