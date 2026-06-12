using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class MissionMathTests
    {
        // 2026-06-12 00:00:00 UTC como âncora; +segundos para variar dentro/fora do dia.
        private const long Day0 = 1_780_272_000L;   // meia-noite UTC de um dia-calendário
        private const long OneDay = 86_400L;

        // ---------- UtcDayIndex / IsNewUtcDay ----------

        [Fact]
        public void UtcDayIndex_MesmoDia_MesmoIndice()
        {
            Assert.Equal(MissionMath.UtcDayIndex(Day0), MissionMath.UtcDayIndex(Day0 + OneDay - 1));
        }

        [Fact]
        public void UtcDayIndex_DiaSeguinte_IndiceMaior()
        {
            Assert.Equal(MissionMath.UtcDayIndex(Day0) + 1, MissionMath.UtcDayIndex(Day0 + OneDay));
        }

        [Fact]
        public void IsNewUtcDay_MesmoDia_False_DiaSeguinte_True()
        {
            Assert.False(MissionMath.IsNewUtcDay(Day0, Day0 + 3600));        // mesma data UTC
            Assert.True(MissionMath.IsNewUtcDay(Day0, Day0 + OneDay));       // virou o dia
            Assert.True(MissionMath.IsNewUtcDay(0, Day0));                   // nunca reivindicado
        }

        // ---------- NextLoginStreak ----------

        [Fact]
        public void NextLoginStreak_PrimeiroLogin_RetornaUm()
        {
            Assert.Equal(1, MissionMath.NextLoginStreak(0, 0, Day0));
        }

        [Fact]
        public void NextLoginStreak_DiaSeguinte_Incrementa()
        {
            Assert.Equal(4, MissionMath.NextLoginStreak(3, Day0, Day0 + OneDay));
        }

        [Fact]
        public void NextLoginStreak_MesmoDia_NaoMuda()
        {
            Assert.Equal(3, MissionMath.NextLoginStreak(3, Day0, Day0 + 3600));
        }

        [Fact]
        public void NextLoginStreak_GapDeDoisDias_Reseta()
        {
            Assert.Equal(1, MissionMath.NextLoginStreak(5, Day0, Day0 + 2 * OneDay));
        }

        // ---------- Calendário de 7 dias ----------

        [Theory]
        [InlineData(1, 0)]
        [InlineData(7, 6)]
        [InlineData(8, 0)]    // ciclo reinicia
        [InlineData(15, 0)]
        public void LoginCycleDay_CiclaACada7(int streak, int esperado)
        {
            Assert.Equal(esperado, MissionMath.LoginCycleDay(streak));
        }

        [Fact]
        public void LoginRewardCoins_CresceNoCiclo()
        {
            Assert.Equal(100L, MissionMath.LoginRewardCoins(1));
            Assert.Equal(1000L, MissionMath.LoginRewardCoins(7));   // dia 7 = pico
            Assert.Equal(100L, MissionMath.LoginRewardCoins(8));    // reinicia o ciclo
        }

        [Fact]
        public void LoginRewardGems_DiasIntermediariosEFinalDoCiclo()
        {
            Assert.Equal(0, MissionMath.LoginRewardGems(1));
            Assert.Equal(5, MissionMath.LoginRewardGems(3));
            Assert.Equal(10, MissionMath.LoginRewardGems(5));
            Assert.Equal(20, MissionMath.LoginRewardGems(7));
        }

        // ---------- Missões diárias ----------

        [Fact]
        public void MissionCoins_AplicaMultiplicadorDeMundo()
        {
            Assert.Equal(100L, MissionMath.MissionCoins(1f));
            Assert.Equal(160L, MissionMath.MissionCoins(1.6f));
            Assert.Equal(100L, MissionMath.MissionCoins(0.5f));   // clamp mínimo Mb=1
        }

        [Theory]
        [InlineData(0, 3, false)]
        [InlineData(2, 3, false)]
        [InlineData(3, 3, true)]
        [InlineData(5, 3, true)]
        [InlineData(1, 0, false)]   // alvo inválido nunca completa
        public void IsComplete_ProgressoVsAlvo(int progress, int target, bool esperado)
        {
            Assert.Equal(esperado, MissionMath.IsComplete(progress, target));
        }
    }
}
