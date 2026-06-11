using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class SanityTests
    {
        [Fact]
        public void GateType_PrimeiroValor_EstaDefinido()
        {
            Assert.True(Enum.IsDefined(typeof(GateType), 0));
        }

        [Fact]
        public void CurrencyType_TresMoedasCanonicas()
        {
            Assert.True(Enum.IsDefined(typeof(CurrencyType), CurrencyType.Coin));
            Assert.True(Enum.IsDefined(typeof(CurrencyType), CurrencyType.Gem));
            Assert.True(Enum.IsDefined(typeof(CurrencyType), CurrencyType.Xp));
            Assert.Equal(3, Enum.GetValues(typeof(CurrencyType)).Length);
        }

        [Fact]
        public void ChestType_NoneEhZero_QuatroValores()
        {
            Assert.Equal(0, (int)ChestType.None);
            Assert.Equal(4, Enum.GetValues(typeof(ChestType)).Length);
        }

        [Fact]
        public void UpgradeTrack_OitoTrilhasDoCanon()
        {
            // CANON §9: 8 trilhas de upgrade de meta
            Assert.Equal(8, Enum.GetValues(typeof(UpgradeTrack)).Length);
            Assert.True(Enum.IsDefined(typeof(UpgradeTrack), UpgradeTrack.RewardMultiplier));
            Assert.True(Enum.IsDefined(typeof(UpgradeTrack), UpgradeTrack.StartArmy));
        }
    }
}
