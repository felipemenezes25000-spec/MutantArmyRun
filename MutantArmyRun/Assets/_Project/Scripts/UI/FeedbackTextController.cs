using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// Feedbacks textuais canônicos (tabela do doc 09 §4.2; doc 14 §7) + feedbacks da missão
    /// Nota 10: FRAQUEZA!/RESISTIU! (OnBossElementalHit), BOA ESCOLHA!/PORTAL RUIM...
    /// (JuiceEvents.OnGood/BadGateChoice), x10!/QUE PENA... (OnRiskResolved), BOSS RARO!
    /// (OnRareBossAnnounced) e os 6 combos de fim de corrida (OnComboEarned).
    /// Nunca 2 ao mesmo tempo — fila com prioridade crescente (ordem do enum); cada gatilho
    /// dispara no máx. 1×/corrida exceto os repetíveis (RepeatsAllowed), que usam cooldown
    /// próprio contra spam. Tudo por evento do bus — zero polling (doc 12 §3.2).
    /// Os textos antigos seguem em inglês ("linguagem de jogo", doc 09 §6); os da missão são
    /// PT-BR curtos e gritantes por decisão da missão Nota 10 (máx. 3-5 palavras).
    /// </summary>
    public class FeedbackTextController : MonoBehaviour
    {
        // Ordem = prioridade crescente (doc 09 §4.2). Combos no topo: são o clímax da corrida
        // e chegam ANTES da Victory (OnComboEarned na morte do boss) — a fila os escalona.
        private enum Feedback
        {
            Nice = 0,
            Great = 1,
            GoodChoice = 2,         // "BOA ESCOLHA!" — escolha ótima de portal (JuiceEvents)
            Insane = 3,
            BadChoice = 4,          // "PORTAL RUIM..." — armadilha escolhida (1×/fase, discreto)
            RiskLose = 5,           // "QUE PENA..." — zona de risco perdida
            RiskWin = 6,            // "x10!" — zona de risco vencida
            Resisted = 7,           // "RESISTIU!" — golpe resistido/imune no boss
            Weakness = 8,           // "FRAQUEZA!" — golpe na fraqueza elemental
            Godlike = 9,
            Perfect = 10,
            Mutation = 11,
            MegaArmy = 12,
            RareBoss = 13,          // "BOSS RARO!" — anunciado no BossScout
            ComboPerfectGate = 14,
            ComboWeaknessHit = 15,
            ComboOverkill = 16,
            ComboNoLoss = 17,
            ComboClutch = 18,
            ComboBossBreaker = 19
        }

        private const int FeedbackCount = 20;   // tamanho de Labels/_firedThisRun — manter em dia com o enum

        private static readonly string[] Labels =
        {
            "NICE!", "GREAT!", "BOA ESCOLHA!", "INSANE!", "PORTAL RUIM...", "QUE PENA...", "x10!",
            "RESISTIU!", "FRAQUEZA!", "GODLIKE!", "PERFECT!", "MUTATION!", "MEGA ARMY!", "BOSS RARO!",
            "PORTAIS PERFEITOS!", "CAÇADOR DE FRAQUEZAS!", "OVERKILL!", "SEM PERDAS!",
            "VITÓRIA IMPOSSÍVEL!", "BOSS BREAKER!"
        };

        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _popSeconds = 0.6f;     // pop elástico 0,6 s (doc 09 §4.2)
        [SerializeField] private float _holdSeconds = 0.35f;
        [SerializeField] private float _fadeSeconds = 0.2f;
        [SerializeField] private int _godlikeArmySize = 200;   // doc 09 §4.2
        [SerializeField] private int _insaneStreak = 3;
        [SerializeField] private float _elementalCooldown = 2f;    // FRAQUEZA!/RESISTIU! repetíveis com folga
        [SerializeField] private float _goodChoiceCooldown = 1.5f; // BOA ESCOLHA! sem virar metralhadora

        // Entrada da fila: o texto vem de Labels, mas cor (FRAQUEZA tinge pelo elemento) e a
        // linha de dica ("Tente outro elemento!", "+25 moedas") variam por disparo.
        private struct Entry
        {
            public Feedback kind;
            public Color color;
            public string hint;     // null = sem segunda linha
        }

        private readonly List<Entry> _queue = new List<Entry>(8);
        private readonly bool[] _firedThisRun = new bool[FeedbackCount];
        private bool _playing;
        private int _positiveStreak;
        private int _unitsLost;
        private bool _resistedHintShown;        // dica "Tente outro elemento!" só na 1ª vez por luta
        private float _lastWeaknessTime = -999f;
        private float _lastResistedTime = -999f;
        private float _lastGoodChoiceTime = -999f;
        private Color _defaultColor = new Color(1f, 0.76f, 0.20f);   // âmbar da factory (fallback)
        private bool _defaultColorCaptured;

        // Cores fixas dos feedbacks novos (a COR reforça, o NOME informa — doc 09 P7).
        private static readonly Color GoldColor = new Color(1.00f, 0.84f, 0.25f);
        private static readonly Color GrayColor = new Color(0.72f, 0.72f, 0.78f);
        private static readonly Color BadColor = new Color(0.85f, 0.32f, 0.28f);     // vermelho discreto
        private static readonly Color RareColor = new Color(0.80f, 0.45f, 1.00f);

        /// <summary>true enquanto nenhuma unidade morreu na corrida (alimenta o selo PERFECT).</summary>
        public bool PerfectSoFar
        {
            get { return _unitsLost == 0; }
        }

        private void Awake()
        {
            // Cor base do label (a factory pinta de âmbar) — restaurada nos feedbacks antigos.
            if (_label != null)
            {
                _defaultColor = _label.color;
                _defaultColorCaptured = true;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnCrowdChanged += HandleCrowdChanged;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
            GameEvents.OnMutationGained += HandleMutationGained;
            GameEvents.OnUnitDied += HandleUnitDied;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnBossElementalHit += HandleBossElementalHit;
            GameEvents.OnComboEarned += HandleComboEarned;
            GameEvents.OnRareBossAnnounced += HandleRareBossAnnounced;
            JuiceEvents.OnGoodGateChoice += HandleGoodGateChoice;
            JuiceEvents.OnBadGateChoice += HandleBadGateChoice;
            JuiceEvents.OnRiskResolved += HandleRiskResolved;
            // LevelStarted é o gatilho de reset CORRETO: cobre TODO início de fase, inclusive o
            // soft reset da pausa (RestartLevelFromAnyState), que NÃO dispara OnLevelFinished —
            // sem isto, _firedThisRun/_unitsLost vazavam da corrida abortada e suprimiam
            // feedbacks 1×/PERFECT na fase reiniciada. (-= antes de += : sem dupla inscrição.)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
                GameManager.Instance.LevelStarted += HandleLevelStarted;
            }
            TryPlayNext();
        }

        private void OnDisable()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnCrowdChanged -= HandleCrowdChanged;
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnMutationGained -= HandleMutationGained;
            GameEvents.OnUnitDied -= HandleUnitDied;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnRareBossAnnounced -= HandleRareBossAnnounced;
            JuiceEvents.OnGoodGateChoice -= HandleGoodGateChoice;
            JuiceEvents.OnBadGateChoice -= HandleBadGateChoice;
            JuiceEvents.OnRiskResolved -= HandleRiskResolved;
            if (GameManager.Instance != null) GameManager.Instance.LevelStarted -= HandleLevelStarted;
            StopAllCoroutines();
            _playing = false;
            if (_label != null) _label.gameObject.SetActive(false);
        }

        // Início de fase (StartLevel/RestartLevelFromAnyState): zera o estado da corrida ANTES
        // do 1º feedback (o RareBoss do BossScout vem depois, sem clobber). Cobre o soft reset.
        private void HandleLevelStarted(int levelIndex)
        {
            ResetRun();
        }

        /// <summary>Zera os contadores da corrida (chamado também no fim de fase).</summary>
        public void ResetRun()
        {
            Array.Clear(_firedThisRun, 0, _firedThisRun.Length);
            _positiveStreak = 0;
            _unitsLost = 0;
            _resistedHintShown = false;
            _lastWeaknessTime = -999f;
            _lastResistedTime = -999f;
            _lastGoodChoiceTime = -999f;
        }

        /// <summary>
        /// PERFECT dispara ao chegar à arena sem perder nenhuma unidade (doc 09 §4.2).
        /// Não há evento de "arena alcançada" no bus — o glue de cena chama este método.
        /// </summary>
        public void NotifyArenaReached()
        {
            if (_unitsLost == 0) Enqueue(Feedback.Perfect);
        }

        private void HandleGateConsumed(GateResult result)
        {
            GateConfigSO gate = result.gate;
            if (gate == null) return;

            switch (gate.gateType)
            {
                case GateType.AddFlat:
                    if (gate.value > 0f)
                    {
                        _positiveStreak++;
                        Enqueue(Feedback.Nice);     // portal positivo simples (+10/+25)
                    }
                    else
                    {
                        _positiveStreak = 0;
                    }
                    break;

                case GateType.Multiply:
                    if (gate.value >= 2f)
                    {
                        _positiveStreak++;
                        Enqueue(Feedback.Great);    // multiplicador ×2/×3
                    }
                    else if (gate.value < 1f)
                    {
                        _positiveStreak = 0;        // ÷2 quebra a sequência
                    }
                    break;

                    // Risk: o veredito vem pelo JuiceEvents.OnRiskResolved (x10!/QUE PENA...).
                    // Element/Mutation/ClassConvert: neutros para a sequência.
            }

            if (_positiveStreak >= _insaneStreak) Enqueue(Feedback.Insane);
        }

        private void HandleCrowdChanged(int count, int supplyUsed)
        {
            if (count >= _godlikeArmySize) Enqueue(Feedback.Godlike);        // exército ≥ 200
        }

        private void HandleUnitDied(UnitDeath death)
        {
            _unitsLost++;
            _positiveStreak = 0;    // "sem perder unidade" — morte zera a sequência do INSANE
        }

        private void HandleMutationGained(MutationConfigSO mutation)
        {
            Enqueue(Feedback.Mutation);
        }

        private void HandleSupplyOverflow(SupplyOverflow overflow)
        {
            Enqueue(Feedback.MegaArmy);     // estouro de Supply = fanfarra, nunca punição (CANON §3.2)
        }

        private void HandleLevelFinished(LevelResult result)
        {
            // BOSS BREAKER deixou de ser one-shot de vitória: agora é combo de verdade
            // (ComboKind.BossBreaker via OnComboEarned, luta ≤ 8 s) — enfileirar aqui duplicaria.
            ResetRun();                                     // próxima corrida começa limpa
        }

        // ---------------------------------------------------------------- missão Nota 10

        // FRAQUEZA!/RESISTIU! — golpe elemental já classificado (WeaknessJudge na origem,
        // rate-limited ≥0,5 s no Gameplay). Cooldown local ~2 s: repetível sem virar ruído.
        private void HandleBossElementalHit(BossElementalHit hit)
        {
            float now = Time.unscaledTime;
            switch (hit.relation)
            {
                case ElementRelation.Weakness:
                    if (now - _lastWeaknessTime < _elementalCooldown) return;
                    _lastWeaknessTime = now;
                    // Tinge pela cor canônica do elemento do golpe (laranja quando None).
                    Color weaknessColor = hit.element != ElementType.None
                        ? BossScoutOverlay.ElementColorPt(hit.element)
                        : new Color(1.00f, 0.55f, 0.15f);
                    Enqueue(Feedback.Weakness, weaknessColor, null);
                    break;

                case ElementRelation.Resisted:
                case ElementRelation.Immune:
                    if (now - _lastResistedTime < _elementalCooldown) return;
                    _lastResistedTime = now;
                    string hint = _resistedHintShown ? null : "Tente outro elemento!";
                    _resistedHintShown = true;      // dica 1×/luta — o grito continua repetindo
                    Enqueue(Feedback.Resisted, GrayColor, hint);
                    break;

                    // Neutral: sem texto — o feedback elemental só grita quando há decisão a tomar.
            }
        }

        // BOA ESCOLHA! — veredito instantâneo do GateManager (rota ótima vs armadilha).
        private void HandleGoodGateChoice(Vector3 worldPosition)
        {
            float now = Time.unscaledTime;
            if (now - _lastGoodChoiceTime < _goodChoiceCooldown) return;
            _lastGoodChoiceTime = now;
            Enqueue(Feedback.GoodChoice, GoldColor, null);
        }

        // PORTAL RUIM... — discreto e 1×/fase (Enqueue já trava via _firedThisRun):
        // aponta o erro sem esfregar na cara a cada portal.
        private void HandleBadGateChoice(Vector3 worldPosition)
        {
            Enqueue(Feedback.BadChoice, BadColor, null);
        }

        // x10!/QUE PENA... — veredito da zona de risco (substitui o antigo GODLIKE heurístico
        // por OnCrowdChanged: agora o RiskResolver publica o resultado de verdade).
        private void HandleRiskResolved(bool success, Vector3 worldPosition)
        {
            if (success) Enqueue(Feedback.RiskWin, GoldColor, null);
            else Enqueue(Feedback.RiskLose, GrayColor, null);
        }

        // Combos do fim da corrida — chegam 1 evento por combo ANTES da Victory; a fila
        // escalona os popups na ordem de prioridade, com o bônus na linha de dica.
        private void HandleComboEarned(ComboEarned combo)
        {
            Feedback kind;
            switch (combo.kind)
            {
                case ComboKind.PerfectGate: kind = Feedback.ComboPerfectGate; break;
                case ComboKind.WeaknessHit: kind = Feedback.ComboWeaknessHit; break;
                case ComboKind.BossBreaker: kind = Feedback.ComboBossBreaker; break;
                case ComboKind.Clutch: kind = Feedback.ComboClutch; break;
                case ComboKind.NoLoss: kind = Feedback.ComboNoLoss; break;
                case ComboKind.Overkill: kind = Feedback.ComboOverkill; break;
                default: return;    // ComboKind novo sem texto: degrada sem quebrar
            }
            string hint = combo.bonusCoins > 0 ? "+" + combo.bonusCoins + " moedas" : null;
            Enqueue(kind, GoldColor, hint);
        }

        // BOSS RARO! — anunciado no BossScout (início da corrida), antes da fase começar.
        private void HandleRareBossAnnounced(RareBossAnnounce announce)
        {
            string hint = announce.rewardMultiplier > 1f
                ? "Recompensa x" + Mathf.RoundToInt(announce.rewardMultiplier) + "!"
                : null;
            Enqueue(Feedback.RareBoss, RareColor, hint);
        }

        // ---------------------------------------------------------------- fila

        private static bool RepeatsAllowed(Feedback feedback)
        {
            // Repetíveis: frequentes por natureza (gates/risco/elemental) — o cooldown de cada
            // gatilho impede spam. O resto continua máx. 1×/corrida (doc 14 §7).
            switch (feedback)
            {
                case Feedback.Nice:
                case Feedback.Great:
                case Feedback.Insane:
                case Feedback.GoodChoice:
                case Feedback.RiskWin:
                case Feedback.RiskLose:
                case Feedback.Resisted:
                case Feedback.Weakness:
                    return true;
                default:
                    return false;
            }
        }

        private void Enqueue(Feedback feedback)
        {
            Enqueue(feedback, DefaultColor(), null);
        }

        private void Enqueue(Feedback feedback, Color color, string hint)
        {
            if (!RepeatsAllowed(feedback))
            {
                if (_firedThisRun[(int)feedback]) return;   // máx. 1×/corrida (doc 14 §7)
                _firedThisRun[(int)feedback] = true;
            }
            _queue.Add(new Entry { kind = feedback, color = color, hint = hint });
            TryPlayNext();
        }

        private Color DefaultColor()
        {
            if (!_defaultColorCaptured && _label != null)
            {
                _defaultColor = _label.color;
                _defaultColorCaptured = true;
            }
            return _defaultColor;
        }

        private void TryPlayNext()
        {
            if (_playing || _queue.Count == 0 || _label == null || !isActiveAndEnabled) return;

            // Fila com prioridade crescente: o pendente de MAIOR prioridade sai primeiro.
            int best = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                if ((int)_queue[i].kind > (int)_queue[best].kind) best = i;
            }
            Entry next = _queue[best];
            _queue.RemoveAt(best);
            StartCoroutine(PlayRoutine(next));
        }

        private IEnumerator PlayRoutine(Entry entry)
        {
            _playing = true;
            _label.gameObject.SetActive(true);
            // Linha de dica opcional em corpo menor (rich text — o TMP da factory tem richText on).
            _label.text = entry.hint == null
                ? Labels[(int)entry.kind]
                : Labels[(int)entry.kind] + "\n<size=46%>" + entry.hint + "</size>";
            _label.color = entry.color;     // setar a cor ANTES do alpha (alpha escreve em color.a)
            Transform t = _label.transform;

            // unscaled: os combos aparecem DURANTE o slow motion canônico (doc 12 §3.1).
            float elapsed = 0f;
            while (elapsed < _popSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = _popSeconds > 0f ? Mathf.Clamp01(elapsed / _popSeconds) : 1f;
                float s = ElasticOut(k);
                t.localScale = new Vector3(s, s, 1f);
                _label.alpha = Mathf.Clamp01(k * 4f);
                yield return null;
            }
            t.localScale = Vector3.one;
            _label.alpha = 1f;

            elapsed = 0f;
            while (elapsed < _holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _label.alpha = 1f - (_fadeSeconds > 0f ? Mathf.Clamp01(elapsed / _fadeSeconds) : 1f);
                yield return null;
            }

            _label.gameObject.SetActive(false);
            _playing = false;
            TryPlayNext();
        }

        private static float ElasticOut(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * k) * Mathf.Sin((k * 10f - 0.75f) * c4) + 1f;
        }
    }
}
