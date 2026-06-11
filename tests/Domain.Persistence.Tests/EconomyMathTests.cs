using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class EconomyMathTests
    {
        // ---------- LevelReward — CANON §8: fase 1 = 100; cresce 1,10^(fase−1) ----------

        [Fact]
        public void LevelReward_Fase1_Da100Moedas()
        {
            Assert.Equal(100, EconomyMath.LevelReward(1));
        }

        [Fact]
        public void LevelReward_Fase10_DaAprox236()
        {
            // âncora canônica: 100 × 1,10^9 = 235,79… → 236
            Assert.Equal(236, EconomyMath.LevelReward(10));
        }

        [Theory]
        [InlineData(2, 110)]
        [InlineData(3, 121)]
        [InlineData(5, 146)]   // 100 × 1,1^4 = 146,41 → 146
        public void LevelReward_SegueACurvaCanonica(int fase, int esperado)
        {
            Assert.Equal(esperado, EconomyMath.LevelReward(fase));
        }

        [Fact]
        public void LevelReward_AplicaMultiplicadorDeRecompensa()
        {
            // mult vem de 1 + TrackBonus(RewardMultiplier, nível) na camada Unity
            Assert.Equal(150, EconomyMath.LevelReward(1, mult: 1.5f));
        }

        [Fact]
        public void LevelReward_ParametrosRecalibraveisPorRemoteConfig()
        {
            Assert.Equal(200, EconomyMath.LevelReward(1, baseReward: 200f));
            Assert.Equal(400, EconomyMath.LevelReward(2, baseReward: 200f, growth: 2f));
        }

        // ---------- UpgradeCost — CANON §8/§9: custo(n) = 100 × 1,35^n ----------

        [Fact]
        public void UpgradeCost_Nivel0_Custa100()
        {
            Assert.Equal(100, EconomyMath.UpgradeCost(0));
        }

        [Theory]
        [InlineData(1, 135)]
        [InlineData(2, 182)]   // 182,25 → 182
        [InlineData(3, 246)]   // 246,04 → 246
        [InlineData(4, 332)]   // 332,15 → 332
        public void UpgradeCost_SegueACurvaCanonica(int nivel, int esperado)
        {
            Assert.Equal(esperado, EconomyMath.UpgradeCost(nivel));
        }

        [Fact]
        public void UpgradeCost_ParametrosRecalibraveis()
        {
            Assert.Equal(50, EconomyMath.UpgradeCost(0, costBase: 50f));
            Assert.Equal(100, EconomyMath.UpgradeCost(1, costBase: 50f, growth: 2f));
        }

        // ---------- TrackBonus — CANON §9: +5%/nível; StartArmy = +1 unidade a cada 2 níveis ----------

        [Theory]
        [InlineData(UpgradeTrack.StartDamage, 1, 0.05f)]
        [InlineData(UpgradeTrack.StartHealth, 4, 0.20f)]
        [InlineData(UpgradeTrack.Speed, 2, 0.10f)]
        [InlineData(UpgradeTrack.RewardMultiplier, 10, 0.50f)]
        [InlineData(UpgradeTrack.CritChance, 3, 0.15f)]
        [InlineData(UpgradeTrack.BossDamage, 0, 0f)]
        [InlineData(UpgradeTrack.ObstacleResist, 6, 0.30f)]
        public void TrackBonus_TrilhasPercentuais_5PorCentoPorNivel(UpgradeTrack trilha, int nivel, float esperado)
        {
            Assert.Equal(esperado, EconomyMath.TrackBonus(trilha, nivel), 4);
        }

        [Theory]
        [InlineData(0, 0f)]
        [InlineData(1, 0f)]
        [InlineData(2, 1f)]
        [InlineData(3, 1f)]
        [InlineData(4, 2f)]
        [InlineData(10, 5f)]
        public void TrackBonus_StartArmy_Mais1UnidadeACada2Niveis(int nivel, float unidadesEsperadas)
        {
            // retorna UNIDADES inteiras, não percentual
            Assert.Equal(unidadesEsperadas, EconomyMath.TrackBonus(UpgradeTrack.StartArmy, nivel), 4);
        }

        [Fact]
        public void TrackBonus_NivelNegativo_RetornaZero()
        {
            Assert.Equal(0f, EconomyMath.TrackBonus(UpgradeTrack.StartDamage, -1), 4);
            Assert.Equal(0f, EconomyMath.TrackBonus(UpgradeTrack.StartArmy, -3), 4);
        }

        // ---------- ShardsToLevel — CANON §8: evoluir de n para n+1 custa 10 × 2^(n−1) ----------

        [Theory]
        [InlineData(1, 10)]
        [InlineData(2, 20)]
        [InlineData(3, 40)]
        [InlineData(5, 160)]
        [InlineData(9, 2560)]  // último upgrade possível (nível máximo 10)
        public void ShardsToLevel_DobraACadaNivel(int nivel, int esperado)
        {
            Assert.Equal(esperado, EconomyMath.ShardsToLevel(nivel));
        }

        [Fact]
        public void ShardsToLevel_NivelInvalido_RetornaZero()
        {
            Assert.Equal(0, EconomyMath.ShardsToLevel(0));
            Assert.Equal(0, EconomyMath.ShardsToLevel(-1));
        }
    }
}
