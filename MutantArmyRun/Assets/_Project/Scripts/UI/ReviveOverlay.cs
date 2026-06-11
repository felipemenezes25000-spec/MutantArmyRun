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
    /// </summary>
    public class ReviveOverlay : UIOverlay
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _reviveButton;
        [SerializeField] private TMP_Text _reviveLabel;
        [SerializeField] private Button _declineButton;

        private bool _pending;   // rewarded em exibição: trava os dois botões

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
                Resolve(granted);   // o Pop do estado fecha este overlay via UIManager
            });
        }

        private void OnDeclineClicked()
        {
            if (_pending) return;
            Resolve(false);
        }

        private static void Resolve(bool revived)
        {
            if (GameManager.Instance != null) GameManager.Instance.ResolveRevive(revived);
        }
    }
}
