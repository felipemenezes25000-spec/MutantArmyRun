using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class ChestMathTests
    {
        // ---------- ShardsForRarity — doc 07 §4: C 10 · R 5 · E 2 · L 1 ----------

        [Theory]
        [InlineData(Rarity.Common, 10)]
        [InlineData(Rarity.Rare, 5)]
        [InlineData(Rarity.Epic, 2)]
        [InlineData(Rarity.Legendary, 1)]
        public void ShardsForRarity_QuantidadeCanonicaPorRaridade(Rarity rarity, int esperado)
        {
            Assert.Equal(esperado, ChestMath.ShardsForRarity(rarity));
        }

        // ---------- TableFor — tabelas do §4 (soma das chances = 1.0) ----------

        [Theory]
        [InlineData(ChestKind.Common, 3)]
        [InlineData(ChestKind.Rare, 8)]
        [InlineData(ChestKind.Epic, 15)]
        [InlineData(ChestKind.Legendary, 25)]
        [InlineData(ChestKind.World, 10)]
        public void TableFor_NumeroDePacotesCanonico(ChestKind kind, int esperado)
        {
            Assert.Equal(esperado, ChestMath.TableFor(kind).Packets);
        }

        [Theory]
        [InlineData(ChestKind.Common)]
        [InlineData(ChestKind.Rare)]
        [InlineData(ChestKind.Epic)]
        [InlineData(ChestKind.Legendary)]
        [InlineData(ChestKind.World)]
        public void TableFor_ChancesSomam100PorCento(ChestKind kind)
        {
            ChestMath.ChestTable t = ChestMath.TableFor(kind);
            float sum = t.CommonChance + t.RareChance + t.EpicChance + t.LegendaryChance;
            Assert.Equal(1.0f, sum, 4);
        }

        [Fact]
        public void TableFor_TiersAltosDaoGemas()
        {
            Assert.Equal(0, ChestMath.TableFor(ChestKind.Common).Gems);
            Assert.Equal(0, ChestMath.TableFor(ChestKind.Rare).Gems);
            Assert.Equal(20, ChestMath.TableFor(ChestKind.Epic).Gems);
            Assert.Equal(80, ChestMath.TableFor(ChestKind.Legendary).Gems);
            Assert.Equal(15, ChestMath.TableFor(ChestKind.World).Gems);
        }

        // ---------- RollPacketRarity — acumulação determinística ----------

        [Fact]
        public void RollPacketRarity_Comum_RolagensBaixasCaemEmComum()
        {
            ChestMath.ChestTable t = ChestMath.TableFor(ChestKind.Common);
            Assert.Equal(Rarity.Common, ChestMath.RollPacketRarity(t, 0.00));
            Assert.Equal(Rarity.Common, ChestMath.RollPacketRarity(t, 0.84));   // < 0.85
            Assert.Equal(Rarity.Rare, ChestMath.RollPacketRarity(t, 0.90));     // 0.85..0.98
            Assert.Equal(Rarity.Legendary, ChestMath.RollPacketRarity(t, 0.999)); // cauda
        }

        [Fact]
        public void RollPacketRarity_Legendario_RolagemMaximaDaLendario()
        {
            ChestMath.ChestTable t = ChestMath.TableFor(ChestKind.Legendary);
            Assert.Equal(Rarity.Common, ChestMath.RollPacketRarity(t, 0.0));
            // 0.30 common + 0.40 rare + 0.22 epic = 0.92 → ≥0.92 é Lendário
            Assert.Equal(Rarity.Legendary, ChestMath.RollPacketRarity(t, 0.95));
        }

        // ---------- ApplyPity — doc 07 §4: após N pacotes, Raro+ vira Lendário ----------

        [Fact]
        public void ApplyPity_AntesDoTeto_NaoPromove()
        {
            Assert.Equal(Rarity.Rare, ChestMath.ApplyPity(Rarity.Rare, 10, 50));
        }

        [Fact]
        public void ApplyPity_NoTeto_PromoveRaroParaLendario()
        {
            Assert.Equal(Rarity.Legendary, ChestMath.ApplyPity(Rarity.Rare, 50, 50));
            Assert.Equal(Rarity.Legendary, ChestMath.ApplyPity(Rarity.Epic, 51, 50));
        }

        [Fact]
        public void ApplyPity_Comum_NuncaPromovido()
        {
            // a garantia conta pacotes "valiosos" — Common não é promovido
            Assert.Equal(Rarity.Common, ChestMath.ApplyPity(Rarity.Common, 99, 50));
        }

        [Fact]
        public void ApplyPity_JaLendario_PermaneceLendario()
        {
            Assert.Equal(Rarity.Legendary, ChestMath.ApplyPity(Rarity.Legendary, 0, 50));
        }

        [Fact]
        public void ApplyPity_TetoDesligado_NuncaPromove()
        {
            Assert.Equal(Rarity.Rare, ChestMath.ApplyPity(Rarity.Rare, 1000, 0));
        }

        // ---------- RollCoins — faixa min..max ----------

        [Fact]
        public void RollCoins_DentroDaFaixaDaTabela()
        {
            ChestMath.ChestTable t = ChestMath.TableFor(ChestKind.Rare);   // 250..400
            Assert.Equal(250L, ChestMath.RollCoins(t, 0.0));
            Assert.True(ChestMath.RollCoins(t, 0.5) >= 250 && ChestMath.RollCoins(t, 0.5) <= 400);
            Assert.Equal(400L, ChestMath.RollCoins(t, 0.999999));
        }

        [Fact]
        public void RollCoins_SemFaixa_RetornaMin()
        {
            ChestMath.ChestTable t = ChestMath.TableFor(ChestKind.World);   // 0..0
            Assert.Equal(0L, ChestMath.RollCoins(t, 0.7));
        }
    }
}
