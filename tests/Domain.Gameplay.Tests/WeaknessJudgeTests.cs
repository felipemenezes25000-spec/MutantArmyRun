using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // Missão Nota 10: classificador único do feedback FRAQUEZA!/RESISTIU!/IMUNE!
    // (CONTRACT §2). Limiares: >1.05 Weakness · <=0 Immune · <0.95 Resisted · senão Neutral.
    public class WeaknessJudgeTests
    {
        [Theory]
        [InlineData(1.5f)]    // ciclo Fogo>Gelo>Raio do ElementChart (+50%)
        [InlineData(1.06f)]   // logo acima da janela morta
        [InlineData(10f)]
        public void MultiplicadorAcimaDaJanela_EhFraqueza(float multiplier)
        {
            Assert.Equal(ElementRelation.Weakness, WeaknessJudge.Classify(multiplier));
        }

        [Theory]
        [InlineData(0.5f)]    // mesmo elemento −50% do ElementChart
        [InlineData(0.94f)]   // logo abaixo da janela morta
        [InlineData(0.01f)]   // quase zero mas ainda causa dano → resistiu, não imune
        public void MultiplicadorAbaixoDaJanela_EhResistido(float multiplier)
        {
            Assert.Equal(ElementRelation.Resisted, WeaknessJudge.Classify(multiplier));
        }

        [Theory]
        [InlineData(0f)]      // Veneno vs Machine/Undead (ElementChart.Default)
        [InlineData(-1f)]     // negativo defensivo: nunca "cura" o boss, classifica imune
        public void MultiplicadorZeroOuNegativo_EhImune(float multiplier)
        {
            Assert.Equal(ElementRelation.Immune, WeaknessJudge.Classify(multiplier));
        }

        [Theory]
        [InlineData(1f)]      // neutro exato
        [InlineData(1.02f)]   // ruído de tuning dentro da janela morta — sem texto
        [InlineData(0.98f)]
        public void MultiplicadorDentroDaJanelaMorta_EhNeutro(float multiplier)
        {
            Assert.Equal(ElementRelation.Neutral, WeaknessJudge.Classify(multiplier));
        }

        [Fact]
        public void Fronteiras_LimiaresExatos_FicamNeutros()
        {
            // 1.05 e 0.95 são INCLUSIVOS na janela morta (> e < estritos no contrato).
            Assert.Equal(ElementRelation.Neutral, WeaknessJudge.Classify(1.05f));
            Assert.Equal(ElementRelation.Neutral, WeaknessJudge.Classify(0.95f));
        }

        [Fact]
        public void Fronteira_ZeroExato_EhImune_NaoResistido()
        {
            // <=0 vence o <0.95: dano zero é IMUNE (informação acionável: troque de elemento).
            Assert.Equal(ElementRelation.Immune, WeaknessJudge.Classify(0f));
        }
    }
}
