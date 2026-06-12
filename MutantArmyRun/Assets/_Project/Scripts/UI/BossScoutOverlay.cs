using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// OVL-01 — Boss Scout card (CANON §3.1, doc 09 §5.1): cartão de ~2 s mostrando o
    /// boss da fase, seu elemento e sua fraqueza ("FRACO CONTRA FOGO"). Auto-dismiss;
    /// qualquer toque pula. Fraqueza com NOME do elemento, nunca só cor (doc 09 P7).
    /// </summary>
    public class BossScoutOverlay : UIOverlay
    {
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private TMP_Text _elementText;
        [SerializeField] private TMP_Text _weaknessText;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private Image _portrait;
        [SerializeField] private Image _elementIcon;    // orbe tintado pela cor do elemento do boss
        [SerializeField] private Image _weaknessIcon;   // orbe tintado pela cor da FRAQUEZA (reforça o texto)
        [SerializeField] private Image _timerFill;      // anel radial: esvazia junto com o auto-dismiss
        [SerializeField] private Button _skipButton;    // botão fullscreen: qualquer toque pula

        private Coroutine _autoHide;
        private Action _onDone;
        private bool _finished;

        protected override void Awake()
        {
            base.Awake();
            if (_skipButton != null) _skipButton.onClick.AddListener(Finish);
        }

        /// <summary>Mostra o cartão por <paramref name="seconds"/>; onDone após fechar (1× garantido).</summary>
        public void Play(BossConfigSO boss, float seconds, Action onDone)
        {
            _onDone = onDone;
            _finished = false;
            Bind(boss);
            Show(() =>
            {
                if (_autoHide != null) StopCoroutine(_autoHide);
                _autoHide = StartCoroutine(AutoHideRoutine(seconds));
            });
        }

        /// <summary>
        /// Pulo PROGRAMÁTICO do cartão (AutoPilot/PlayMode em batchmode): mesmo caminho do
        /// toque — fecha 1× e dispara o onDone (que leva BossScout→Running). Necessário
        /// porque o auto-dismiss roda em tempo UNSCALED (Time.timeScale não acelera o cartão).
        /// </summary>
        public void Skip()
        {
            Finish();
        }

        /// <summary>Preenche o cartão a partir do dado — a UI nunca inventa conteúdo.</summary>
        public void Bind(BossConfigSO boss)
        {
            if (boss == null) return;
            if (_bossNameText != null) _bossNameText.text = DisplayName(boss);
            if (_elementText != null) _elementText.text = "ELEMENTO: " + ElementNamePt(boss.element);
            if (_weaknessText != null) _weaknessText.text = WeaknessLine(boss);
            if (_hintText != null) _hintText.text = HintLine(boss);
            if (_portrait != null)
            {
                _portrait.sprite = boss.scoutCardArt;
                _portrait.enabled = boss.scoutCardArt != null;
            }
            if (_elementIcon != null)
            {
                _elementIcon.color = ElementColorPt(boss.element);
                _elementIcon.enabled = _elementIcon.sprite != null;
            }
            if (_weaknessIcon != null)
            {
                bool hasWeakness = boss.weaknesses != null && boss.weaknesses.Length > 0;
                if (hasWeakness) _weaknessIcon.color = ElementColorPt(boss.weaknesses[0]);
                _weaknessIcon.enabled = hasWeakness && _weaknessIcon.sprite != null;
            }
            if (_timerFill != null) _timerFill.fillAmount = 1f;
        }

        public static string ElementNamePt(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "FOGO";
                case ElementType.Ice: return "GELO";
                case ElementType.Lightning: return "RAIO";
                case ElementType.Poison: return "VENENO";
                case ElementType.Light: return "LUZ";
                case ElementType.Shadow: return "SOMBRA";
                case ElementType.Metal: return "METAL";
                case ElementType.Alien: return "ALIEN";
                default: return "SEM ELEMENTO";
            }
        }

        /// <summary>
        /// Cor canônica por elemento (doc 09 P7: a COR reforça, o NOME informa — nunca
        /// só cor). Tons vivos para tintar orbes/ícones sobre o cartão escuro.
        /// </summary>
        public static Color ElementColorPt(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1.00f, 0.45f, 0.15f);
                case ElementType.Ice: return new Color(0.45f, 0.85f, 1.00f);
                case ElementType.Lightning: return new Color(1.00f, 0.92f, 0.25f);
                case ElementType.Poison: return new Color(0.55f, 0.90f, 0.25f);
                case ElementType.Light: return new Color(1.00f, 0.97f, 0.75f);
                case ElementType.Shadow: return new Color(0.58f, 0.40f, 0.88f);
                case ElementType.Metal: return new Color(0.75f, 0.78f, 0.85f);
                case ElementType.Alien: return new Color(0.40f, 1.00f, 0.65f);
                default: return new Color(0.70f, 0.70f, 0.75f);
            }
        }

        private IEnumerator AutoHideRoutine(float seconds)
        {
            // unscaled: o cartão fecha no tempo certo mesmo se o jogo congelar timeScale.
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                // Timer circular esvaziando: comunica o auto-dismiss sem texto (doc 09 §5.1).
                if (_timerFill != null && seconds > 0f)
                    _timerFill.fillAmount = Mathf.Clamp01(1f - t / seconds);
                yield return null;
            }
            Finish();
        }

        private void Finish()
        {
            if (_finished) return;     // toque + auto-hide no mesmo frame: fecha 1× só
            _finished = true;
            if (_autoHide != null)
            {
                StopCoroutine(_autoHide);
                _autoHide = null;
            }
            Action done = _onDone;
            _onDone = null;
            Hide(() =>
            {
                if (done != null) done();
            });
        }

        private static string DisplayName(BossConfigSO boss)
        {
            string raw = string.IsNullOrEmpty(boss.displayNameKey) ? boss.bossId : boss.displayNameKey;
            return string.IsNullOrEmpty(raw) ? "BOSS" : raw.ToUpperInvariant();
        }

        private static string WeaknessLine(BossConfigSO boss)
        {
            if (boss.weaknesses == null || boss.weaknesses.Length == 0)
                return "SEM FRAQUEZA ELEMENTAL";

            var sb = new StringBuilder("FRACO CONTRA ");
            for (int i = 0; i < boss.weaknesses.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(ElementNamePt(boss.weaknesses[i]));
            }
            return sb.ToString();
        }

        private static string HintLine(BossConfigSO boss)
        {
            if (boss.weaknesses == null || boss.weaknesses.Length == 0)
                return "Capriche na quantidade e no Supply!";
            return "Priorize portais de " + ElementNamePt(boss.weaknesses[0]) + "!";
        }
    }
}
