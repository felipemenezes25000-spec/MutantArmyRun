using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Tipo de recompensa de UM nó do Passe de Temporada — leve o bastante para a UI pintar o ícone
    /// e o número sem conhecer a Meta. SkinShard/UnitShard concedem fragmentos genéricos do pool;
    /// a tela mostra "fragmentos" e a concessão real fica na Meta (RewardSystem).
    /// </summary>
    public enum SeasonRewardKind { None, Coins, Gems, Shards, Skin }

    /// <summary>
    /// Uma recompensa de um nó do passe (trilha grátis OU premium de um nível). Struct de VALOR puro:
    /// kind + amount. A tela formata "+250 moedas" / "+5 gemas" / "+10 fragmentos" / "SKIN" a partir disto.
    /// </summary>
    public readonly struct SeasonReward
    {
        public readonly SeasonRewardKind kind;
        public readonly int amount;

        public SeasonReward(SeasonRewardKind kind, int amount)
        {
            this.kind = kind;
            this.amount = amount;
        }

        public bool HasReward => kind != SeasonRewardKind.None && (amount > 0 || kind == SeasonRewardKind.Skin);

        public static readonly SeasonReward None = new SeasonReward(SeasonRewardKind.None, 0);
    }

    /// <summary>
    /// Matemática PURA do Passe de Temporada (sem UnityEngine): trilha de ~30 níveis, XP por nível,
    /// progresso atual a partir de uma quantidade total de XP de passe, e a tabela determinística de
    /// recompensas (trilha GRÁTIS vs PREMIUM) por nível. A XP de passe é derivada na camada Meta a
    /// partir do que o save JÁ tem (fases vencidas, missões, nível do jogador) — aqui só convertemos
    /// XP→nível e nível→recompensa, igual ao espírito das demais curvas do EconomyMath/MissionMath.
    ///
    /// As trilhas são intencionalmente generosas no premium e modestas no grátis (modelo casual premium):
    /// o grátis sempre dá algo a cada nível; o premium dá mais e adiciona gemas/skin nos marcos.
    /// </summary>
    public static class SeasonPassMath
    {
        /// <summary>Número de níveis da trilha do passe (marcos da temporada).</summary>
        public const int TierCount = 30;

        /// <summary>XP de passe necessária para AVANÇAR um nível (constante por simplicidade casual).</summary>
        public const int XpPerTier = 100;

        /// <summary>Preço do passe em dólares (CANON §11 / doc 11 §4.6: season_pass_699 = US$ 6,99).</summary>
        public const float PriceUsd = 6.99f;

        /// <summary>
        /// Nível ATUAL do passe (1..TierCount) para uma quantidade de XP total. XP 0 = nível 1.
        /// Saturado no teto: passar de TierCount × XpPerTier mantém o último nível (a barra fica cheia).
        /// </summary>
        public static int TierForXp(long totalXp)
        {
            if (totalXp <= 0) return 1;
            long tier = totalXp / XpPerTier + 1;
            if (tier > TierCount) return TierCount;
            return (int)tier;
        }

        /// <summary>XP DENTRO do nível atual (0..XpPerTier-1); no teto, devolve XpPerTier (barra cheia).</summary>
        public static int XpIntoTier(long totalXp)
        {
            if (totalXp <= 0) return 0;
            int tier = TierForXp(totalXp);
            if (tier >= TierCount) return XpPerTier;
            return (int)(totalXp - (long)(tier - 1) * XpPerTier);
        }

        /// <summary>Fração de progresso (0..1) dentro do nível atual — barrinha de XP do passe.</summary>
        public static float TierProgress01(long totalXp)
        {
            if (XpPerTier <= 0) return 1f;
            return (float)XpIntoTier(totalXp) / XpPerTier;
        }

        /// <summary>XP total acumulada necessária para ATINGIR um nível (nível 1 = 0).</summary>
        public static long XpToReachTier(int tier)
        {
            if (tier <= 1) return 0;
            if (tier > TierCount) tier = TierCount;
            return (long)(tier - 1) * XpPerTier;
        }

        /// <summary>Um nível está liberado (atingido) com a XP total dada?</summary>
        public static bool IsTierReached(int tier, long totalXp)
        {
            if (tier < 1) return true;
            return totalXp >= XpToReachTier(tier);
        }

        // ---------------------------------------------------------------- Tabela de recompensas

        /// <summary>
        /// Recompensa da trilha GRÁTIS no nível dado (1..TierCount). Sempre concede algo (modelo
        /// "valor a cada nível"): moedas escalando, com gemas/fragmentos em marcos de 5 em 5.
        /// </summary>
        public static SeasonReward FreeReward(int tier)
        {
            if (tier < 1 || tier > TierCount) return SeasonReward.None;

            // Marcos de 10 dão gemas; marcos de 5 (não-10) dão fragmentos; demais dão moedas.
            if (tier % 10 == 0)
                return new SeasonReward(SeasonRewardKind.Gems, 10 + tier / 10 * 5);   // 15, 20, 25
            if (tier % 5 == 0)
                return new SeasonReward(SeasonRewardKind.Shards, 5);

            return new SeasonReward(SeasonRewardKind.Coins, FreeCoins(tier));
        }

        /// <summary>
        /// Recompensa da trilha PREMIUM no nível dado (1..TierCount). Sempre mais rica que a grátis:
        /// moedas maiores, gemas mais frequentes, fragmentos em dobro e uma SKIN no último nível.
        /// </summary>
        public static SeasonReward PremiumReward(int tier)
        {
            if (tier < 1 || tier > TierCount) return SeasonReward.None;

            if (tier == TierCount)
                return new SeasonReward(SeasonRewardKind.Skin, 1);   // recompensa de capa: skin exclusiva
            if (tier % 10 == 0)
                return new SeasonReward(SeasonRewardKind.Gems, 30 + tier / 10 * 10);  // 40, 50
            if (tier % 5 == 0)
                return new SeasonReward(SeasonRewardKind.Gems, 15);
            if (tier % 3 == 0)
                return new SeasonReward(SeasonRewardKind.Shards, 10);

            return new SeasonReward(SeasonRewardKind.Coins, PremiumCoins(tier));
        }

        /// <summary>Moedas da trilha grátis num nível "comum" — cresce suave com o nível.</summary>
        public static int FreeCoins(int tier)
        {
            if (tier < 1) tier = 1;
            return 100 + (tier - 1) * 50;          // 100, 150, 200, ...
        }

        /// <summary>Moedas da trilha premium num nível "comum" — ~2,5× a grátis (valor do passe).</summary>
        public static int PremiumCoins(int tier)
        {
            if (tier < 1) tier = 1;
            return 250 + (tier - 1) * 120;         // 250, 370, 490, ...
        }
    }
}
