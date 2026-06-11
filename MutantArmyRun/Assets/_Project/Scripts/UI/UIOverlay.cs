using System;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Base abstrata de overlay modal (OVL-01..06, doc 09 §2.2). Sobe SOBRE a tela
    /// atual com fade de 150 ms (doc 09 §6) em unscaled time — a tela de baixo não
    /// sai da pilha do UIManager (doc 12 §4.13).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIOverlay : MonoBehaviour
    {
        [SerializeField] private float _fadeSeconds = 0.15f;   // fade 150 ms (doc 09 §6)

        private CanvasGroup _group;
        private Coroutine _fade;

        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            EnsureGroup();
        }

        public void Show(Action onDone = null)
        {
            EnsureGroup();
            gameObject.SetActive(true);
            IsVisible = true;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            Restart(UIUtils.FadeRoutine(_group, _group.alpha, 1f, _fadeSeconds, () =>
            {
                OnShown();
                if (onDone != null) onDone();
            }));
        }

        public void Hide(Action onDone = null)
        {
            EnsureGroup();
            if (!gameObject.activeSelf)
            {
                IsVisible = false;
                if (onDone != null) onDone();
                return;
            }

            IsVisible = false;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Restart(UIUtils.FadeRoutine(_group, _group.alpha, 0f, _fadeSeconds, () =>
            {
                gameObject.SetActive(false);
                OnHidden();
                if (onDone != null) onDone();
            }));
        }

        /// <summary>Hook chamado quando o fade de entrada termina.</summary>
        protected virtual void OnShown() { }

        /// <summary>Hook chamado quando o fade de saída termina (objeto já inativo).</summary>
        protected virtual void OnHidden() { }

        private void EnsureGroup()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
        }

        private void Restart(System.Collections.IEnumerator routine)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(routine);
        }
    }
}
