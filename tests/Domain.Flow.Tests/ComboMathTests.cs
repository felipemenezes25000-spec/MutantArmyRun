using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    // Missão Nota 10 (CONTRACT §2): combos de fim de corrida — cada regra dispara no caso
    // certo e NÃO dispara fora dele; bônus de moedas é tabela fixa; buffer é do chamador.
    public class ComboMathTests
    {
        private static ComboKind[] NovoBuffer() => new ComboKind[6];

        // Corrida "morna": venceu, mas sem nenhum combo (escolhas imperfeitas, perdas na
        // pista, luta longa, muitos sobreviventes, sem weakness hit nem overkill).
        private static ComboMath.RunComboStats SemCombos() => new ComboMath.RunComboStats
        {
            bestGateChoices = 2,
            totalGateChoices = 5,
            weaknessHits = 0,
            unitsLostOnTrack = 8,
            survivors = 30,
            armyPeak = 50,
            bossFightSeconds = 20f,
            overkillDamage = 0f,
            bossMaxHp = 1000f
        };

        // ---------------------------------------------------------------- PerfectGate

        [Fact]
        public void PerfectGate_TodasAsEscolhasOtimas_Dispara()
        {
            var s = SemCombos();
            s.bestGateChoices = 5;
            s.totalGateChoices = 5;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.PerfectGate, buffer[0]);
        }

        [Fact]
        public void PerfectGate_UmaEscolhaErrada_NaoDispara()
        {
            var s = SemCombos();
            s.bestGateChoices = 4;
            s.totalGateChoices = 5;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void PerfectGate_FaseSemPortais_NaoDispara()
        {
            // 0 == 0 mas totalGateChoices > 0 é exigido: fase sem portal não é "perfeita".
            var s = SemCombos();
            s.bestGateChoices = 0;
            s.totalGateChoices = 0;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void PerfectGate_PagaMesmoEmDerrota()
        {
            // Combo de LEITURA DE ROTA não exige vitória — reforço positivo, nunca punição.
            var s = SemCombos();
            s.bestGateChoices = 3;
            s.totalGateChoices = 3;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: false, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.PerfectGate, buffer[0]);
        }

        // ---------------------------------------------------------------- WeaknessHit

        [Fact]
        public void WeaknessHit_AoMenosUmGolpeDeFraqueza_Dispara()
        {
            var s = SemCombos();
            s.weaknessHits = 1;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.WeaknessHit, buffer[0]);
        }

        [Fact]
        public void WeaknessHit_SemGolpeDeFraqueza_NaoDispara()
        {
            var s = SemCombos();
            s.weaknessHits = 0;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        // ---------------------------------------------------------------- BossBreaker

        [Fact]
        public void BossBreaker_LutaDe8SegundosExatos_Dispara()
        {
            // fronteira inclusiva: <= 8 s
            var s = SemCombos();
            s.bossFightSeconds = 8f;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.BossBreaker, buffer[0]);
        }

        [Fact]
        public void BossBreaker_LutaAcimaDe8Segundos_NaoDispara()
        {
            var s = SemCombos();
            s.bossFightSeconds = 8.01f;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void BossBreaker_DuracaoZero_NaoDispara()
        {
            // 0 s = dado não medido (defensivo) — nunca premiar medição ausente.
            var s = SemCombos();
            s.bossFightSeconds = 0f;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void BossBreaker_EmDerrota_NaoDispara()
        {
            var s = SemCombos();
            s.bossFightSeconds = 5f;
            Assert.Equal(0, ComboMath.Evaluate(s, won: false, NovoBuffer()));
        }

        // ---------------------------------------------------------------- Clutch

        [Fact]
        public void Clutch_SobreviventesAteDezPorCentoDoPico_Dispara()
        {
            // pico 50 → limite ⌊50×0.1⌋ = 5 sobreviventes
            var s = SemCombos();
            s.armyPeak = 50;
            s.survivors = 5;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.Clutch, buffer[0]);
        }

        [Fact]
        public void Clutch_UmSobreviventeAcimaDoLimite_NaoDispara()
        {
            var s = SemCombos();
            s.armyPeak = 50;
            s.survivors = 6;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void Clutch_PicoPequeno_PisoDeUmSobrevivente()
        {
            // pico 5 → 10% seria 0, mas o piso Math.Max(1, ...) permite o clutch com 1 vivo
            var s = SemCombos();
            s.armyPeak = 5;
            s.survivors = 1;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.Clutch, buffer[0]);
        }

        [Fact]
        public void Clutch_SemSobreviventesOuSemPico_NaoDispara()
        {
            var s = SemCombos();
            s.armyPeak = 50;
            s.survivors = 0;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));

            var s2 = SemCombos();
            s2.armyPeak = 0;
            s2.survivors = 1;
            Assert.Equal(0, ComboMath.Evaluate(s2, won: true, NovoBuffer()));
        }

        // ---------------------------------------------------------------- NoLoss

        [Fact]
        public void NoLoss_ZeroPerdasNaPista_Dispara()
        {
            var s = SemCombos();
            s.unitsLostOnTrack = 0;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.NoLoss, buffer[0]);
        }

        [Fact]
        public void NoLoss_UmaPerda_NaoDispara()
        {
            var s = SemCombos();
            s.unitsLostOnTrack = 1;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void NoLoss_EmDerrota_NaoDispara()
        {
            var s = SemCombos();
            s.unitsLostOnTrack = 0;
            Assert.Equal(0, ComboMath.Evaluate(s, won: false, NovoBuffer()));
        }

        // ---------------------------------------------------------------- Overkill

        [Fact]
        public void Overkill_DanoExcedenteDe25PorCento_Dispara()
        {
            // fronteira inclusiva: >= bossMaxHp × 0.25
            var s = SemCombos();
            s.bossMaxHp = 1000f;
            s.overkillDamage = 250f;
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(1, count);
            Assert.Equal(ComboKind.Overkill, buffer[0]);
        }

        [Fact]
        public void Overkill_AbaixoDoLimiar_NaoDispara()
        {
            var s = SemCombos();
            s.bossMaxHp = 1000f;
            s.overkillDamage = 249f;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        [Fact]
        public void Overkill_BossSemHpRegistrado_NaoDispara()
        {
            // bossMaxHp 0 (dado ausente) nunca dispara — evita divisão moral por zero.
            var s = SemCombos();
            s.bossMaxHp = 0f;
            s.overkillDamage = 9999f;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, NovoBuffer()));
        }

        // ---------------------------------------------------------------- Buffer e ordem

        [Fact]
        public void CorridaPerfeita_DisparaOsSeisCombos_NaOrdemDoEnum()
        {
            var s = new ComboMath.RunComboStats
            {
                bestGateChoices = 5,
                totalGateChoices = 5,
                weaknessHits = 3,
                unitsLostOnTrack = 0,
                survivors = 5,
                armyPeak = 50,
                bossFightSeconds = 6f,
                overkillDamage = 300f,
                bossMaxHp = 1000f
            };
            var buffer = NovoBuffer();
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(6, count);
            Assert.Equal(ComboKind.PerfectGate, buffer[0]);
            Assert.Equal(ComboKind.WeaknessHit, buffer[1]);
            Assert.Equal(ComboKind.BossBreaker, buffer[2]);
            Assert.Equal(ComboKind.Clutch, buffer[3]);
            Assert.Equal(ComboKind.NoLoss, buffer[4]);
            Assert.Equal(ComboKind.Overkill, buffer[5]);
        }

        [Fact]
        public void BufferCurto_TruncaSemLancar()
        {
            // corrida de 6 combos com buffer de 2: escreve 2, retorna 2, nunca lança
            var s = new ComboMath.RunComboStats
            {
                bestGateChoices = 5,
                totalGateChoices = 5,
                weaknessHits = 3,
                unitsLostOnTrack = 0,
                survivors = 5,
                armyPeak = 50,
                bossFightSeconds = 6f,
                overkillDamage = 300f,
                bossMaxHp = 1000f
            };
            var buffer = new ComboKind[2];
            int count = ComboMath.Evaluate(s, won: true, buffer);
            Assert.Equal(2, count);
            Assert.Equal(ComboKind.PerfectGate, buffer[0]);
            Assert.Equal(ComboKind.WeaknessHit, buffer[1]);
        }

        [Fact]
        public void BufferNull_RetornaZeroSemLancar()
        {
            var s = SemCombos();
            s.weaknessHits = 1;
            Assert.Equal(0, ComboMath.Evaluate(s, won: true, null));
        }

        // ---------------------------------------------------------------- BonusCoins

        [Theory]
        [InlineData(ComboKind.PerfectGate, 25)]
        [InlineData(ComboKind.WeaknessHit, 15)]
        [InlineData(ComboKind.BossBreaker, 40)]
        [InlineData(ComboKind.Clutch, 50)]
        [InlineData(ComboKind.NoLoss, 30)]
        [InlineData(ComboKind.Overkill, 20)]
        public void BonusCoins_TabelaCanonicaDoContrato(ComboKind kind, int esperado)
        {
            Assert.Equal(esperado, ComboMath.BonusCoins(kind));
        }

        [Fact]
        public void BonusCoins_ValorForaDoEnum_RetornaZero()
        {
            Assert.Equal(0, ComboMath.BonusCoins((ComboKind)999));
        }
    }
}
