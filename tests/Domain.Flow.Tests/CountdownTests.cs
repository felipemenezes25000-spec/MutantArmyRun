using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    public class CountdownTests
    {
        [Fact]
        public void RecemCriado_RemainingZero_EDoneTrue()
        {
            var c = new Countdown();

            Assert.Equal(0f, c.Remaining);
            Assert.True(c.Done);
        }

        [Fact]
        public void Tick_AntesDeSet_EhNoOpSeguro()
        {
            // Doc 12 §4.5: timer puro tickado externamente — tick antes de armar não pode
            // lançar nem deixar Remaining negativo.
            var c = new Countdown();

            c.Tick(1f);
            c.Tick(100f);

            Assert.Equal(0f, c.Remaining);
            Assert.True(c.Done);
        }

        [Fact]
        public void Set_ArmaOTimer_DoneFicaFalse()
        {
            var c = new Countdown();

            c.Set(3f);

            Assert.Equal(3f, c.Remaining);
            Assert.False(c.Done);
        }

        [Fact]
        public void Tick_DecrementaRemaining()
        {
            var c = new Countdown();
            c.Set(3f);

            c.Tick(1f);

            Assert.Equal(2f, c.Remaining);
            Assert.False(c.Done);
        }

        [Fact]
        public void Tick_AlemDoRestante_ClampaEmZero_NuncaNegativo()
        {
            var c = new Countdown();
            c.Set(1f);

            c.Tick(5f);

            Assert.Equal(0f, c.Remaining);
            Assert.True(c.Done);
        }

        [Fact]
        public void Tick_AcumuladoEmPassosPequenos_TerminaExatamenteEmZero()
        {
            var c = new Countdown();
            c.Set(0.5f);

            for (int i = 0; i < 10; i++) c.Tick(0.1f);

            Assert.Equal(0f, c.Remaining);
            Assert.True(c.Done);
        }

        [Fact]
        public void Set_ValorNegativo_ClampaEmZero()
        {
            // Invariante "nunca negativo" vale para Set também — telegraph nunca arma "no passado".
            var c = new Countdown();

            c.Set(-2f);

            Assert.Equal(0f, c.Remaining);
            Assert.True(c.Done);
        }

        [Fact]
        public void Set_DepoisDeDone_RearmaOTimer()
        {
            // Cooldown do especial do boss é re-armado várias vezes na mesma luta (doc 12 §4.5).
            var c = new Countdown();
            c.Set(1f);
            c.Tick(2f);
            Assert.True(c.Done);

            c.Set(4f);

            Assert.Equal(4f, c.Remaining);
            Assert.False(c.Done);
        }
    }
}
