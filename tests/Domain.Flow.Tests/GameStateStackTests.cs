using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    public class GameStateStackTests
    {
        [Fact]
        public void EstadoInicial_EhBoot_ComDepth1()
        {
            var stack = new GameStateStack();

            Assert.Equal(GameState.Boot, stack.Current);
            Assert.Equal(1, stack.Depth);
        }

        [Fact]
        public void CaminhoFeliz_BootAteVictoria_EProximaFaseSemVoltarAoMenu()
        {
            // Doc 12 §4.1: Victory → BossScout permite encadear a próxima fase sem loading/menu.
            var stack = new GameStateStack();

            Assert.True(stack.ChangeState(GameState.MainMenu));
            Assert.Equal(GameState.MainMenu, stack.Current);

            Assert.True(stack.ChangeState(GameState.BossScout));
            Assert.Equal(GameState.BossScout, stack.Current);

            Assert.True(stack.ChangeState(GameState.Running));
            Assert.Equal(GameState.Running, stack.Current);

            Assert.True(stack.ChangeState(GameState.BossFight));
            Assert.Equal(GameState.BossFight, stack.Current);

            Assert.True(stack.ChangeState(GameState.Victory));
            Assert.Equal(GameState.Victory, stack.Current);

            Assert.True(stack.ChangeState(GameState.BossScout));
            Assert.Equal(GameState.BossScout, stack.Current);

            // ChangeState troca o topo — nunca acumula profundidade.
            Assert.Equal(1, stack.Depth);
        }

        [Fact]
        public void TransicaoIlegal_RunningParaVictory_RetornaFalseEMantemEstado()
        {
            var stack = new GameStateStack();
            stack.ChangeState(GameState.MainMenu);
            stack.ChangeState(GameState.BossScout);
            stack.ChangeState(GameState.Running);

            // Vitória só existe saindo do BossFight (toda fase termina em boss, CANON §6).
            Assert.False(stack.ChangeState(GameState.Victory));
            Assert.Equal(GameState.Running, stack.Current);
            Assert.Equal(1, stack.Depth);
        }

        [Theory]
        [InlineData(GameState.Boot, GameState.Running)]
        [InlineData(GameState.Boot, GameState.BossFight)]
        [InlineData(GameState.Boot, GameState.Boot)]
        [InlineData(GameState.Boot, GameState.Victory)]
        public void TransicaoIlegal_APartirDeBoot_RetornaFalse(GameState origem, GameState destino)
        {
            var stack = new GameStateStack();
            Assert.Equal(origem, stack.Current);

            Assert.False(stack.ChangeState(destino));
            Assert.Equal(origem, stack.Current);
        }

        [Fact]
        public void TabelaAllowed_RunningPodeIrParaDefeat_SemPassarPeloBoss()
        {
            // Doc 12 §4.1: Running → { BossFight, Defeat } — morrer na pista é legal.
            var stack = new GameStateStack();
            stack.ChangeState(GameState.MainMenu);
            stack.ChangeState(GameState.BossScout);
            stack.ChangeState(GameState.Running);

            Assert.True(stack.ChangeState(GameState.Defeat));
            Assert.Equal(GameState.Defeat, stack.Current);
        }

        [Fact]
        public void TabelaAllowed_DefeatPermiteRetryDireto_EVoltaAoMenu()
        {
            // Doc 12 §4.1: Defeat → { MainMenu, BossScout }.
            var retry = new GameStateStack();
            retry.ChangeState(GameState.MainMenu);
            retry.ChangeState(GameState.BossScout);
            retry.ChangeState(GameState.Running);
            retry.ChangeState(GameState.Defeat);
            Assert.True(retry.ChangeState(GameState.BossScout));

            var menu = new GameStateStack();
            menu.ChangeState(GameState.MainMenu);
            menu.ChangeState(GameState.BossScout);
            menu.ChangeState(GameState.Running);
            menu.ChangeState(GameState.Defeat);
            Assert.True(menu.ChangeState(GameState.MainMenu));
        }

        [Fact]
        public void Revive_PushReviveOfferSobreBossFight_PopVoltaExatoABossFight()
        {
            // Cenário canônico do revive (doc 12 §4.1 + CANON §11): a oferta entra POR CIMA
            // da luta — exército/RunWallet/progresso ficam intactos embaixo.
            var stack = new GameStateStack();
            stack.ChangeState(GameState.MainMenu);
            stack.ChangeState(GameState.BossScout);
            stack.ChangeState(GameState.Running);
            stack.ChangeState(GameState.BossFight);

            stack.Push(GameState.ReviveOffer);
            Assert.Equal(GameState.ReviveOffer, stack.Current);
            Assert.Equal(2, stack.Depth);

            GameState removido = stack.Pop();
            Assert.Equal(GameState.ReviveOffer, removido);
            Assert.Equal(GameState.BossFight, stack.Current);
            Assert.Equal(1, stack.Depth);
        }

        [Fact]
        public void Revive_RecusadoAposPop_BossFightParaDefeat_EhLegal()
        {
            // Fluxo do ResolveRevive(false): Pop() e depois ChangeState(Defeat) a partir do BossFight.
            var stack = new GameStateStack();
            stack.ChangeState(GameState.MainMenu);
            stack.ChangeState(GameState.BossScout);
            stack.ChangeState(GameState.Running);
            stack.ChangeState(GameState.BossFight);
            stack.Push(GameState.ReviveOffer);
            stack.Pop();

            Assert.True(stack.ChangeState(GameState.Defeat));
            Assert.Equal(GameState.Defeat, stack.Current);
        }

        [Fact]
        public void ChangeState_ComOverlaySemEntradaNaTabela_RetornaFalse()
        {
            // ReviveOffer não tem linha na tabela Allowed: enquanto o overlay está no topo,
            // a única saída é Pop() — transição lateral é ilegal e não corrompe a pilha.
            var stack = new GameStateStack();
            stack.ChangeState(GameState.MainMenu);
            stack.ChangeState(GameState.BossScout);
            stack.ChangeState(GameState.Running);
            stack.ChangeState(GameState.BossFight);
            stack.Push(GameState.ReviveOffer);

            Assert.False(stack.ChangeState(GameState.Defeat));
            Assert.Equal(GameState.ReviveOffer, stack.Current);
            Assert.Equal(2, stack.Depth);
        }

        [Fact]
        public void Pop_EmPilhaDe1_LancaInvalidOperationException()
        {
            var stack = new GameStateStack();

            Assert.Throws<InvalidOperationException>(() => { stack.Pop(); });
            Assert.Equal(GameState.Boot, stack.Current);
            Assert.Equal(1, stack.Depth);
        }
    }
}
