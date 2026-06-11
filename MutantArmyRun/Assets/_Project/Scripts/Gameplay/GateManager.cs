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
        private readonly List<GateConfigSO> _optimalBuffer = new List<GateConfigSO>();
        private readonly List<GateConfigSO> _trapBuffer = new List<GateConfigSO>();
        private readonly List<GateConfigSO> _neutralBuffer = new List<GateConfigSO>();

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

        // Rota ótima: portal cujo elemento explora boss.weaknesses.
        // Armadilha: número maior bruto, mas elemento resistido/imune (CANON §3.1).
        private (GateConfigSO, GateConfigSO) PickPairForBoss(BossConfigSO boss, float depth01, System.Random rng)
        {
            CategorizeForBoss(boss);

            GateConfigSO optimal = PickWeighted(
                _optimalBuffer.Count > 0 ? _optimalBuffer : _neutralBuffer, depth01, rng);
            GateConfigSO trap = _trapBuffer.Count > 0
                ? PickWeighted(_trapBuffer, depth01, rng)
                : PickBiggestNumber(_neutralBuffer, optimal);

            if (optimal == null) optimal = trap;
            if (trap == null) trap = optimal;
            if (optimal == null) return (null, null);   // pool vazio: o chamador pula o slot

            // lado sorteado: a rota ótima não pode ter lado fixo
            return rng.Next(2) == 0 ? (optimal, trap) : (trap, optimal);
        }

        private void CategorizeForBoss(BossConfigSO boss)
        {
            _optimalBuffer.Clear();
            _trapBuffer.Clear();
            _neutralBuffer.Clear();
            if (_autoBalancePool == null) return;

            foreach (GateConfigSO g in _autoBalancePool)
            {
                if (g == null) continue;
                bool exploitsWeakness = g.gateType == GateType.Element && boss != null
                                        && ContainsElement(boss.weaknesses, g.element);
                bool resisted = g.gateType == GateType.Element && boss != null
                                && (ContainsElement(boss.immunities, g.element) || g.element == boss.element);
                if (exploitsWeakness) _optimalBuffer.Add(g);
                else if (resisted) _trapBuffer.Add(g);
                else _neutralBuffer.Add(g);
            }
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
            if (pool == null || pool.Count == 0) return null;
            double total = 0;
            for (int i = 0; i < pool.Count; i++) total += WeightOf(pool[i], depth01);

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= WeightOf(pool[i], depth01);
                if (roll <= 0) return pool[i];
            }
            return pool[pool.Count - 1];
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
