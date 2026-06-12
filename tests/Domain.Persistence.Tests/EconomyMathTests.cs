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

        // ---------- EvolveCoinCost — doc 07 §6: 100 × 2^(n−1) × raridade ----------

        [Theory]
        [InlineData(1, 1, 100)]    // Comum 1→2
        [InlineData(2, 1, 200)]    // Comum 2→3
        [InlineData(3, 1, 400)]
        [InlineData(9, 1, 25600)]  // Comum 9→10 (último válido)
        [InlineData(1, 2, 200)]    // Raro 1→2
        [InlineData(1, 4, 400)]    // Épico 1→2
        [InlineData(1, 8, 800)]    // Lendário 1→2
        [InlineData(9, 8, 204800)] // Lendário 9→10
        public void EvolveCoinCost_SegueCurvaCanonicaPorRaridade(int n, int rarityMult, long esperado)
        {
            Assert.Equal(esperado, EconomyMath.EvolveCoinCost(n, rarityMult));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]   // nv máx é 10: não há evolução 10→11
        [InlineData(-1)]
        public void EvolveCoinCost_NivelForaDaFaixa_RetornaZero(int n)
        {
            Assert.Equal(0L, EconomyMath.EvolveCoinCost(n, 1));
        }

        [Theory]
        [InlineData(Rarity.Common, 1)]
        [InlineData(Rarity.Rare, 2)]
        [InlineData(Rarity.Epic, 4)]
        [InlineData(Rarity.Legendary, 8)]
        public void RarityCoinMultiplier_EscadaCanonica(Rarity rarity, int esperado)
        {
            Assert.Equal(esperado, EconomyMath.RarityCoinMultiplier(rarity));
        }

        // ---------- PlayerLevelXpThreshold — doc 07 §3.3 (marcos do CANON §16) ----------

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 120)]
        [InlineData(3, 220)]
        [InlineData(4, 380)]
        [InlineData(5, 550)]
        [InlineData(10, 2350)]
        [InlineData(20, 17400)]
        public void PlayerLevelXpThreshold_BateNosMarcosCanonicos(int level, int esperado)
        {
            Assert.Equal(esperado, EconomyMath.PlayerLevelXpThreshold(level));
        }

        [Fact]
        public void PlayerLevelXpThreshold_EhMonotonicaInclusiveAlemDaTabela()
        {
            int prev = -1;
            for (int l = 1; l <= 40; l++)
            {
                int xp = EconomyMath.PlayerLevelXpThreshold(l);
                Assert.True(xp > prev, $"nível {l}: {xp} deveria ser > {prev}");
                prev = xp;
            }
        }

        [Fact]
        public void PlayerLevelXpToNext_EhODeltaDoLimiar()
        {
            Assert.Equal(120, EconomyMath.PlayerLevelXpToNext(1));   // 1→2
            Assert.Equal(100, EconomyMath.PlayerLevelXpToNext(2));   // 2→3 (220−120)
            Assert.Equal(160, EconomyMath.PlayerLevelXpToNext(3));   // 3→4 (380−220)
        }

        // ---------- SpeedRunMultiplier — doc 07 §5.3: +5%/nível, cap +50% (nv 10) ----------

        [Theory]
        [InlineData(0, 1.00f)]
        [InlineData(1, 1.05f)]
        [InlineData(10, 1.50f)]   // cap exato
        [InlineData(20, 1.50f)]   // além do cap: corrida estabiliza
        public void SpeedRunMultiplier_CapaEmMais50PorCento(int level, float esperado)
        {
            Assert.Equal(esperado, EconomyMath.SpeedRunMultiplier(level), 4);
        }

        // ---------- ObstacleLossFactor — doc 07 §5.3: composto 0.95^nível, nunca imune ----------

        [Fact]
        public void ObstacleLossFactor_Composto_NuncaChegaAZero()
        {
            Assert.Equal(1f, EconomyMath.ObstacleLossFactor(0), 4);
            Assert.Equal(0.95f, EconomyMath.ObstacleLossFactor(1), 4);
            // nv 5 ⇒ 0.95^5 ≈ 0.7738 (≈ −23% de perdas, doc 07 §5.4)
            Assert.Equal(0.7738f, EconomyMath.ObstacleLossFactor(5), 3);
            Assert.True(EconomyMath.ObstacleLossFactor(30) > 0f);   // assintótico, nunca imunidade
        }
    }
}
