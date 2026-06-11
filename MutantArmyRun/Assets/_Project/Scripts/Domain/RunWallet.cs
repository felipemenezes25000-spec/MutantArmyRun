namespace MutantArmy.Domain
{
    /// <summary>
    /// Carteira TEMPORÁRIA da corrida (doc 12 §4.6): ganhos da fase acumulam aqui e só
    /// viram moeda persistente no commit do fim de fase. Vitória comita coins×multiplier
    /// ("DOBRAR x2" do rewarded = multiplier 2); derrota descarta as moedas, mas a XP é
    /// SEMPRE comitada integral — derrota nunca zera aprendizado. Após o commit, zera.
    /// </summary>
    public sealed class RunWallet
    {
        public int Coins { get; private set; }
        public int Xp { get; private set; }

        public void EarnCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
        }

        public void EarnXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
        }

        /// <summary>
        /// Fecha a corrida e devolve o que deve ser creditado na carteira persistente.
        /// O multiplicador do rewarded afeta só as moedas, nunca a XP.
        /// </summary>
        public (long coins, int xp) BuildCommit(bool won, int multiplier = 1)
        {
            // long no retorno: coins × multiplier pode passar de int.MaxValue.
            long coins = won ? (long)Coins * multiplier : 0L;
            int xp = Xp;
            Coins = 0;
            Xp = 0;
            return (coins, xp);
        }
    }
}
