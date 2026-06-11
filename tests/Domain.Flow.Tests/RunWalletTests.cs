using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    public class RunWalletTests
    {
        [Fact]
        public void RecemCriada_TudoZerado()
        {
            var w = new RunWallet();

            Assert.Equal(0, w.Coins);
            Assert.Equal(0, w.Xp);
        }

        [Fact]
        public void EarnCoins_EEarnXp_Acumulam()
        {
            var w = new RunWallet();

            w.EarnCoins(40);
            w.EarnCoins(60);
            w.EarnXp(10);
            w.EarnXp(15);

            Assert.Equal(100, w.Coins);
            Assert.Equal(25, w.Xp);
        }

        [Fact]
        public void Earn_ValoresNaoPositivos_SaoIgnorados()
        {
            // Mesma guarda do EconomySystem.Earn (doc 12 §4.6): valor <= 0 não entra.
            var w = new RunWallet();
            w.EarnCoins(50);
            w.EarnXp(5);

            w.EarnCoins(0);
            w.EarnCoins(-30);
            w.EarnXp(0);
            w.EarnXp(-2);

            Assert.Equal(50, w.Coins);
            Assert.Equal(5, w.Xp);
        }

        [Fact]
        public void BuildCommit_Vitoria_ComitaCoinsEXpIntegrais()
        {
            var w = new RunWallet();
            w.EarnCoins(100);
            w.EarnXp(30);

            var (coins, xp) = w.BuildCommit(won: true);

            Assert.Equal(100L, coins);
            Assert.Equal(30, xp);
        }

        [Fact]
        public void BuildCommit_VitoriaComMultiplicador2_DobraSomenteCoins()
        {
            // "DOBRAR x2" do rewarded (CANON §11 + doc 12 §4.6): multiplica as moedas da
            // corrida; XP nunca é multiplicada.
            var w = new RunWallet();
            w.EarnCoins(100);
            w.EarnXp(50);

            var (coins, xp) = w.BuildCommit(won: true, multiplier: 2);

            Assert.Equal(200L, coins);
            Assert.Equal(50, xp);
        }

        [Fact]
        public void BuildCommit_Derrota_DescartaCoins_MasXpSempreIntegral()
        {
            // Doc 12 §4.6: derrota descarta as moedas do wallet, mas XP é SEMPRE comitada —
            // derrota nunca zera aprendizado.
            var w = new RunWallet();
            w.EarnCoins(80);
            w.EarnXp(20);

            var (coins, xp) = w.BuildCommit(won: false);

            Assert.Equal(0L, coins);
            Assert.Equal(20, xp);
        }

        [Fact]
        public void BuildCommit_DerrotaComMultiplicador_NaoRessuscitaCoins()
        {
            var w = new RunWallet();
            w.EarnCoins(80);
            w.EarnXp(20);

            var (coins, xp) = w.BuildCommit(won: false, multiplier: 2);

            Assert.Equal(0L, coins);
            Assert.Equal(20, xp);
        }

        [Fact]
        public void BuildCommit_ZeraACarteira_SegundoCommitNaoDuplica()
        {
            var w = new RunWallet();
            w.EarnCoins(100);
            w.EarnXp(30);
            w.BuildCommit(won: true, multiplier: 2);

            Assert.Equal(0, w.Coins);
            Assert.Equal(0, w.Xp);

            var (coins, xp) = w.BuildCommit(won: true, multiplier: 2);
            Assert.Equal(0L, coins);
            Assert.Equal(0, xp);
        }

        [Fact]
        public void BuildCommit_CoinsGrandesComMultiplicador_NaoEstouraInt()
        {
            // Retorno em long: int.MaxValue × 2 precisa sobreviver ao commit.
            var w = new RunWallet();
            w.EarnCoins(int.MaxValue);

            var (coins, _) = w.BuildCommit(won: true, multiplier: 2);

            Assert.Equal(2L * int.MaxValue, coins);
        }

        [Fact]
        public void BuildCommit_AposCommit_CarteiraAceitaNovaCorrida()
        {
            var w = new RunWallet();
            w.EarnCoins(10);
            w.BuildCommit(won: true);

            w.EarnCoins(7);
            w.EarnXp(3);

            Assert.Equal(7, w.Coins);
            Assert.Equal(3, w.Xp);
        }
    }
}
