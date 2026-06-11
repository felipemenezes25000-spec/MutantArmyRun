using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // Tabela canônica do doc 12 §4.3 + doc 04 §7.1 (piso de 1 unidade).
    public class GateMathTests
    {
        [Fact]
        public void Apply_AddFlat_Mais10_Sobre7_Da17()
        {
            Assert.Equal(17, GateMath.Apply(GateType.AddFlat, 10f, 7));
        }

        [Fact]
        public void Apply_AddFlat_Mais25_Sobre1_Da26()
        {
            Assert.Equal(26, GateMath.Apply(GateType.AddFlat, 25f, 1));
        }

        [Fact]
        public void Apply_Multiply_X2_Sobre21_Da42()
        {
            Assert.Equal(42, GateMath.Apply(GateType.Multiply, 2f, 21));
        }

        [Fact]
        public void Apply_Multiply_X3_Sobre5_Da15()
        {
            Assert.Equal(15, GateMath.Apply(GateType.Multiply, 3f, 5));
        }

        [Fact]
        public void Apply_Multiply_Metade_ImparArredondaParaCima()
        {
            // ÷2 sobre 7 → ⌈7/2⌉ = 4 (arredonda a favor do jogador, doc 04)
            Assert.Equal(4, GateMath.Apply(GateType.Multiply, 0.5f, 7));
        }

        [Fact]
        public void Apply_Multiply_Metade_Par_DivideExato()
        {
            Assert.Equal(4, GateMath.Apply(GateType.Multiply, 0.5f, 8));
        }

        [Fact]
        public void Apply_Multiply_Metade_Sobre1_NuncaZera()
        {
            Assert.Equal(1, GateMath.Apply(GateType.Multiply, 0.5f, 1));
        }

        [Fact]
        public void Apply_AddFlat_Negativo_NuncaFicaAbaixoDe1()
        {
            Assert.Equal(1, GateMath.Apply(GateType.AddFlat, -10f, 4));
        }

        [Theory]
        [InlineData(GateType.Element)]
        [InlineData(GateType.Mutation)]
        [InlineData(GateType.ClassConvert)]
        public void Apply_PortaisQueNaoMudamContagem_RetornamIdentidade(GateType type)
        {
            Assert.Equal(9, GateMath.Apply(type, 0f, 9));
        }

        [Fact]
        public void Apply_Multiply_X2_NuncaEAplicadoComoDelta_BugX2QueTriplica()
        {
            // Caso negativo obrigatório (doc 12 §4.3): aplicar x2 como DELTA sobre o
            // atual daria 21 + 2×21 = 63 ("x2 que triplica"). Semântica correta é
            // TOTAL-ALVO: o resultado é 42, nunca 63.
            int result = GateMath.Apply(GateType.Multiply, 2f, 21);
            Assert.NotEqual(63, result);
            Assert.Equal(42, result);
        }
    }
}
