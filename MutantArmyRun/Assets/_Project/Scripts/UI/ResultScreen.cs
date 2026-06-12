using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MutantArmy.UI
{
    /// <summary>
    /// SCR-04/05 — tela de resultado (doc 12 §4.13, doc 09 §4.4/§4.5). É PASSIVA:
    /// todas as propriedades são preenchidas por sistema externo via Bind() ANTES de
    /// exibir — a tela não calcula nada, só mostra. O número principal é o DELTA
    /// ("+100 moedas"), nunca o total da carteira. O botão "DOBRAR ×2" NÃO concede
    /// nada: só dispara DoubleRequested — a camada que enxerga Services mostra o
    /// rewarded (AdsManager.ShowRewarded(AdPlacement.DoubleReward)) e, no sucesso,
    /// credita via EconomySystem.GrantRunDouble e chama ConfirmDoubled() aqui
    /// (CANON §11: rewarded sempre opcional, recompensa só após o anúncio).
    /// </summary>
    public class ResultScreen : UIScreen
    {
        [SerializeField] private Image _headerBg;               // faixa do header: verde vitória / vermelho derrota
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _coinsDeltaText;      // "+100" — DELTA, nunca o total
        [SerializeField] private TMP_Text _xpDeltaText;
        [SerializeField] private TMP_Text _survivorsText;
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private GameObject _perfectBadge;      // selo PERFECT (doc 09 §4.4)
        [SerializeField] private Button _doubleButton;
        [SerializeField] private TMP_Text _doubleButtonLabel;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _homeButton;

        /// <summary>Disparado pelo botão "PRÓXIMA FASE" — quem orquestra a transição é externo.</summary>
        public event Action NextRequested;

        /// <summary>Disparado pelo link "voltar ao início".</summary>
        public event Action HomeRequested;

        /// <summary>
        /// Disparado pelo botão "DOBRAR ×2" com o delta base da corrida. A tela NÃO concede
        /// nada: a camada externa mostra o rewarded e, se concedido, credita via
        /// EconomySystem.GrantRunDouble(delta) e confirma com <see cref="ConfirmDoubled"/>;
        /// se falhou/abandonado, chama <see cref="CancelDoubleRequest"/>.
        /// </summary>
        public event Action<long> DoubleRequested;

        private readonly StringBuilder _sb = new StringBuilder(32);
        private long _coinsDelta;
        private bool _doubled;
        private bool _doublePending;   // rewarded em exibição: trava o botão sem conceder

        // Código de cores reservado do doc 01 §6.5: verde = ganho, vermelho = perda.
        private static readonly Color VictoryHeader = new Color(0.22f, 0.72f, 0.35f);
        private static readonly Color DefeatHeader = new Color(0.85f, 0.25f, 0.28f);

        protected override void Awake()
        {
            base.Awake();
            if (_doubleButton != null) _doubleButton.onClick.AddListener(OnDoubleClicked);
            if (_nextButton != null) _nextButton.onClick.AddListener(RaiseNext);
            if (_homeButton != null) _homeButton.onClick.AddListener(RaiseHome);
        }

        /// <summary>
        /// Preenche a tela com o resultado JÁ comitado (listeners de dados rodam antes
        /// da transição de tela — doc 12 §3.2). Derrota: delta de moedas é 0 (descartadas),
        /// mas a XP aparece sempre (RunWallet comita XP integral, doc 12 §4.6).
        /// </summary>
        public void Bind(bool won, long coinsDelta, int xpDelta, int survivors, long damageDealt, bool perfect)
        {
            _coinsDelta = coinsDelta;
            _doubled = false;
            _doublePending = false;

            if (_titleText != null) _titleText.text = won ? "VITÓRIA!" : "DERROTA...";
            if (_headerBg != null) _headerBg.color = won ? VictoryHeader : DefeatHeader;
            RenderCoinsDelta(coinsDelta);

            if (_xpDeltaText != null)
            {
                _sb.Length = 0;
                _sb.Append('+').Append(xpDelta).Append(" XP");
                _xpDeltaText.SetText(_sb);
            }

            if (_survivorsText != null)
            {
                _sb.Length = 0;
                _sb.Append("Sobreviventes: ").Append(survivors);
                _survivorsText.SetText(_sb);
            }

            if (_damageText != null)
            {
                _sb.Length = 0;
                _sb.Append("Dano causado: ").Append(damageDealt);
                _damageText.SetText(_sb);
            }

            if (_perfectBadge != null) _perfectBadge.SetActive(won && perfect);

            if (_doubleButton != null)
            {
                // Só vitória com ganho dobrável mostra o botão; o gate de fill de
                // rewarded entra por SetDoubleAvailable (nunca botão morto, doc 12 §7.3).
                _doubleButton.gameObject.SetActive(won && coinsDelta > 0);
                _doubleButton.interactable = true;
            }
            if (_doubleButtonLabel != null && coinsDelta > 0)
            {
                // "x2" ASCII: ✓/× fora do Latin-1 viram glifo ausente nas fontes SDF.
                _sb.Length = 0;
                _sb.Append("DOBRAR x2  +").Append(coinsDelta);
                _doubleButtonLabel.SetText(_sb);
            }
        }

        /// <summary>
        /// Chamado pela camada de cima quando o rewarded está (in)disponível:
        /// sem fill, o botão SOME — nunca prometer e falhar (doc 12 §7.3).
        /// </summary>
        public void SetDoubleAvailable(bool available)
        {
            if (_doubleButton == null) return;
            _doubleButton.gameObject.SetActive(available && !_doubled && _coinsDelta > 0);
        }

        /// <summary>
        /// Confirmação da camada externa: o rewarded foi assistido e o crédito
        /// (EconomySystem.GrantRunDouble) já aconteceu — só AGORA a tela vira "✓ DOBRADO".
        /// </summary>
        public void ConfirmDoubled()
        {
            _doublePending = false;
            if (_doubled) return;
            _doubled = true;
            RenderCoinsDelta(_coinsDelta * 2);
            if (_doubleButtonLabel != null) _doubleButtonLabel.text = "DOBRADO!";
            if (_doubleButton != null) _doubleButton.interactable = false;
        }

        /// <summary>Rewarded falhou/abandonado: destrava o botão sem conceder nada.</summary>
        public void CancelDoubleRequest()
        {
            _doublePending = false;
            if (_doubleButton != null) _doubleButton.interactable = !_doubled;
        }

        private void OnDoubleClicked()
        {
            if (_doubled || _doublePending) return;   // guard anti duplo-clique
            _doublePending = true;

            // A tela é PASSIVA: nada é creditado nem exibido como dobrado aqui — só o
            // pedido sobe; a recompensa depende do rewarded completar (CANON §11).
            if (_doubleButton != null) _doubleButton.interactable = false;
            if (DoubleRequested != null) DoubleRequested(_coinsDelta);
        }

        private void RenderCoinsDelta(long delta)
        {
            if (_coinsDeltaText == null) return;
            _sb.Length = 0;
            _sb.Append('+').Append(delta);
            _coinsDeltaText.SetText(_sb);
        }

        private void RaiseNext()
        {
            if (NextRequested != null) NextRequested();
        }

        private void RaiseHome()
        {
            if (HomeRequested != null) HomeRequested();
        }
    }
}
