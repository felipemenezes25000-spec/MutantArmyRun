using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;

namespace MutantArmy.UI
{
    /// <summary>
    /// OVL-05 — oferta de revive no boss (CANON §11: 1×/fase, sempre opcional). Aberto/
    /// fechado pelo UIManager espelhando o estado ReviveOffer (doc 12 §4.13). O botão de
    /// reviver só RENDERIZA com fill de rewarded pronto (doc 12 §7.3 — nunca botão morto;
    /// com NullAdsProvider só o "DESISTIR" aparece). Aceitar mostra o rewarded via hook do
    /// blackboard (UI não referencia Services, doc 12 §2.3) e responde ao GameManager com
    /// ResolveRevive(granted) — recusa/falha resolve false e o estado vira Defeat.
    /// Painel URGENTE: countdown em unscaled time (número grande + anel esvaziando);
    /// zerar = desistir automático — a oferta nunca fica pendurada (doc 09 §5.5). O timer
    /// PAUSA enquanto o rewarded está em exibição (_pending).
    /// </summary>
    public class ReviveOverlay : UIOverlay
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _reviveButton;
        [SerializeField] private TMP_Text _reviveLabel;
        [SerializeField] private Button _declineButton;
        [SerializeField] private TMP_Text _countdownText;       // segundos restantes, número grande
        [SerializeField] private Image _timerFill;               // anel radial em volta do número
        [SerializeField] private float _countdownSeconds = 5f;   // ≤0 desliga o auto-decline

        private bool _pending;   // rewarded em exibição: trava os dois botões
        private Coroutine _countdown;

        protected override void Awake()
        {
            base.Awake();
            if (_reviveButton != null) _reviveButton.onClick.AddListener(OnReviveClicked);
            if (_declineButton != null) _declineButton.onClick.AddListener(OnDeclineClicked);
        }

        private void OnEnable()
        {
            _pending = false;
            GameBootstrap root = GameBootstrap.Current;
            bool ready = root != null && root.RewardedAdReady != null && root.RewardedAdReady();
            if (_reviveButton != null)
            {
                _reviveButton.gameObject.SetActive(ready);   // sem fill, o botão SOME (doc 12 §7.3)
                _reviveButton.interactable = true;
            }
            if (_declineButton != null) _declineButton.interactable = true;
            if (_titleText != null) _titleText.text = "EXÉRCITO DERROTADO";
            if (_reviveLabel != null) _reviveLabel.text = "REVIVER (ANÚNCIO)";

            if (_countdown != null) StopCoroutine(_countdown);
            if (_countdownSeconds > 0f) _countdown = StartCoroutine(CountdownRoutine());
        }

        /// <summary>
        /// Urgência visível: número + anel esvaziando em UNSCALED time (o overlay abre com
        /// o jogo pausado/derrotado). Zerou sem decisão = Resolve(false) — mesmo caminho
        /// do DESISTIR; pausa enquanto o rewarded está aberto para nunca roubar o prêmio.
        /// </summary>
        private IEnumerator CountdownRoutine()
        {
            float remaining = _countdownSeconds;
            RenderCountdown(remaining);
            while (remaining > 0f)
            {
                yield return null;
                if (_pending) continue;          // rewarded aberto: timer congela
                remaining -= Time.unscaledDeltaTime;
                RenderCountdown(remaining);
            }
            _countdown = null;
            if (!_pending) Resolve(false);       // tempo esgotado = desistiu (CANON §11: opcional)
        }

        private void RenderCountdown(float remaining)
        {
            if (_countdownText != null)
                _countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
            if (_timerFill != null && _countdownSeconds > 0f)
                _timerFill.fillAmount = Mathf.Clamp01(remaining / _countdownSeconds);
        }

        private void OnReviveClicked()
        {
            if (_pending) return;
            GameBootstrap root = GameBootstrap.Current;
            if (root == null || root.ShowRewardedAd == null)
            {
                Resolve(false);
                return;
            }

            _pending = true;
            if (_reviveButton != null) _reviveButton.interactable = false;
            if (_declineButton != null) _declineButton.interactable = false;
            root.ShowRewardedAd(AdPlacement.ReviveBoss, granted =>
            {
                _pending = false;
                StopCountdown();    // decisão tomada: o timer não pode re-resolver no fade
                Resolve(granted);   // o Pop do estado fecha este overlay via UIManager
            });
        }

        private void OnDeclineClicked()
        {
            if (_pending) return;
            StopCountdown();
            Resolve(false);
        }

        private void StopCountdown()
        {
            if (_countdown == null) return;
            StopCoroutine(_countdown);
            _countdown = null;
        }

        private static void Resolve(bool revived)
        {
            if (GameManager.Instance != null) GameManager.Instance.ResolveRevive(revived);
        }
    }
}
