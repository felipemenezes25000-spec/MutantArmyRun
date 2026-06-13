using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    // Missão Nota 10 (CONTRACT §2): variante rara de boss — roll determinístico por seed
    // (System.Random INJETADO, padrão RiskGate) e chance clampada em 0..0.25.
    public class RareBossMathTests
    {
        [Fact]
        public void Roll_SeedFixa_EDeterministico()
        {
            // mesma seed + mesma chance = mesmo resultado (QA reproduz boss raro por seed)
            bool a = RareBossMath.Roll(new Random(42), 0.1f);
            bool b = RareBossMath.Roll(new Random(42), 0.1f);
            Assert.Equal(a, b);
        }

        [Fact]
        public void Roll_ChanceZero_NuncaRaro()
        {
            for (int seed = 0; seed < 200; seed++)
                Assert.False(RareBossMath.Roll(new Random(seed), 0f));
        }

        [Fact]
        public void Roll_ChanceNegativa_ClampaEmZero_NuncaRaro()
        {
            for (int seed = 0; seed < 200; seed++)
                Assert.False(RareBossMath.Roll(new Random(seed), -1f));
        }

        [Fact]
        public void Roll_ChanceAcimaDoTeto_ClampaEm25PorCento()
        {
            // chance 1.0 SEM clamp daria 100% de raro; com o teto, chance 1.0 e 0.25
            // produzem EXATAMENTE a mesma sequência de resultados por seed.
            for (int seed = 0; seed < 500; seed++)
            {
                bool comTeto = RareBossMath.Roll(new Random(seed), 1f);
                bool noTeto = RareBossMath.Roll(new Random(seed), 0.25f);
                Assert.Equal(noTeto, comTeto);
            }
        }

        [Fact]
        public void Roll_ChanceUm_NaoEh100PorCento()
        {
            // prova direta do teto: com clamp em 0.25, alguma seed precisa falhar o roll
            int rare = 0;
            const int samples = 1000;
            for (int seed = 0; seed < samples; seed++)
                if (RareBossMath.Roll(new Random(seed), 1f)) rare++;
            Assert.True(rare < samples, "chance 1.0 sem clamp daria raro em 100% das seeds");
        }

        [Fact]
        public void Roll_10MilAmostras_ProporcaoRespeitaAChance()
        {
            // padrão RiskGateTests: com chance 0.2, proporção observada fica em 0.2 ± 0.02
            var rng = new Random(12345);
            const int samples = 10000;
            int rare = 0;
            for (int i = 0; i < samples; i++)
                if (RareBossMath.Roll(rng, 0.2f)) rare++;
            double proportion = rare / (double)samples;
            Assert.InRange(proportion, 0.18, 0.22);
        }

        [Fact]
        public void Roll_RngNull_DegradaParaFalse_SemLancar()
        {
            // null-safety greybox-friendly: nunca quebra, só nunca é raro
            Assert.False(RareBossMath.Roll(null, 0.25f));
        }

        [Fact]
        public void Multiplicadores_SaoOsDoContrato()
        {
            // HP ×1.5 e recompensa ×3 (CONTRACT §2 — variante mais dura, prêmio que compensa)
            Assert.Equal(1.5f, RareBossMath.HpMultiplier, 3);
            Assert.Equal(3f, RareBossMath.RewardMultiplier, 3);
        }
    }
}
