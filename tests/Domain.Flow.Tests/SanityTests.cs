using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    public class SanityTests
    {
        [Fact]
        public void GateType_PrimeiroValor_EstaDefinido()
        {
            Assert.True(Enum.IsDefined(typeof(GateType), 0));
        }

        [Fact]
        public void GameState_TodosOsEstadosDoDoc12_EstaoDefinidos()
        {
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.Boot));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.MainMenu));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.BossScout));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.Running));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.BossFight));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.ReviveOffer));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.Victory));
            Assert.True(Enum.IsDefined(typeof(GameState), GameState.Defeat));
        }

        [Fact]
        public void GameState_OrdemCanonica_BootEhZero()
        {
            Assert.Equal(0, (int)GameState.Boot);
            Assert.Equal(8, Enum.GetValues(typeof(GameState)).Length);
        }
    }
}
