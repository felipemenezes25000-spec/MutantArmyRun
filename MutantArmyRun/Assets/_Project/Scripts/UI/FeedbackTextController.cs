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
    /// Feedbacks textuais canônicos (tabela exclusiva do doc 09 §4.2; doc 14 §7):
    /// NICE (portal positivo simples) · GREAT (×2/×3) · INSANE (3 positivos seguidos sem
    /// perder unidade) · GODLIKE (exército ≥ 200 OU risco vencido) · PERFECT (arena sem
    /// perder unidade) · MUTATION (portal de mutação) · MEGA ARMY (estouro de Supply) ·
    /// BOSS BREAKER (golpe final no boss). Nunca 2 ao mesmo tempo — fila com prioridade
    /// crescente; cada gatilho dispara no máx. 1×/corrida exceto NICE/GREAT/INSANE.
    /// Tudo por evento do bus — zero polling (doc 12 §3.2).
    /// </summary>
    public class FeedbackTextController : MonoBehaviour
    {
        // Ordem = prioridade crescente (doc 09 §4.2).
        private enum Feedback
        {
            Nice = 0,
            Great = 1,
            Insane = 2,
            Godlike = 3,
            Perfect = 4,
            Mutation = 5,
            MegaArmy = 6,
            BossBreaker = 7
        }

        // Textos ficam em inglês em todas as línguas — "linguagem de jogo" (doc 09 §6).
        private static readonly string[] Labels =
        {
            "NICE!", "GREAT!", "INSANE!", "GODLIKE!", "PERFECT!", "MUTATION!", "MEGA ARMY!", "BOSS BREAKER!"
        };

        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _popSeconds = 0.6f;     // pop elástico 0,6 s (doc 09 §4.2)
        [SerializeField] private float _holdSeconds = 0.35f;
        [SerializeField] private float _fadeSeconds = 0.2f;
        [SerializeField] private int _godlikeArmySize = 200;   // doc 09 §4.2
        [SerializeField] private int _insaneStreak = 3;

        private readonly List<Feedback> _queue = new List<Feedback>(8);
        private readonly bool[] _firedThisRun = new bool[8];
        private bool _playing;
        private int _positiveStreak;
        private int _unitsLost;
        private int _lastCrowdCount;
        private bool _riskPending;
        private int _riskCountSnapshot;

        /// <summary>true enquanto nenhuma unidade morreu na corrida (alimenta o selo PERFECT).</summary>
        public bool PerfectSoFar
        {
            get { return _unitsLost == 0; }
        }

        private void OnEnable()
        {
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnCrowdChanged += HandleCrowdChanged;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
            GameEvents.OnMutationGained += HandleMutationGained;
            GameEvents.OnUnitDied += HandleUnitDied;
            GameEvents.OnLevelFinished += HandleLevelFinished;
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
            StopAllCoroutines();
            _playing = false;
            if (_label != null) _label.gameObject.SetActive(false);
        }

        /// <summary>Zera os contadores da corrida (chamado também no fim de fase).</summary>
        public void ResetRun()
        {
            Array.Clear(_firedThisRun, 0, _firedThisRun.Length);
            _positiveStreak = 0;
            _unitsLost = 0;
            _riskPending = false;
            _riskCountSnapshot = 0;
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

                case GateType.Risk:
                    // O risco resolve depois (zona de perigo): o veredito chega pelo
                    // próximo OnCrowdChanged — contagem subiu = risco vencido (GODLIKE).
                    _riskPending = true;
                    _riskCountSnapshot = _lastCrowdCount;
                    break;

                    // Element/Mutation/ClassConvert: neutros para a sequência.
            }

            if (_positiveStreak >= _insaneStreak) Enqueue(Feedback.Insane);
        }

        private void HandleCrowdChanged(int count, int supplyUsed)
        {
            if (_riskPending && count != _riskCountSnapshot)
            {
                if (count > _riskCountSnapshot) Enqueue(Feedback.Godlike);   // risco vencido
                _riskPending = false;
            }

            if (count >= _godlikeArmySize) Enqueue(Feedback.Godlike);        // exército ≥ 200
            _lastCrowdCount = count;
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
            if (result.won) Enqueue(Feedback.BossBreaker);  // golpe final (junto do slow motion)
            ResetRun();                                     // próxima corrida começa limpa
        }

        private static bool RepeatsAllowed(Feedback feedback)
        {
            return feedback == Feedback.Nice || feedback == Feedback.Great || feedback == Feedback.Insane;
        }

        private void Enqueue(Feedback feedback)
        {
            if (!RepeatsAllowed(feedback))
            {
                if (_firedThisRun[(int)feedback]) return;   // máx. 1×/corrida (doc 14 §7)
                _firedThisRun[(int)feedback] = true;
            }
            _queue.Add(feedback);
            TryPlayNext();
        }

        private void TryPlayNext()
        {
            if (_playing || _queue.Count == 0 || _label == null || !isActiveAndEnabled) return;

            // Fila com prioridade crescente: o pendente de MAIOR prioridade sai primeiro.
            int best = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                if ((int)_queue[i] > (int)_queue[best]) best = i;
            }
            Feedback next = _queue[best];
            _queue.RemoveAt(best);
            StartCoroutine(PlayRoutine(next));
        }

        private IEnumerator PlayRoutine(Feedback feedback)
        {
            _playing = true;
            _label.gameObject.SetActive(true);
            _label.text = Labels[(int)feedback];
            Transform t = _label.transform;

            // unscaled: BOSS BREAKER aparece DURANTE o slow motion canônico (doc 12 §3.1).
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
