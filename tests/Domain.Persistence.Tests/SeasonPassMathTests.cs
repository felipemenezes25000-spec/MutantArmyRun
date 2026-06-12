using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class SeasonPassMathTests
    {
        // ---------- TierForXp / XpIntoTier / progresso ----------

        [Theory]
        [InlineData(0, 1)]
        [InlineData(50, 1)]
        [InlineData(99, 1)]
        [InlineData(100, 2)]
        [InlineData(250, 3)]
        public void TierForXp_ConverteXpEmNivel(long xp, int esperado)
        {
            Assert.Equal(esperado, SeasonPassMath.TierForXp(xp));
        }

        [Fact]
        public void TierForXp_SaturaNoTeto()
        {
            long acima = (long)SeasonPassMath.TierCount * SeasonPassMath.XpPerTier + 9999L;
            Assert.Equal(SeasonPassMath.TierCount, SeasonPassMath.TierForXp(acima));
        }

        [Fact]
        public void XpIntoTier_DentroDoNivel()
        {
            Assert.Equal(0, SeasonPassMath.XpIntoTier(0));
            Assert.Equal(50, SeasonPassMath.XpIntoTier(150));     // nível 2, 50 dentro
            Assert.Equal(0, SeasonPassMath.XpIntoTier(100));      // exatamente o início do nível 2
        }

        [Fact]
        public void XpIntoTier_NoTeto_BarraCheia()
        {
            long teto = (long)SeasonPassMath.TierCount * SeasonPassMath.XpPerTier + 500L;
            Assert.Equal(SeasonPassMath.XpPerTier, SeasonPassMath.XpIntoTier(teto));
            Assert.Equal(1f, SeasonPassMath.TierProgress01(teto), 3);
        }

        [Fact]
        public void TierProgress01_FracaoCorreta()
        {
            Assert.Equal(0f, SeasonPassMath.TierProgress01(0), 3);
            Assert.Equal(0.5f, SeasonPassMath.TierProgress01(150), 3);   // meio do nível 2
        }

        // ---------- XpToReachTier / IsTierReached ----------

        [Theory]
        [InlineData(1, 0L)]
        [InlineData(2, 100L)]
        [InlineData(5, 400L)]
        public void XpToReachTier_AcumuladoLinear(int tier, long esperado)
        {
            Assert.Equal(esperado, SeasonPassMath.XpToReachTier(tier));
        }

        [Fact]
        public void IsTierReached_RespeitaLimiar()
        {
            Assert.True(SeasonPassMath.IsTierReached(1, 0));
            Assert.False(SeasonPassMath.IsTierReached(3, 199));   // precisa de 200
            Assert.True(SeasonPassMath.IsTierReached(3, 200));
        }

        // ---------- Tabela de recompensas: GRÁTIS ----------

        [Fact]
        public void FreeReward_NivelComum_DaMoedas()
        {
            SeasonReward r = SeasonPassMath.FreeReward(1);
            Assert.Equal(SeasonRewardKind.Coins, r.kind);
            Assert.Equal(100, r.amount);
            Assert.True(r.HasReward);
        }

        [Fact]
        public void FreeReward_Marco5_DaFragmentos()
        {
            SeasonReward r = SeasonPassMath.FreeReward(5);
            Assert.Equal(SeasonRewardKind.Shards, r.kind);
            Assert.Equal(5, r.amount);
        }

        [Fact]
        public void FreeReward_Marco10_DaGemas()
        {
            SeasonReward r = SeasonPassMath.FreeReward(10);
            Assert.Equal(SeasonRewardKind.Gems, r.kind);
            Assert.True(r.amount > 0);
        }

        [Fact]
        public void FreeReward_NivelInvalido_None()
        {
            Assert.Equal(SeasonRewardKind.None, SeasonPassMath.FreeReward(0).kind);
            Assert.Equal(SeasonRewardKind.None, SeasonPassMath.FreeReward(SeasonPassMath.TierCount + 1).kind);
        }

        // ---------- Tabela de recompensas: PREMIUM ----------

        [Fact]
        public void PremiumReward_UltimoNivel_DaSkin()
        {
            SeasonReward r = SeasonPassMath.PremiumReward(SeasonPassMath.TierCount);
            Assert.Equal(SeasonRewardKind.Skin, r.kind);
            Assert.True(r.HasReward);   // skin conta mesmo com amount tratado especial
        }

        [Fact]
        public void PremiumReward_SempreMaiorQueGratis_EmMoedas()
        {
            // Em níveis "comuns" (sem marco), o premium paga mais moedas que o grátis.
            Assert.True(SeasonPassMath.PremiumCoins(1) > SeasonPassMath.FreeCoins(1));
            Assert.True(SeasonPassMath.PremiumCoins(7) > SeasonPassMath.FreeCoins(7));
        }

        [Fact]
        public void Coins_CrescemComNivel()
        {
            Assert.True(SeasonPassMath.FreeCoins(2) > SeasonPassMath.FreeCoins(1));
            Assert.True(SeasonPassMath.PremiumCoins(2) > SeasonPassMath.PremiumCoins(1));
        }

        [Fact]
        public void TodosOsNiveis_TemRecompensaEmAmbasTrilhas()
        {
            for (int t = 1; t <= SeasonPassMath.TierCount; t++)
            {
                Assert.True(SeasonPassMath.FreeReward(t).HasReward, $"grátis nível {t} vazio");
                Assert.True(SeasonPassMath.PremiumReward(t).HasReward, $"premium nível {t} vazio");
            }
        }
    }
}
