using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Pool;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Gera e consome pares de portais (doc 12 §4.3). Geração considera o boss
    /// (CANON §3.1/§3.4): sempre ≥1 rota ótima (explora boss.weaknesses) e ≥1 armadilha
    /// plausível (número maior bruto com elemento resistido). RNG é o System.Random
    /// determinístico da fase — pares reproduzíveis em QA. Consumo é o funil único:
    /// efeito puro → total-alvo → CrowdManager.ReconcileTo.
    /// </summary>
    public class GateManager : MonoBehaviour, IInitializable
    {
        public static GateManager Instance { get; private set; }

        [SerializeField] private GatePairView _pairPrefab;          // 1 prefab = par L/R
        [SerializeField] private GateConfigSO[] _autoBalancePool;   // pool de configs p/ slots autoBalance

        private ObjectPool<GatePairView> _pairPool;                 // UnityEngine.Pool (doc 12 §6.4)
        private readonly List<GatePairView> _livePairs = new List<GatePairView>();
        private readonly List<GateConfigSO> _strongBuffer = new List<GateConfigSO>();   // rota ótima (FORTE)
        private readonly List<GateConfigSO> _trapBuffer = new List<GateConfigSO>();     // armadilha plausível
        private readonly List<GateConfigSO> _neutralBuffer = new List<GateConfigSO>();  // honesto sem brilho

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            _pairPool = new ObjectPool<GatePairView>(
                CreatePair, OnGetPair, OnReleasePair, null,
                collectionCheck: false, defaultCapacity: 8, maxSize: 32);
        }

        // Chamado pelo LevelManager ao montar a pista (doc 12 §4.11), com a MESMA seed da fase.
        public void SpawnGates(LevelConfigSO level, System.Random rng)
        {
            if (level == null || level.gateSlots == null || rng == null) return;
            if (_pairPrefab == null || _pairPool == null)
            {
                Debug.LogWarning("GateManager: _pairPrefab não configurado — fase sem portais.");
                return;
            }

            foreach (GateSlot slot in level.gateSlots)   // posições ao longo da pista
            {
                if (slot == null) continue;
                GateConfigSO left = slot.leftGate;
                GateConfigSO right = slot.rightGate;
                if (slot.autoBalance)                    // fases geradas: escolhe par coerente com o boss
                    (left, right) = PickPairForBoss(level.boss, slot.depth01, rng);
                if (left == null || right == null) continue;

                GatePairView pair = _pairPool.Get();
                pair.Setup(left, right, slot.trackPosition);   // número/ícone/% SEMPRE visíveis
                _livePairs.Add(pair);
            }
        }

        // Par autoBalance SEMPRE honesto-mas-justo (CANON §3.1/§3.4): a rota ótima é genuinamente
        // BOA (FORTE — número alto, elemento/classe vantajoso vs boss ou mutação boa) e a alternativa
        // é NEUTRA ou ARMADILHA (a graça do "número maior que engana"). NUNCA FRACO vs FRACO: nenhum
        // par oferece os DOIS lados ruins. Determinístico pela seed da fase.
        private (GateConfigSO, GateConfigSO) PickPairForBoss(BossConfigSO boss, float depth01, System.Random rng)
        {
            CategorizeForBoss(boss);

            // Lado ótimo: SEMPRE um FORTE. Sem FORTE no pool, o melhor NEUTRO assume o papel
            // (maior número; senão um neutro qualquer) — a armadilha é escolhida por exclusão abaixo.
            GateConfigSO optimal;
            if (_strongBuffer.Count > 0)
                optimal = PickWeighted(_strongBuffer, depth01, rng);
            else
            {
                optimal = PickBiggestNumber(_neutralBuffer, null);
                if (optimal == null) optimal = PickWeighted(_neutralBuffer, depth01, rng);
            }

            // Alternativa: a ARMADILHA é preferida (decisão tem peso real); sem armadilha,
            // um NEUTRO diferente do ótimo. Nunca outro FORTE (não seria escolha), nunca o ótimo.
            GateConfigSO trap = null;
            if (_trapBuffer.Count > 0)
                trap = PickWeighted(_trapBuffer, depth01, rng);
            if (trap == null)
                trap = PickBiggestNumber(_neutralBuffer, optimal);
            if (trap == null || trap == optimal)
                trap = PickWeighted(_neutralBuffer, depth01, rng, optimal);

            if (optimal == null) optimal = trap;     // pool degenerado: par espelhado em vez de slot vazio
            if (trap == null) trap = optimal;
            if (optimal == null) return (null, null);   // pool vazio: o chamador pula o slot

            // lado sorteado: a rota ótima não pode ter lado fixo
            return rng.Next(2) == 0 ? (optimal, trap) : (trap, optimal);
        }

        // Classifica cada gate em 3 cestas vs o boss (CANON §3.1):
        //   FORTE     = ganho genuíno: x2/x3/x5, +25/+50, elemento que explora fraqueza,
        //               classe com elemento vantajoso, mutação boa (DPS/HP/voo).
        //   ARMADILHA = parece bom mas é ruim: −10, ÷2, elemento resistido/imune, risco de EV baixo.
        //   NEUTRO    = honesto sem brilho: +10, x2 pequeno, mutação sem ganho claro, classe neutra.
        private void CategorizeForBoss(BossConfigSO boss)
        {
            _strongBuffer.Clear();
            _trapBuffer.Clear();
            _neutralBuffer.Clear();
            if (_autoBalancePool == null) return;

            foreach (GateConfigSO g in _autoBalancePool)
            {
                if (g == null) continue;
                switch (Classify(g, boss))
                {
                    case GateClass.Strong: _strongBuffer.Add(g); break;
                    case GateClass.Trap: _trapBuffer.Add(g); break;
                    default: _neutralBuffer.Add(g); break;
                }
            }
        }

        private enum GateClass { Neutral, Strong, Trap }

        private static GateClass Classify(GateConfigSO g, BossConfigSO boss)
        {
            // Elemento estratégico (direto p/ Element; herdado da tropa-alvo p/ ClassConvert)
            // tem prioridade na leitura vs boss — uma fraqueza explorada é sempre FORTE,
            // um elemento resistido/imune é sempre ARMADILHA (a promessa do cartão é mecânica).
            ElementType strategic = StrategicElementOf(g);
            if (strategic != ElementType.None && boss != null)
            {
                if (ContainsElement(boss.weaknesses, strategic)) return GateClass.Strong;
                if (ContainsElement(boss.immunities, strategic) || strategic == boss.element)
                    return GateClass.Trap;
            }

            switch (g.gateType)
            {
                case GateType.Multiply:
                    if (g.value < 1f) return GateClass.Trap;          // ÷2 e afins: corta o exército
                    if (g.value >= 2f) return GateClass.Strong;       // x2/x3/x5: ganho real
                    return GateClass.Neutral;                          // x1.x: ganho marginal

                case GateType.AddFlat:
                    if (g.value < 0f) return GateClass.Trap;          // −10: punição disfarçada
                    if (g.value >= 25f) return GateClass.Strong;      // +25/+50: ganho real
                    return GateClass.Neutral;                          // +10: honesto, modesto

                case GateType.ClassConvert:
                    // sem vantagem elemental, classe é só tradeoff de stats → NEUTRO honesto
                    return GateClass.Neutral;

                case GateType.Element:
                    // elemento que não explora fraqueza nem é resistido: lateral → NEUTRO
                    return GateClass.Neutral;

                case GateType.Mutation:
                    return IsStrongMutation(g.mutation) ? GateClass.Strong : GateClass.Neutral;

                case GateType.Risk:
                    // EV = chance×prêmio + (1−chance)×penalidade. EV alto e prêmio gordo → FORTE;
                    // EV baixo (aposta ruim disfarçada de dourado) → ARMADILHA; meio-termo NEUTRO.
                    float ev = g.riskSuccessChance * g.riskRewardMult
                               + (1f - g.riskSuccessChance) * g.riskFailPenalty;
                    if (ev >= 3f) return GateClass.Strong;
                    if (ev < 1.5f) return GateClass.Trap;
                    return GateClass.Neutral;

                default:
                    return GateClass.Neutral;
            }
        }

        // Mutação "boa" = ganho claro de poder (DPS/HP) OU voo/elemento — vira rota ótima.
        // Mutações só cosméticas/laterais (sem ganho líquido) ficam NEUTRAS.
        private static bool IsStrongMutation(MutationConfigSO m)
        {
            if (m == null) return false;
            return m.grantsFlight
                   || m.addsElement != ElementType.None
                   || m.dpsMult >= 1.25f
                   || m.hpMult >= 1.4f;
        }

        // Elemento que o portal injeta no plano contra o boss (CANON §3.1): direto para Element,
        // herdado da tropa-alvo para ClassConvert (converter em Lança-Chamas leva Fogo ao exército).
        private static ElementType StrategicElementOf(GateConfigSO g)
        {
            if (g.gateType == GateType.Element) return g.element;
            if (g.gateType == GateType.ClassConvert && g.unitToAdd != null) return g.unitToAdd.element;
            return ElementType.None;
        }

        private static bool ContainsElement(ElementType[] list, ElementType e)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++)
                if (list[i] == e) return true;
            return false;
        }

        private static GateConfigSO PickWeighted(List<GateConfigSO> pool, float depth01, System.Random rng)
        {
            return PickWeighted(pool, depth01, rng, null);
        }

        // exclude: nunca sorteia o gate já escolhido para o outro lado (evita par espelhado).
        private static GateConfigSO PickWeighted(List<GateConfigSO> pool, float depth01,
                                                 System.Random rng, GateConfigSO exclude)
        {
            if (pool == null || pool.Count == 0) return null;
            double total = 0;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != exclude) total += WeightOf(pool[i], depth01);
            if (total <= 0) return null;   // só o excluído sobrou

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == exclude) continue;
                roll -= WeightOf(pool[i], depth01);
                if (roll <= 0) return pool[i];
            }
            // fallback determinístico: último != exclude
            for (int i = pool.Count - 1; i >= 0; i--)
                if (pool[i] != exclude) return pool[i];
            return null;
        }

        private static double WeightOf(GateConfigSO g, float depth01)
        {
            // números maiores ficam mais prováveis no fundo da pista (depth01 → 1)
            bool isMath = g.gateType == GateType.Multiply || g.gateType == GateType.AddFlat;
            double magnitude = isMath ? Math.Max(1f, g.value) : 1.0;
            return 1.0 + magnitude * depth01;
        }

        private static GateConfigSO PickBiggestNumber(List<GateConfigSO> pool, GateConfigSO exclude)
        {
            GateConfigSO best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < pool.Count; i++)
            {
                GateConfigSO g = pool[i];
                if (g == exclude) continue;
                float score;
                switch (g.gateType)
                {
                    case GateType.Multiply: score = g.value * 10f; break;
                    case GateType.AddFlat: score = g.value; break;
                    case GateType.Risk: score = g.riskRewardMult * g.riskSuccessChance * 10f; break;
                    default: continue;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = g;
                }
            }
            return best;
        }

        /// <summary>
        /// Veredito público da escolha do par (missão Nota 10, contrato §5): TRUE quando o
        /// escolhido é pelo menos tão bom quanto o rejeitado na ordem Trap &lt; Neutral &lt; Strong
        /// (classificados pelo Classify privado contra o boss da fase atual) E não é armadilha.
        /// Consumido pelo ComboSystem (Perfect Gate) e pelo feedback "BOA ESCOLHA!".
        /// </summary>
        public bool WasBestChoice(GateConfigSO chosen, GateConfigSO rejected)
        {
            if (chosen == null) return false;
            BossConfigSO boss = CurrentBoss();
            GateClass chosenClass = Classify(chosen, boss);
            if (chosenClass == GateClass.Trap) return false;       // armadilha nunca é a melhor escolha
            if (rejected == null) return true;                     // par degenerado: sem alternativa real
            return RankOf(chosenClass) >= RankOf(Classify(rejected, boss));
        }

        // Boss da fase atual — GameManager vive no Boot; null-safe p/ cena Game aberta direto (§12).
        private static BossConfigSO CurrentBoss()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.CurrentLevel != null ? gm.CurrentLevel.boss : null;
        }

        // Ordem de MÉRITO da escolha: Trap(0) < Neutral(1) < Strong(2). Os ordinais do enum
        // GateClass não seguem essa ordem (Neutral=0) — por isso o rank explícito.
        private static int RankOf(GateClass c)
        {
            switch (c)
            {
                case GateClass.Strong: return 2;
                case GateClass.Trap: return 0;
                default: return 1;
            }
        }

        // Funil único do consumo: efeito puro → total-alvo → CrowdManager reconcilia (doc 12 §4.3).
        public void Consume(GateConfigSO gate, GateConfigSO rejected)
        {
            CrowdManager crowd = CrowdManager.Instance;
            if (gate == null || crowd == null) return;

            switch (gate.gateType)
            {
                case GateType.AddFlat:
                case GateType.Multiply:
                    crowd.ReconcileTo(gate.Apply(crowd.Count), gate.unitToAdd);
                    break;
                case GateType.ClassConvert:
                    crowd.ConvertClass(gate.unitToAdd, gate.value);
                    break;
                case GateType.Element:
                    crowd.SetElement(gate.element);
                    break;
                case GateType.Mutation:
                    crowd.ApplyMutation(gate.mutation);
                    break;
                case GateType.Risk:
                    RiskResolver.Begin(gate);   // "x10 se sobreviver à zona de perigo"
                    break;
            }

            // Fronteira de assembly (doc 12 §2.3): analytics/UI assinam OnGateConsumed —
            // Gameplay nunca chama Services direto. O rejected vai junto no payload:
            // gate_selected(chosen, rejected) mede rota ótima vs armadilha (doc 12 §4.3/§4.9).
            GameEvents.RaiseGateConsumed(new GateResult(gate, rejected, crowd.Count));

            EmitChoiceFeedback(gate, rejected);
        }

        // Veredito cosmético instantâneo da escolha (missão Nota 10): "BOA ESCOLHA!" só quando o
        // jogador leu o par certo (escolhido ESTRITAMENTE melhor que o rejeitado — elogio barato
        // vira ruído); aviso só quando caiu na ARMADILHA tendo opção melhor na mesa. Pares de
        // mérito igual não geram veredito. Âncora: posição do exército (CrowdAnchor) — o par já
        // foi consumido/devolvido ao pool quando este código roda.
        private void EmitChoiceFeedback(GateConfigSO chosen, GateConfigSO rejected)
        {
            if (chosen == null || rejected == null) return;
            BossConfigSO boss = CurrentBoss();
            GateClass chosenClass = Classify(chosen, boss);
            int chosenRank = RankOf(chosenClass);
            int rejectedRank = RankOf(Classify(rejected, boss));
            Vector3 position = CrowdAnchor.Position;

            // chosenRank > rejectedRank já implica chosen != Trap (rank de Trap é o piso 0).
            if (chosenRank > rejectedRank)
                JuiceEvents.RaiseGoodGateChoice(position);
            else if (chosenClass == GateClass.Trap && rejectedRank > chosenRank)
                JuiceEvents.RaiseBadGateChoice(position);
        }

        /// <summary>Soft reset (doc 12 §4.11): devolve todos os pares vivos ao pool.</summary>
        public void ReleaseAll()
        {
            if (_pairPool == null)
            {
                _livePairs.Clear();
                return;
            }
            for (int i = 0; i < _livePairs.Count; i++)
                if (_livePairs[i] != null) _pairPool.Release(_livePairs[i]);
            _livePairs.Clear();
        }

        private GatePairView CreatePair()
        {
            GatePairView pair = Instantiate(_pairPrefab);
            pair.gameObject.SetActive(false);
            return pair;
        }

        private void OnGetPair(GatePairView pair)
        {
            pair.gameObject.SetActive(true);
        }

        private void OnReleasePair(GatePairView pair)
        {
            pair.gameObject.SetActive(false);
        }
    }
}
