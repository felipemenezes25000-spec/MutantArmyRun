using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // Doc 12 §4.3: portal de risco com System.Random INJETADO (nunca UnityEngine.Random);
    // odds exibidas são as odds reais (CANON §3.4 — portais honestos).
    public class RiskGateTests
    {
        [Fact]
        public void Resolve_Chance100PorCento_SempreAplicaRecompensa()
        {
            var rng = new Random(1);
            // "x10 se sobreviver" sobre 7 unidades → total-alvo 70
            Assert.Equal(70, RiskGate.Resolve(rng, 1f, 10f, 0.5f, 7));
        }

        [Fact]
        public void Resolve_Chance0PorCento_SempreAplicaPenalidade()
        {
            var rng = new Random(1);
            // "perde metade" sobre 7 → ⌈7×0.5⌉ = 4 (arredonda a favor do jogador)
            Assert.Equal(4, RiskGate.Resolve(rng, 0f, 10f, 0.5f, 7));
        }

        [Fact]
        public void Resolve_Falha_NuncaZeraOExercito()
        {
            var rng = new Random(1);
            Assert.Equal(1, RiskGate.Resolve(rng, 0f, 10f, 0.5f, 1));
            // penalidade total (×0) também respeita o piso de 1
            var rng2 = new Random(1);
            Assert.Equal(1, RiskGate.Resolve(rng2, 0f, 10f, 0f, 5));
        }

        [Fact]
        public void Resolve_SeedFixa_EDeterministico()
        {
            int a = RiskGate.Resolve(new Random(42), 0.7f, 10f, 0.5f, 10);
            int b = RiskGate.Resolve(new Random(42), 0.7f, 10f, 0.5f, 10);
            Assert.Equal(a, b);
        }

        [Fact]
        public void Resolve_10MilAmostras_ProporcaoDeSucessoRespeitaAsOddsExibidas()
        {
            // CANON §3.4: o RNG usa as odds mostradas. Com chance 0.7, a proporção
            // observada em 10.000 amostras fica em 0.7 ± 0.02.
            var rng = new Random(12345);
            const int samples = 10000;
            int successes = 0;
            for (int i = 0; i < samples; i++)
            {
                // sucesso (×10 sobre 10 → 100) é distinguível da falha (×0.5 → 5)
                if (RiskGate.Resolve(rng, 0.7f, 10f, 0.5f, 10) == 100) successes++;
            }
            double proportion = successes / (double)samples;
            Assert.InRange(proportion, 0.68, 0.72);
        }

        [Fact]
        public void Resolve_UsaSemanticaDeTotalAlvo_NuncaDelta()
        {
            // mesmo bug "x2 que triplica" do GateMath: ×10 sobre 7 é 70, nunca 7 + 70 = 77
            var rng = new Random(1);
            int result = RiskGate.Resolve(rng, 1f, 10f, 0.5f, 7);
            Assert.NotEqual(77, result);
            Assert.Equal(70, result);
        }
    }
}
