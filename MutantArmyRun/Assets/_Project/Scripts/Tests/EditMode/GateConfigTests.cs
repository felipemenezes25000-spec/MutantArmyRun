using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.Tests
{
    /// <summary>
    /// Os 8 portais canônicos do CANON §10 testados via GateConfigSO.Apply — a função
    /// pura de TOTAL-ALVO do doc 12 §4.3/§5.1. Inclui o caso negativo obrigatório
    /// "x2 que triplica" (multiplicador aplicado como delta) e a resolução do portal
    /// de risco delegada ao Domain (RiskGate, RNG injetado).
    /// </summary>
    public class GateConfigTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object instance in _created)
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            _created.Clear();
        }

        private GateConfigSO MakeGate(GateType type, float value)
        {
            var gate = ScriptableObject.CreateInstance<GateConfigSO>();
            gate.gateType = type;
            gate.value = value;
            _created.Add(gate);
            return gate;
        }

        [Test]
        public void AddTen_IsTargetTotal()
        {
            Assert.AreEqual(17, MakeGate(GateType.AddFlat, 10f).Apply(7));
        }

        [Test]
        public void AddTwentyFive_IsTargetTotal()
        {
            Assert.AreEqual(26, MakeGate(GateType.AddFlat, 25f).Apply(1));
        }

        [Test]
        public void TimesTwo_DoublesAndNeverTriples()
        {
            int result = MakeGate(GateType.Multiply, 2f).Apply(21);
            // Caso negativo obrigatório (doc 12 §4.3): x2 como DELTA daria 21 + 42 = 63 —
            // o bug real "x2 que triplica". A semântica é TOTAL-ALVO: 42.
            Assert.AreNotEqual(63, result);
            Assert.AreEqual(42, result);
        }

        [Test]
        public void TimesThree_IsTargetTotal()
        {
            Assert.AreEqual(15, MakeGate(GateType.Multiply, 3f).Apply(5));
        }

        [Test]
        public void HalfGate_OddRoundsUp()
        {
            // ÷2 = value 0.5; ímpar arredonda a favor do jogador: ⌈7/2⌉ = 4 (doc 04).
            Assert.AreEqual(4, MakeGate(GateType.Multiply, 0.5f).Apply(7));
        }

        [Test]
        public void HalfGate_EvenHalves()
        {
            Assert.AreEqual(4, MakeGate(GateType.Multiply, 0.5f).Apply(8));
        }

        [Test]
        public void HalfGate_NeverZeroesTheArmy()
        {
            Assert.AreEqual(1, MakeGate(GateType.Multiply, 0.5f).Apply(1));
        }

        [Test]
        public void ClassConvertArcher_DoesNotChangeCount()
        {
            // "Virar Arqueiro" muda composição, nunca a contagem (doc 12 §5.1).
            var gate = MakeGate(GateType.ClassConvert, 1f);
            Assert.AreEqual(9, gate.Apply(9));
        }

        [Test]
        public void ElementFire_DoesNotChangeCount()
        {
            var gate = MakeGate(GateType.Element, 0f);
            gate.element = ElementType.Fire;
            Assert.AreEqual(9, gate.Apply(9));
        }

        [Test]
        public void RiskGate_ApplyIsIdentity_ResolutionHappensInZone()
        {
            // O portal de risco não muda a contagem no toque: a resolução acontece ao
            // fim da zona de perigo, via Domain.RiskGate com RNG injetado.
            var gate = MakeGate(GateType.Risk, 0f);
            gate.riskSuccessChance = 0.7f;
            gate.riskRewardMult = 10f;
            gate.riskFailPenalty = 0.5f;
            Assert.AreEqual(9, gate.Apply(9));
        }

        [Test]
        public void RiskGate_Resolution_SuccessMultipliesByTen()
        {
            var gate = MakeGate(GateType.Risk, 0f);
            gate.riskSuccessChance = 1f;    // odds nas pontas tornam o resultado determinístico
            gate.riskRewardMult = 10f;
            gate.riskFailPenalty = 0.5f;

            int result = RiskGate.Resolve(new System.Random(1234),
                gate.riskSuccessChance, gate.riskRewardMult, gate.riskFailPenalty, 7);
            Assert.AreEqual(70, result);
        }

        [Test]
        public void RiskGate_Resolution_FailureHalvesWithFloorOfOne()
        {
            var gate = MakeGate(GateType.Risk, 0f);
            gate.riskSuccessChance = 0f;
            gate.riskRewardMult = 10f;
            gate.riskFailPenalty = 0.5f;

            int halved = RiskGate.Resolve(new System.Random(1234),
                gate.riskSuccessChance, gate.riskRewardMult, gate.riskFailPenalty, 7);
            Assert.AreEqual(4, halved);     // ⌈7/2⌉ — mesmo arredondamento do ÷2

            int floored = RiskGate.Resolve(new System.Random(1234),
                gate.riskSuccessChance, gate.riskRewardMult, gate.riskFailPenalty, 1);
            Assert.AreEqual(1, floored);    // nunca zera o exército
        }

        [Test]
        public void GateConfig_Apply_MatchesDomainGateMath()
        {
            // O SO delega/espelha o Domain — os dois nunca podem divergir (doc 12 §5.1).
            int[] currents = { 1, 2, 7, 8, 21, 60 };
            (GateType type, float value)[] gates =
            {
                (GateType.AddFlat, 10f),
                (GateType.AddFlat, 25f),
                (GateType.Multiply, 2f),
                (GateType.Multiply, 3f),
                (GateType.Multiply, 0.5f),
                (GateType.Element, 0f),
                (GateType.ClassConvert, 1f),
                (GateType.Risk, 0f)
            };

            foreach ((GateType type, float value) in gates)
            {
                var gate = MakeGate(type, value);
                foreach (int current in currents)
                {
                    Assert.AreEqual(GateMath.Apply(type, value, current), gate.Apply(current),
                        "Divergência SO vs Domain em " + type + " value=" + value + " current=" + current);
                }
            }
        }
    }
}
