using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // Doc 12 §4.4 (combate agregado, crítico ×2) e §4.5 (fases 0.5/0.25, waves por ponteiro).
    public class CombatMathTests
    {
        // ---- CrowdDps ----

        [Fact]
        public void CrowdDps_Neutro_RetornaBase()
        {
            Assert.Equal(100f, CombatMath.CrowdDps(100f, 1f, 0f, false));
        }

        [Fact]
        public void CrowdDps_ChartEBonusDeBoss_Multiplicam()
        {
            // 100 × 1.5 (chart) × 1.2 (+20% dano contra boss) = 180
            Assert.Equal(180f, CombatMath.CrowdDps(100f, 1.5f, 0.2f, false), 3);
        }

        [Fact]
        public void CrowdDps_Critico_DobraODano()
        {
            float semCrit = CombatMath.CrowdDps(100f, 1.5f, 0.2f, false);
            float comCrit = CombatMath.CrowdDps(100f, 1.5f, 0.2f, true);
            Assert.Equal(semCrit * 2f, comCrit, 3);
        }

        // ---- BossPhase: limiares canônicos 0.5/0.25 (doc 12 §4.5 PhaseThresholds) ----

        [Theory]
        [InlineData(100f, 100f, 0)]  // vida cheia
        [InlineData(60f, 100f, 0)]
        [InlineData(50f, 100f, 1)]   // atingiu o limiar de 50%
        [InlineData(30f, 100f, 1)]
        [InlineData(25f, 100f, 2)]   // atingiu o limiar de 25%
        [InlineData(10f, 100f, 2)]
        [InlineData(0f, 100f, 2)]
        public void BossPhase_LimiaresCanonicos(float hp, float maxHp, int expectedPhase)
        {
            Assert.Equal(expectedPhase, CombatMath.BossPhase(hp, maxHp));
        }

        // ---- WavePointer: lista ordenada consumida por ponteiro (nunca polling) ----

        private static ArenaWave[] TresWaves()
        {
            return new[]
            {
                new ArenaWave { time = 1f, enemyTypeId = 10, count = 3 },
                new ArenaWave { time = 2f, enemyTypeId = 11, count = 5 },
                new ArenaWave { time = 3f, enemyTypeId = 12, count = 7 }
            };
        }

        [Fact]
        public void WavePointer_DtGigante_DisparaTodasEmOrdem_Exatamente1Vez()
        {
            ArenaWave[] waves = TresWaves();
            int next = 0;
            int fired = WavePointer.Consume(1000f, waves, ref next);
            Assert.Equal(3, fired);
            Assert.Equal(3, next);

            // chamada seguinte não duplica nada
            Assert.Equal(0, WavePointer.Consume(2000f, waves, ref next));
            Assert.Equal(3, next);
        }

        [Fact]
        public void WavePointer_DtPequeno_NuncaDuplicaEventos()
        {
            ArenaWave[] waves = TresWaves();
            int next = 0;
            int total = 0;
            // simula FightTime avançando em passos de 0.05 s
            for (float t = 0f; t <= 3.5f; t += 0.05f)
                total += WavePointer.Consume(t, waves, ref next);
            Assert.Equal(3, total);
            Assert.Equal(3, next);
        }

        [Fact]
        public void WavePointer_AntesDoPrimeiroEvento_NaoDisparaNada()
        {
            ArenaWave[] waves = TresWaves();
            int next = 0;
            Assert.Equal(0, WavePointer.Consume(0.99f, waves, ref next));
            Assert.Equal(0, next);
        }

        [Fact]
        public void WavePointer_DisparaSomenteOsEventosVencidos()
        {
            ArenaWave[] waves = TresWaves();
            int next = 0;
            Assert.Equal(2, WavePointer.Consume(2.5f, waves, ref next));
            Assert.Equal(2, next);   // waves de t=1 e t=2; a de t=3 ainda pendente
        }
    }
}
