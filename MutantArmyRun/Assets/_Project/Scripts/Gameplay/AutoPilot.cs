using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Piloto automático para testes PlayMode/QA/demo — DESLIGADO por padrão (a flag
    /// _active nasce false; o componente pode existir numa cena de produção sem efeito).
    /// Quando ativo e o jogo está em Running, empurra o CrowdAnchor para frente
    /// continuamente (além da velocidade própria do líder) e escolhe o lado do próximo
    /// par de portais lendo APENAS o dado (GateConfigSO) — zero dependência de UI/input.
    /// Estratégias: maior valor esperado de contagem (padrão, determinística) ou lado
    /// aleatório com seed (reprodutível em QA).
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public enum SideStrategy
        {
            BestExpectedValue,
            RandomSeeded
        }

        [SerializeField] private bool _active;   // desligado por padrão — testes ligam via Active/Configure
        [SerializeField] private SideStrategy _strategy = SideStrategy.BestExpectedValue;
        [SerializeField] private int _randomSeed = 12345;
        [SerializeField] private float _extraForwardSpeed = 4f;   // m/s somados ao avanço base do anchor
        [SerializeField] private float _lateralSpeed = 10f;       // m/s de correção lateral até o lado escolhido
        [SerializeField] private float _laneHalfWidth = 2.2f;     // mesmo clamp lateral do CrowdAnchor

        private System.Random _rng;
        private GatePairView _decidedPair;     // decisão é tomada 1× por par e mantida até passar por ele
        private float _decidedX;

        public bool Active
        {
            get { return _active; }
            set { _active = value; }
        }

        public SideStrategy Strategy
        {
            get { return _strategy; }
        }

        public void Configure(SideStrategy strategy, int seed)
        {
            _strategy = strategy;
            _randomSeed = seed;
            _rng = new System.Random(seed);
            _decidedPair = null;
        }

        private void Update()
        {
            if (!_active) return;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Running) return;

            CrowdAnchor anchor = CrowdAnchor.Instance;
            if (anchor == null) return;

            Vector3 p = anchor.transform.position;
            p.z += _extraForwardSpeed * Time.deltaTime;

            GatePairView next = FindNextPairAhead(p.z);
            if (next != null)
            {
                if (!ReferenceEquals(next, _decidedPair))
                {
                    _decidedPair = next;
                    _decidedX = DecideTargetX(next);
                }
                p.x = Mathf.MoveTowards(p.x, _decidedX, _lateralSpeed * Time.deltaTime);
            }

            p.x = Mathf.Clamp(p.x, -_laneHalfWidth, _laneHalfWidth);
            anchor.transform.position = p;
        }

        private static GatePairView FindNextPairAhead(float z)
        {
            // Busca por tipo (só ativos): pares consumidos ficam na cena até o soft reset,
            // mas como a busca é sempre "o mais próximo À FRENTE", eles saem do alvo
            // naturalmente quando o líder passa por eles.
            GatePairView[] pairs = FindObjectsByType<GatePairView>();
            GatePairView best = null;
            float bestZ = float.MaxValue;
            for (int i = 0; i < pairs.Length; i++)
            {
                float pairZ = pairs[i].transform.position.z;
                if (pairZ <= z) continue;
                if (pairZ < bestZ)
                {
                    bestZ = pairZ;
                    best = pairs[i];
                }
            }
            return best;
        }

        private float DecideTargetX(GatePairView pair)
        {
            List<GateView> views = new List<GateView>();
            pair.GetComponentsInChildren(false, views);
            if (views.Count == 0) return 0f;

            GateView chosen;
            if (_strategy == SideStrategy.RandomSeeded)
            {
                if (_rng == null) _rng = new System.Random(_randomSeed);
                chosen = views[_rng.Next(views.Count)];
            }
            else
            {
                int count = CrowdManager.Instance != null ? Mathf.Max(1, CrowdManager.Instance.Count) : 1;
                chosen = views[0];
                double bestEv = double.MinValue;
                for (int i = 0; i < views.Count; i++)
                {
                    double ev = ExpectedValue(views[i].Config, count);
                    if (ev > bestEv)
                    {
                        bestEv = ev;
                        chosen = views[i];
                    }
                }
            }

            return Mathf.Clamp(chosen.transform.position.x, -_laneHalfWidth, _laneHalfWidth);
        }

        /// <summary>
        /// Valor esperado da CONTAGEM após o portal, lido do GateConfigSO via Domain.GateMath
        /// (mesma semântica de total-alvo do consumo real). Element/Mutation/ClassConvert não
        /// mudam contagem — valem o total atual (heurística simples, suficiente p/ o piloto).
        /// </summary>
        private static double ExpectedValue(GateConfigSO gate, int count)
        {
            if (gate == null) return double.MinValue;   // meio-portal sem config nunca é escolhido

            switch (gate.gateType)
            {
                case GateType.AddFlat:
                case GateType.Multiply:
                    return GateMath.Apply(gate.gateType, gate.value, count);
                case GateType.Risk:
                    double success = GateMath.Apply(GateType.Multiply, gate.riskRewardMult, count);
                    double fail = GateMath.Apply(GateType.Multiply, gate.riskFailPenalty, count);
                    return gate.riskSuccessChance * success + (1f - gate.riskSuccessChance) * fail;
                default:
                    return count;
            }
        }
    }
}
