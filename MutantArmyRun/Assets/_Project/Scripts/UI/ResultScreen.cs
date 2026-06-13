using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

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
    ///
    /// Missão Nota 10: a vitória ganhou três extras passivos — linhas de combo
    /// (BindCombos, com reveal em stagger), teaser do próximo boss em silhueta
    /// (BindNextBoss) e badge de boss raro derrotado (SetRareBossDefeated). Os campos
    /// novos são serializados (a factory liga na Onda 4) MAS têm fallback de
    /// construção em código — a tela funciona greybox mesmo sem re-rodar a factory.
    /// </summary>
    public class ResultScreen : UIScreen
    {
        [SerializeField] private Image _headerBg;               // faixa do header: verde vitória / vermelho derrota
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _reasonText;          // dica da derrota (doc 09 §4.5); vazio na vitória
        [SerializeField] private TMP_Text _coinsDeltaText;      // "+100" — DELTA, nunca o total
        [SerializeField] private TMP_Text _xpDeltaText;
        [SerializeField] private TMP_Text _survivorsText;
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private GameObject _perfectBadge;      // selo PERFECT (doc 09 §4.4)
        [SerializeField] private Button _doubleButton;
        [SerializeField] private TMP_Text _doubleButtonLabel;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _homeButton;

        [Header("Missão Nota 10 (factory liga na Onda 4; nulos = fallback construído em código)")]
        [SerializeField] private RectTransform _combosContent;   // pilha das linhas "+25 PORTAIS PERFEITOS!"
        [SerializeField] private GameObject _rareBadge;          // badge "BOSS RARO DERROTADO! x3"
        [SerializeField] private GameObject _nextBossPanel;      // teaser do próximo boss (só vitória)
        [SerializeField] private Image _nextBossSilhouette;      // scoutCardArt tintado escuro = silhueta
        [SerializeField] private TMP_Text _nextBossText;
        [SerializeField] private TMP_Text _nextButtonLabel;      // rótulo do CTA: PRÓXIMA FASE / TENTAR DE NOVO

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
        private long _coinsDelta;       // TOTAL exibido na vitória (recompensa de fase + corrida)
        private long _doubleBase;       // SÓ as moedas da corrida — o que o "DOBRAR x2" acrescenta
        private bool _doubled;
        private bool _doublePending;   // rewarded em exibição: trava o botão sem conceder

        // Extras da missão: linhas de combo reutilizadas entre corridas (nunca Destroy) e a
        // fila de reveal — grupos aparecem em STAGGER quando a tela termina de entrar (OnShown).
        private readonly List<ComboLine> _comboLines = new List<ComboLine>(6);
        private readonly List<CanvasGroup> _revealQueue = new List<CanvasGroup>(8);
        private Coroutine _reveal;
        private bool _auxBuilt;
        private bool _combosFallbackLayout;   // container nasceu em código: posiciona as linhas à mão

        // Código de cores reservado do doc 01 §6.5: verde = ganho, vermelho = perda.
        private static readonly Color VictoryHeader = new Color(0.22f, 0.72f, 0.35f);
        private static readonly Color DefeatHeader = new Color(0.85f, 0.25f, 0.28f);
        private static readonly Color ChipBg = new Color(0.10f, 0.12f, 0.20f, 0.92f);
        private static readonly Color Gold = new Color(1.00f, 0.83f, 0.37f);
        private static readonly Color GoldDark = new Color(0.25f, 0.13f, 0.02f);
        private static readonly Color TextSoft = new Color(0.82f, 0.86f, 0.92f);
        private static readonly Color SilhouetteTint = new Color(0.07f, 0.06f, 0.11f, 1f);   // silhueta: forma sim, detalhe não

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
        /// <paramref name="coinsDelta"/> é o TOTAL exibido na vitória (recompensa de fase +
        /// moedas da corrida); <paramref name="doubleBase"/> é SÓ a parcela dobrável pelo
        /// rewarded — as moedas coletadas na corrida (CANON §11). Sem passar, dobra o total.
        /// Os extras (combos/raro/próximo boss) são ZERADOS aqui — quem quiser exibi-los
        /// chama BindCombos/SetRareBossDefeated/BindNextBoss DEPOIS deste Bind.
        /// </summary>
        public void Bind(bool won, long coinsDelta, int xpDelta, int survivors, long damageDealt, bool perfect,
                         long doubleBase = -1L, string defeatReason = null)
        {
            EnsureAuxBuilt();
            ResetMissionExtras();     // corrida nova nunca herda combo/badge/teaser da anterior
            ApplyPrimaryCta(won);

            _coinsDelta = coinsDelta;
            _doubleBase = doubleBase >= 0L ? doubleBase : coinsDelta;
            _doubled = false;
            _doublePending = false;

            if (_headerBg != null) _headerBg.color = won ? VictoryHeader : DefeatHeader;

            // motivo só na derrota (doc 09 §4.5) — agora é DICA que ensina (missão Nota 10),
            // nunca bronca. Na vitória o campo some — o número grande é o ganho.
            bool showReason = !won && !string.IsNullOrEmpty(defeatReason);
            if (_reasonText != null)
            {
                _reasonText.gameObject.SetActive(showReason);
                if (showReason) _reasonText.text = defeatReason;
                if (_titleText != null) _titleText.text = won ? "VITÓRIA!" : "DERROTA...";
            }
            else if (_titleText != null)
            {
                // sem campo de motivo dedicado: dobra o motivo no título para nunca ficar mudo
                // (a tela é só preenchida por fora — funciona com qualquer prefab de ResultScreen).
                _titleText.text = won ? "VITÓRIA!"
                    : (showReason ? "DERROTA — " + defeatReason : "DERROTA...");
            }

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
                // Só vitória com ganho DOBRÁVEL (moedas da corrida) mostra o botão; a
                // recompensa de fase não é dobrável. Gate de fill entra por
                // SetDoubleAvailable (nunca botão morto, doc 12 §7.3).
                _doubleButton.gameObject.SetActive(won && _doubleBase > 0);
                _doubleButton.interactable = true;
            }
            if (_doubleButtonLabel != null && _doubleBase > 0)
            {
                // "x2" ASCII: ✓/× fora do Latin-1 viram glifo ausente nas fontes SDF.
                // O "+N" mostra o que o rewarded ACRESCENTA (a parcela dobrável), não o total.
                _sb.Length = 0;
                _sb.Append("DOBRAR x2  +").Append(_doubleBase);
                _doubleButtonLabel.SetText(_sb);
            }
        }

        /// <summary>
        /// Linhas de combo da corrida ("+25 PORTAIS PERFEITOS!") — 1 linha por ComboEarned,
        /// na ordem recebida, com reveal em stagger quando a tela entra. A tela continua
        /// passiva: o GameUIController acumula OnComboEarned e só passa a lista aqui.
        /// </summary>
        public void BindCombos(IReadOnlyList<ComboEarned> combos)
        {
            EnsureAuxBuilt();
            for (int i = 0; i < _comboLines.Count; i++)
                if (_comboLines[i].root != null) _comboLines[i].root.SetActive(false);

            if (combos == null || combos.Count == 0 || _combosContent == null) return;

            for (int i = 0; i < combos.Count; i++)
            {
                ComboLine line = GetOrBuildLine(i);
                if (line.root == null) continue;
                if (line.label != null)
                {
                    // bônus dourado + nome PT-BR curto e gritante (doc 09 §4.2).
                    _sb.Length = 0;
                    _sb.Append("<color=#FFD45E>+").Append(combos[i].bonusCoins).Append("</color>  ")
                       .Append(ComboLabel(combos[i].kind));
                    line.label.SetText(_sb);
                }
                line.root.SetActive(true);
                QueueReveal(line.group);
            }
            RequestReveal();
        }

        /// <summary>
        /// Teaser do PRÓXIMO boss na vitória — nome + elemento + fraqueza, com a arte do
        /// Scout em tinte escuro de silhueta ("quero ver esse de perto" → só mais uma).
        /// next nulo esconde o teaser (derrota não chama; Bind() já o zera).
        /// </summary>
        public void BindNextBoss(BossConfigSO next, bool isWorldBoss)
        {
            EnsureAuxBuilt();
            if (_nextBossPanel == null) return;
            if (next == null)
            {
                _nextBossPanel.SetActive(false);
                return;
            }

            if (_nextBossText != null)
            {
                _sb.Length = 0;
                _sb.Append("<size=58%><color=#FFD45E>");
                if (isWorldBoss)
                {
                    _sb.Append("BOSS DE MUNDO À FRENTE!");
                }
                else
                {
                    _sb.Append("PRÓXIMO BOSS");
                    if (next.element != ElementType.None)
                        _sb.Append(" DE ").Append(BossScoutOverlay.ElementNamePt(next.element));
                }
                _sb.Append("</color></size>\n").Append(BossDisplayName(next));
                _sb.Append("\n<size=66%><color=#FFB13D>").Append(WeaknessLinePt(next)).Append("</color></size>");
                _nextBossText.SetText(_sb);
            }

            if (_nextBossSilhouette != null)
            {
                _nextBossSilhouette.sprite = next.scoutCardArt;
                _nextBossSilhouette.color = SilhouetteTint;
                _nextBossSilhouette.enabled = next.scoutCardArt != null;   // sem arte, sem retângulo preto
            }

            _nextBossPanel.SetActive(true);
            QueueReveal(_nextBossPanel.GetComponent<CanvasGroup>());
            RequestReveal();
        }

        /// <summary>
        /// Badge "BOSS RARO DERROTADO! x3" — a corrida derrubou uma variante rara
        /// (RareBossMath: recompensa ×3). Surpresa justa merece celebração na tela.
        /// </summary>
        public void SetRareBossDefeated(bool defeated)
        {
            EnsureAuxBuilt();
            if (_rareBadge == null) return;
            _rareBadge.SetActive(defeated);
            if (!defeated) return;
            QueueReveal(_rareBadge.GetComponent<CanvasGroup>());   // sem group (wiring antigo): fica estático
            RequestReveal();
        }

        /// <summary>Nome PT-BR curto do combo — "linguagem de jogo", máx. 2 palavras (doc 09 §6).</summary>
        public static string ComboLabel(ComboKind kind)
        {
            switch (kind)
            {
                case ComboKind.PerfectGate: return "PORTAIS PERFEITOS!";
                case ComboKind.WeaknessHit: return "FRAQUEZA EXPLORADA!";
                case ComboKind.BossBreaker: return "BOSS BREAKER!";
                case ComboKind.Clutch: return "POR UM FIO!";
                case ComboKind.NoLoss: return "SEM PERDAS!";
                case ComboKind.Overkill: return "OVERKILL!";
                default: return "COMBO!";
            }
        }

        /// <summary>
        /// Chamado pela camada de cima quando o rewarded está (in)disponível:
        /// sem fill, o botão SOME — nunca prometer e falhar (doc 12 §7.3).
        /// </summary>
        public void SetDoubleAvailable(bool available)
        {
            if (_doubleButton == null) return;
            _doubleButton.gameObject.SetActive(available && !_doubled && _doubleBase > 0);
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
            // Só a parcela da corrida é dobrada: total exibido += o delta base creditado de
            // novo por GrantRunDouble (a recompensa de fase fica intacta — CANON §11).
            RenderCoinsDelta(_coinsDelta + _doubleBase);
            if (_doubleButtonLabel != null) _doubleButtonLabel.text = "DOBRADO!";
            if (_doubleButton != null) _doubleButton.interactable = false;
        }

        /// <summary>Rewarded falhou/abandonado: destrava o botão sem conceder nada.</summary>
        public void CancelDoubleRequest()
        {
            _doublePending = false;
            if (_doubleButton != null) _doubleButton.interactable = !_doubled;
        }

        protected override void OnShown()
        {
            base.OnShown();
            // O reveal dos extras espera a entrada da tela terminar: o stagger nasce com
            // a tela parada, nunca competindo com o slide (Bind roda com o objeto inativo).
            if (_revealQueue.Count > 0 && _reveal == null)
                _reveal = StartCoroutine(RevealRoutine());
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            // O deactivate da base já matou a coroutine — só limpamos os handles; o próximo
            // Bind re-zera estado e re-enfileira o que for exibir.
            _reveal = null;
            _revealQueue.Clear();
        }

        private void OnDoubleClicked()
        {
            if (_doubled || _doublePending) return;   // guard anti duplo-clique
            _doublePending = true;

            // A tela é PASSIVA: nada é creditado nem exibido como dobrado aqui — só o
            // pedido sobe; a recompensa depende do rewarded completar (CANON §11). Sobe a
            // parcela DOBRÁVEL (moedas da corrida) — é o que GrantRunDouble credita de novo.
            if (_doubleButton != null) _doubleButton.interactable = false;
            if (DoubleRequested != null) DoubleRequested(_doubleBase);
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

        // ---------------------------------------------------------------- extras da missão Nota 10

        // CTA primário por resultado: vitória = "PRÓXIMA FASE"; derrota = "TENTAR DE NOVO"
        // com pulso de respiração (UIPulse) — convite ao retry, nunca bronca. O rótulo do
        // NextButton não é wireado pela factory atual: resolve por GetComponentInChildren.
        private void ApplyPrimaryCta(bool won)
        {
            if (_nextButtonLabel == null && _nextButton != null)
                _nextButtonLabel = _nextButton.GetComponentInChildren<TMP_Text>(true);
            if (_nextButtonLabel != null) _nextButtonLabel.text = won ? "PRÓXIMA FASE" : "TENTAR DE NOVO";

            if (_nextButton != null)
            {
                UIPulse pulse = _nextButton.GetComponent<UIPulse>();
                if (!won && pulse == null) pulse = _nextButton.gameObject.AddComponent<UIPulse>();
                if (pulse != null) pulse.enabled = !won;   // vitória: botão quieto (já é o maior do cartão)
            }
        }

        // Zera os extras — chamado no Bind: os BindX externos re-populam o que a corrida mereceu.
        private void ResetMissionExtras()
        {
            if (_reveal != null)
            {
                StopCoroutine(_reveal);
                _reveal = null;
            }
            _revealQueue.Clear();
            for (int i = 0; i < _comboLines.Count; i++)
                if (_comboLines[i].root != null) _comboLines[i].root.SetActive(false);
            if (_rareBadge != null) _rareBadge.SetActive(false);
            if (_nextBossPanel != null) _nextBossPanel.SetActive(false);
        }

        // Fallback greybox dos campos novos (padrão EnsureBuilt das telas de meta): se a
        // factory da Onda 4 ainda não ligou os [SerializeField], a tela constrói tudo em
        // código sobre o próprio painel — referência de layout: cartão 920×1320 no centro
        // do canvas 1080×1920 (ProjectSetup.BuildResultScreen).
        private void EnsureAuxBuilt()
        {
            if (_auxBuilt) return;
            _auxBuilt = true;

            // Dica da derrota: a factory atual NÃO liga _reasonText — sem este rótulo a dica
            // longa ("Veja a fraqueza no Scout!") ficaria espremida no título.
            if (_reasonText == null)
            {
                _reasonText = BuildLabel(transform, "ReasonText", string.Empty, 36f,
                    new Vector2(0f, 445f), new Vector2(820f, 120f), TextSoft, TextAlignmentOptions.Center);
                _reasonText.gameObject.SetActive(false);
            }

            // Pilha de combos acima do cartão (faixa livre y 650..960): 2 colunas × 3 linhas
            // cobrem os 6 ComboKind possíveis sem invadir o header do cartão.
            if (_combosContent == null)
            {
                var go = new GameObject("CombosContent", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, 885f);
                rect.sizeDelta = new Vector2(940f, 240f);
                _combosContent = rect;
                _combosFallbackLayout = true;
            }

            if (_rareBadge == null)
            {
                Image bg = BuildChip(transform, "RareBossBadge", new Vector2(0f, 925f), new Vector2(640f, 62f), Gold);
                BuildLabel(bg.transform, "Label", "BOSS RARO DERROTADO! x3", 34f,
                    Vector2.zero, new Vector2(640f, 62f), GoldDark, TextAlignmentOptions.Center);
                bg.gameObject.AddComponent<CanvasGroup>();
                _rareBadge = bg.gameObject;
                _rareBadge.SetActive(false);
            }

            // Teaser do próximo boss abaixo do cartão (faixa livre y -670..-960).
            if (_nextBossPanel == null)
            {
                Image bg = BuildChip(transform, "NextBossTeaser", new Vector2(0f, -800f), new Vector2(920f, 170f), ChipBg);
                bg.gameObject.AddComponent<CanvasGroup>();

                var silGo = new GameObject("Silhouette", typeof(RectTransform), typeof(Image));
                var silRect = (RectTransform)silGo.transform;
                silRect.SetParent(bg.transform, false);
                silRect.anchorMin = silRect.anchorMax = new Vector2(0.5f, 0.5f);
                silRect.pivot = new Vector2(0.5f, 0.5f);
                silRect.anchoredPosition = new Vector2(-340f, 0f);
                silRect.sizeDelta = new Vector2(140f, 140f);
                _nextBossSilhouette = silGo.GetComponent<Image>();
                _nextBossSilhouette.preserveAspect = true;
                _nextBossSilhouette.raycastTarget = false;
                _nextBossSilhouette.enabled = false;   // só liga com arte no Bind

                _nextBossText = BuildLabel(bg.transform, "Text", string.Empty, 40f,
                    new Vector2(70f, 0f), new Vector2(700f, 160f), Color.white, TextAlignmentOptions.Left);

                _nextBossPanel = bg.gameObject;
                _nextBossPanel.SetActive(false);
            }

            if (_nextButtonLabel == null && _nextButton != null)
                _nextButtonLabel = _nextButton.GetComponentInChildren<TMP_Text>(true);
        }

        // Linha de combo reutilizável (pool simples — telas nunca destroem por corrida).
        private ComboLine GetOrBuildLine(int index)
        {
            while (_comboLines.Count <= index)
            {
                var line = new ComboLine();
                if (_combosContent != null)
                {
                    int i = _comboLines.Count;
                    Image bg = BuildChip(_combosContent, "ComboLine" + i, Vector2.zero, new Vector2(450f, 62f), ChipBg);
                    var rect = (RectTransform)bg.transform;
                    if (_combosFallbackLayout)
                    {
                        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                        rect.anchoredPosition = ComboLinePosition(i);
                    }
                    // LayoutElement: se a factory da Onda 4 trocar o container por um layout
                    // group, as linhas já obedecem sem mudar este código.
                    var le = bg.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = 62f;
                    le.preferredHeight = 62f;
                    le.preferredWidth = 450f;
                    line.root = bg.gameObject;
                    line.group = bg.gameObject.AddComponent<CanvasGroup>();
                    line.label = BuildLabel(bg.transform, "Label", string.Empty, 30f,
                        Vector2.zero, new Vector2(440f, 62f), Color.white, TextAlignmentOptions.Center);
                    bg.gameObject.SetActive(false);
                }
                _comboLines.Add(line);
            }
            return _comboLines[index];
        }

        // Grade 2×3 do fallback (container com pivô no topo): col alternada, linha a cada 70px.
        private static Vector2 ComboLinePosition(int index)
        {
            float x = (index % 2 == 0) ? -235f : 235f;
            float y = -34f - (index / 2) * 70f;
            return new Vector2(x, y);
        }

        // Prepara um grupo para o reveal em stagger: invisível/encolhido até a vez dele.
        private void QueueReveal(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 0f;
            group.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            _revealQueue.Add(group);
        }

        // Dispara o reveal imediatamente SE a tela já está visível (rebind raro); o caminho
        // normal (Bind antes do Push) espera o OnShown.
        private void RequestReveal()
        {
            if (_revealQueue.Count == 0 || _reveal != null) return;
            if (!IsVisible || !isActiveAndEnabled) return;
            _reveal = StartCoroutine(RevealRoutine());
        }

        // Stagger sequencial: cada item entra com pop OutBack + fade (0,22 s) e um respiro
        // de 0,08 s antes do próximo — SEMPRE em unscaled time (doc 12 §4.13).
        private IEnumerator RevealRoutine()
        {
            const float itemSeconds = 0.22f;
            const float gapSeconds = 0.08f;

            for (int i = 0; i < _revealQueue.Count; i++)
            {
                CanvasGroup group = _revealQueue[i];
                if (group == null) continue;
                Transform t = group.transform;

                float elapsed = 0f;
                while (elapsed < itemSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(elapsed / itemSeconds);
                    float s = Mathf.LerpUnclamped(0.7f, 1f, BackOut(k));
                    t.localScale = new Vector3(s, s, 1f);
                    group.alpha = k;
                    yield return null;
                }
                t.localScale = Vector3.one;
                group.alpha = 1f;

                float gap = 0f;
                while (gap < gapSeconds)
                {
                    gap += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            _revealQueue.Clear();
            _reveal = null;
        }

        private static float BackOut(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            k -= 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        // Nome do boss p/ o teaser: displayName PT-BR do factory; fallback humaniza a key
        // (mesma rede de segurança do BossScoutOverlay).
        private static string BossDisplayName(BossConfigSO boss)
        {
            if (!string.IsNullOrEmpty(boss.displayName)) return boss.displayName.ToUpperInvariant();
            string raw = string.IsNullOrEmpty(boss.displayNameKey) ? boss.bossId : boss.displayNameKey;
            if (string.IsNullOrEmpty(raw)) return "BOSS";
            if (raw.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - "_name".Length);
            return raw.Replace('_', ' ').Trim().ToUpperInvariant();
        }

        // "FRACO CONTRA FOGO" — nome do elemento, nunca só cor (doc 09 P7).
        private static string WeaknessLinePt(BossConfigSO boss)
        {
            if (boss.weaknesses == null || boss.weaknesses.Length == 0)
                return "SEM FRAQUEZA ELEMENTAL";

            var sb = new StringBuilder("FRACO CONTRA ");
            for (int i = 0; i < boss.weaknesses.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(BossScoutOverlay.ElementNamePt(boss.weaknesses[i]));
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- construção fallback

        private static Image BuildChip(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text BuildLabel(Transform parent, string name, string content, float size,
                                           Vector2 pos, Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private sealed class ComboLine
        {
            public GameObject root;
            public TMP_Text label;
            public CanvasGroup group;
        }
    }
}
