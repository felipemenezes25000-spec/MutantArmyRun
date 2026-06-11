using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    public class SanityTests
    {
        [Fact]
        public void GateType_PrimeiroValor_EstaDefinido()
        {
            Assert.True(Enum.IsDefined(typeof(GateType), 0));
        }

        [Theory]
        [InlineData(typeof(ElementType), 9)]
        [InlineData(typeof(Rarity), 4)]
        [InlineData(typeof(GateType), 6)]
        [InlineData(typeof(BodyType), 3)]
        [InlineData(typeof(UpgradeTrack), 8)]
        [InlineData(typeof(GameState), 8)]
        [InlineData(typeof(CurrencyType), 3)]
        [InlineData(typeof(ChestType), 4)]
        public void Enums_TemQuantidadeCanonicaDeValores(Type enumType, int expectedCount)
        {
            Assert.Equal(expectedCount, Enum.GetValues(enumType).Length);
        }

        [Fact]
        public void ElementType_CicloPrincipal_ValoresCanonicos()
        {
            Assert.Equal(0, (int)ElementType.None);
            Assert.Equal(1, (int)ElementType.Fire);
            Assert.Equal(2, (int)ElementType.Ice);
            Assert.Equal(3, (int)ElementType.Lightning);
            Assert.Equal(4, (int)ElementType.Poison);
        }

        [Fact]
        public void GateType_OrdemCanonica_DoDoc12()
        {
            Assert.Equal(0, (int)GateType.AddFlat);
            Assert.Equal(1, (int)GateType.Multiply);
            Assert.Equal(2, (int)GateType.ClassConvert);
            Assert.Equal(3, (int)GateType.Element);
            Assert.Equal(4, (int)GateType.Mutation);
            Assert.Equal(5, (int)GateType.Risk);
        }
    }
}
