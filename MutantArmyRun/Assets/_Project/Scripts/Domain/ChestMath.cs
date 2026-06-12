using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Tabelas de drop de baú e regras de pity (doc 07 §4). Matemática PURA: o RewardSystem
    /// (Meta) injeta o RNG e resolve QUAL tropa de cada raridade entra; aqui só decidimos a
    /// RARIDADE de cada "pacote", a quantidade de pacotes/moedas/gemas do tipo, e o pity de
    /// Lendário (contador global de pacotes, promove o próximo Raro+ a Lendário após N).
    /// Nenhuma dependência de UnityEngine — testável por seed no Domain.
    /// </summary>
    public static class ChestMath
    {
        /// <summary>Quantidade fixa de fragmentos por pacote, por raridade (doc 07 §4): C 10 · R 5 · E 2 · L 1.</summary>
        public static int ShardsForRarity(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return 5;
                case Rarity.Epic: return 2;
                case Rarity.Legendary: return 1;
                default: return 10;   // Common
            }
        }

        /// <summary>
        /// Parâmetros completos de um tipo de baú (doc 07 §4): nº de pacotes, faixa de moedas,
        /// gemas e a tabela de chance por raridade (soma = 1.0). Struct imutável de dados.
        /// </summary>
        public readonly struct ChestTable
        {
            public readonly int Packets;
            public readonly int CoinsMin;
            public readonly int CoinsMax;
            public readonly int Gems;
            public readonly float CommonChance;
            public readonly float RareChance;
            public readonly float EpicChance;
            public readonly float LegendaryChance;
            /// <summary>Raridade mínima GARANTIDA em ≥1 pacote (doc 07 §4); só vale se HasGuarantee.</summary>
            public readonly Rarity GuaranteedFloor;
            /// <summary>Há garantia de piso de raridade? (Comum/De Mundo não têm — GuaranteedFloor é ignorado).</summary>
            public readonly bool HasGuarantee;

            public ChestTable(int packets, int coinsMin, int coinsMax, int gems,
                              float commonChance, float rareChance, float epicChance, float legendaryChance,
                              Rarity guaranteedFloor, bool hasGuarantee)
            {
                Packets = packets;
                CoinsMin = coinsMin;
                CoinsMax = coinsMax;
                Gems = gems;
                CommonChance = commonChance;
                RareChance = rareChance;
                EpicChance = epicChance;
                LegendaryChance = legendaryChance;
                GuaranteedFloor = guaranteedFloor;
                HasGuarantee = hasGuarantee;
            }
        }

        /// <summary>
        /// Tabela canônica por tipo (doc 07 §4). ChestType.None é tratado como Common pelo chamador;
        /// World mapeia para a linha "De Mundo". Pity e moedas × Mb são aplicados FORA, na Meta.
        /// </summary>
        public static ChestTable TableFor(ChestKind kind)
        {
            switch (kind)
            {
                case ChestKind.Rare:
                    return new ChestTable(8, 250, 400, 0, 0.65f, 0.28f, 0.06f, 0.01f, Rarity.Rare, true);
                case ChestKind.Epic:
                    return new ChestTable(15, 600, 900, 20, 0.45f, 0.38f, 0.145f, 0.025f, Rarity.Epic, true);
                case ChestKind.Legendary:
                    return new ChestTable(25, 1500, 2500, 80, 0.30f, 0.40f, 0.22f, 0.08f, Rarity.Legendary, true);
                case ChestKind.World:
                    return new ChestTable(10, 0, 0, 15, 0.40f, 0.35f, 0.20f, 0.05f, Rarity.Common, false);
                default: // Common
                    return new ChestTable(3, 60, 100, 0, 0.85f, 0.13f, 0.018f, 0.002f, Rarity.Common, false);
            }
        }

        /// <summary>
        /// Rola a raridade de UM pacote a partir de um sorteio uniforme [0,1) (doc 07 §4).
        /// Acumula Common→Rare→Epic→Legendary na ordem da tabela.
        /// </summary>
        public static Rarity RollPacketRarity(in ChestTable t, double roll)
        {
            if (roll < 0d) roll = 0d;
            double acc = t.CommonChance;
            if (roll < acc) return Rarity.Common;
            acc += t.RareChance;
            if (roll < acc) return Rarity.Rare;
            acc += t.EpicChance;
            if (roll < acc) return Rarity.Epic;
            return Rarity.Legendary;
        }

        /// <summary>
        /// Pity de Lendário (doc 07 §4): se <paramref name="packetsSinceLegendary"/> já atingiu o
        /// teto, o pacote Raro+ é promovido a Lendário. Common nunca é promovido (a garantia conta
        /// pacotes "valiosos"); a promoção zera o contador no chamador. <paramref name="pityThreshold"/>
        /// recalibrável por RC (default 50).
        /// </summary>
        public static Rarity ApplyPity(Rarity rolled, int packetsSinceLegendary, int pityThreshold)
        {
            if (pityThreshold <= 0) return rolled;
            if (rolled == Rarity.Legendary) return rolled;
            if (packetsSinceLegendary >= pityThreshold && rolled != Rarity.Common)
                return Rarity.Legendary;
            return rolled;
        }

        /// <summary>Moedas do baú dado o sorteio uniforme [0,1) entre min e max da tabela (doc 07 §4).</summary>
        public static long RollCoins(in ChestTable t, double roll)
        {
            if (t.CoinsMax <= t.CoinsMin) return t.CoinsMin;
            if (roll < 0d) roll = 0d;
            if (roll >= 1d) roll = 0.999999d;
            return t.CoinsMin + (long)(roll * (t.CoinsMax - t.CoinsMin + 1));
        }
    }

    /// <summary>
    /// Raridade/categoria de baú do Domain (doc 07 §4). Espelha o contrato RewardSystem.ChestType,
    /// mas vive no Domain para a matemática de drop ficar pura (o enum ChestType do Core/Domain
    /// hoje só tem None/Common/Rare/Epic; este cobre também Legendary e World sem mexer no save).
    /// </summary>
    public enum ChestKind { Common, Rare, Epic, Legendary, World }
}
