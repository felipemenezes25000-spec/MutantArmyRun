using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // CANON §4: ciclo Fogo > Gelo > Raio > Fogo (+50%), mesmo elemento −50%,
    // Veneno 0% vs máquinas/mortos-vivos e +50% vs orgânicos, None neutro.
    public class ElementChartTests
    {
        private readonly ElementChart _chart = ElementChart.Default();

        [Theory]
        [InlineData(ElementType.Fire, ElementType.Ice)]
        [InlineData(ElementType.Ice, ElementType.Lightning)]
        [InlineData(ElementType.Lightning, ElementType.Fire)]
        public void GetMultiplier_CicloPrincipal_Da1Ponto5(ElementType atk, ElementType def)
        {
            Assert.Equal(1.5f, _chart.GetMultiplier(atk, def));
        }

        [Theory]
        [InlineData(ElementType.Ice, ElementType.Fire)]
        [InlineData(ElementType.Lightning, ElementType.Ice)]
        [InlineData(ElementType.Fire, ElementType.Lightning)]
        public void GetMultiplier_InversoDoCiclo_ENeutro(ElementType atk, ElementType def)
        {
            Assert.Equal(1.0f, _chart.GetMultiplier(atk, def));
        }

        [Theory]
        [InlineData(ElementType.Fire)]
        [InlineData(ElementType.Ice)]
        [InlineData(ElementType.Lightning)]
        [InlineData(ElementType.Poison)]
        public void GetMultiplier_MesmoElemento_DaMetadeDoDano(ElementType e)
        {
            // Fogo vs boss de lava = péssimo (CANON §4)
            Assert.Equal(0.5f, _chart.GetMultiplier(e, e));
        }

        [Fact]
        public void GetBodyMultiplier_VenenoVsMaquina_DaZero()
        {
            Assert.Equal(0f, _chart.GetBodyMultiplier(ElementType.Poison, BodyType.Machine));
        }

        [Fact]
        public void GetBodyMultiplier_VenenoVsMortoVivo_DaZero()
        {
            Assert.Equal(0f, _chart.GetBodyMultiplier(ElementType.Poison, BodyType.Undead));
        }

        [Fact]
        public void GetBodyMultiplier_VenenoVsOrganico_Da1Ponto5()
        {
            Assert.Equal(1.5f, _chart.GetBodyMultiplier(ElementType.Poison, BodyType.Organic));
        }

        [Fact]
        public void GetBodyMultiplier_LuzVsMortoVivo_Da1Ponto5()
        {
            // CANON §4: Luz +50% vs Sombra e mortos-vivos
            Assert.Equal(1.5f, _chart.GetBodyMultiplier(ElementType.Light, BodyType.Undead));
        }

        [Theory]
        [InlineData(ElementType.Light, ElementType.Shadow)]
        [InlineData(ElementType.Shadow, ElementType.Light)]
        [InlineData(ElementType.Lightning, ElementType.Metal)]
        public void GetMultiplier_RegrasPosMvpCanonicas_Da1Ponto5(ElementType atk, ElementType def)
        {
            Assert.Equal(1.5f, _chart.GetMultiplier(atk, def));
        }

        [Fact]
        public void GetMultiplier_NoneVsQualquer_EQualquerVsNone_ENeutro()
        {
            foreach (ElementType e in Enum.GetValues(typeof(ElementType)))
            {
                Assert.Equal(1.0f, _chart.GetMultiplier(ElementType.None, e));
                Assert.Equal(1.0f, _chart.GetMultiplier(e, ElementType.None));
            }
        }

        [Fact]
        public void GetMultiplier_MatrizCompleta_NenhumaCelulaSemValor()
        {
            foreach (ElementType atk in Enum.GetValues(typeof(ElementType)))
                foreach (ElementType def in Enum.GetValues(typeof(ElementType)))
                {
                    float m = _chart.GetMultiplier(atk, def);
                    Assert.True(m >= 0f && m <= 2f,
                        $"Célula {atk}→{def} fora da faixa canônica: {m}");
                }
        }

        [Fact]
        public void GetBodyMultiplier_MatrizCompleta_NenhumaCelulaSemValor()
        {
            foreach (ElementType atk in Enum.GetValues(typeof(ElementType)))
                foreach (BodyType def in Enum.GetValues(typeof(BodyType)))
                {
                    float m = _chart.GetBodyMultiplier(atk, def);
                    Assert.True(m >= 0f && m <= 2f,
                        $"Célula {atk}→{def} fora da faixa canônica: {m}");
                }
        }

        [Fact]
        public void Construtor_PorEntradas_MesmoFormatoDoSO()
        {
            // O ElementChartSO delega para esta classe usando o mesmo formato de Entry
            var entries = new[]
            {
                new ElementChart.Entry { attacker = ElementType.Alien, defender = ElementType.Metal, multiplier = 1.25f }
            };
            var chart = new ElementChart(entries, new ElementChart.BodyEntry[0]);
            Assert.Equal(1.25f, chart.GetMultiplier(ElementType.Alien, ElementType.Metal));
            // célula não declarada = neutra (1.0), nunca "sem valor"
            Assert.Equal(1.0f, chart.GetMultiplier(ElementType.Fire, ElementType.Ice));
            Assert.Equal(1.0f, chart.GetBodyMultiplier(ElementType.Poison, BodyType.Machine));
        }
    }
}
