using System;
using System.Globalization;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Rótulos e cores das telas de meta. PT-BR base (sem tabela de loc no MVP, doc 09 §6):
    /// nomes derivados das chaves de loc dos SOs (humanização), nomes amigáveis das 8 trilhas,
    /// cores canônicas de raridade (CANON §8 / doc 09 P7 — moldura com COR e silhueta, nunca
    /// só cor). Centraliza para as 5 telas e a factory falarem a mesma língua.
    /// </summary>
    public static class MetaText
    {
        // ---------------------------------------------------------------- Raridade (CANON §8)

        public static Color RarityFrame(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new Color(0.62f, 0.66f, 0.72f);   // cinza/azul claro
                case Rarity.Rare: return new Color(0.28f, 0.58f, 0.98f);     // azul
                case Rarity.Epic: return new Color(0.66f, 0.36f, 0.92f);     // roxo
                case Rarity.Legendary: return new Color(1.00f, 0.80f, 0.28f);// dourado
                default: return new Color(0.62f, 0.66f, 0.72f);
            }
        }

        /// <summary>Tinta de fundo da carta — versão escura/translúcida da cor de raridade.</summary>
        public static Color RarityCardBg(Rarity r)
        {
            Color f = RarityFrame(r);
            return new Color(f.r * 0.22f, f.g * 0.22f, f.b * 0.26f, 0.96f);
        }

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return "COMUM";
                case Rarity.Rare: return "RARA";
                case Rarity.Epic: return "ÉPICA";
                case Rarity.Legendary: return "LENDÁRIA";
                default: return "COMUM";
            }
        }

        // ---------------------------------------------------------------- Trilhas de upgrade (CANON §9)

        public static string TrackName(UpgradeTrack t)
        {
            switch (t)
            {
                case UpgradeTrack.StartDamage: return "DANO INICIAL";
                case UpgradeTrack.StartHealth: return "VIDA INICIAL";
                case UpgradeTrack.Speed: return "VELOCIDADE";
                case UpgradeTrack.RewardMultiplier: return "RECOMPENSA";
                case UpgradeTrack.StartArmy: return "EXÉRCITO INICIAL";
                case UpgradeTrack.CritChance: return "CHANCE CRÍTICA";
                case UpgradeTrack.BossDamage: return "DANO VS BOSS";
                case UpgradeTrack.ObstacleResist: return "RESIST. OBSTÁCULOS";
                default: return t.ToString();
            }
        }

        public static string TrackDescription(UpgradeTrack t)
        {
            switch (t)
            {
                case UpgradeTrack.StartDamage: return "Suas tropas começam a fase com mais dano.";
                case UpgradeTrack.StartHealth: return "Suas tropas começam a fase com mais vida.";
                case UpgradeTrack.Speed: return "Corrida e ataque mais rápidos.";
                case UpgradeTrack.RewardMultiplier: return "Mais moedas em toda fonte de fase.";
                case UpgradeTrack.StartArmy: return "Comece a fase com mais unidades.";
                case UpgradeTrack.CritChance: return "Maior chance de golpe crítico.";
                case UpgradeTrack.BossDamage: return "Mais dano na arena do boss.";
                case UpgradeTrack.ObstacleResist: return "Perde menos unidades em obstáculos.";
                default: return string.Empty;
            }
        }

        /// <summary>Efeito acumulado formatado: "+25%" para percentuais; "+3 un." para Exército.</summary>
        public static string TrackEffectLabel(UpgradeTrack t, float effect)
        {
            if (MetaBridge.IsUnitTrack(t))
                return "+" + Mathf.RoundToInt(effect) + " un.";
            return "+" + Mathf.RoundToInt(effect * 100f) + "%";
        }

        // ---------------------------------------------------------------- Humanização de chaves

        /// <summary>
        /// "world_01_campo_inicial" → "Campo Inicial"; "soldier_name" → "Soldier".
        /// Tira prefixos de índice ("world_NN_"), sufixo "_name", troca "_" por espaço e
        /// capitaliza cada palavra. Rede de segurança quando o SO não tem nome amigável.
        /// </summary>
        public static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string s = key;
            if (s.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - "_name".Length);

            // Remove um prefixo "algo_NN_" (ex.: "world_01_") — mantém só o nome legível.
            int firstUnderscore = s.IndexOf('_');
            if (firstUnderscore > 0 && firstUnderscore + 1 < s.Length)
            {
                int second = s.IndexOf('_', firstUnderscore + 1);
                if (second > firstUnderscore)
                {
                    string middle = s.Substring(firstUnderscore + 1, second - firstUnderscore - 1);
                    if (IsAllDigits(middle)) s = s.Substring(second + 1);
                }
            }

            string[] parts = s.Split('_');
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpper(parts[i][0], CultureInfo.InvariantCulture));
                if (parts[i].Length > 1) sb.Append(parts[i].Substring(1));
            }
            return sb.ToString();
        }

        private static bool IsAllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (!char.IsDigit(s[i])) return false;
            return s.Length > 0;
        }

        /// <summary>
        /// Nome amigável de uma tropa. 1) displayName PT-BR gravado pelo MvpContentFactory
        /// (caminho normal); 2) fallback humaniza a chave de loc/id em assets antigos —
        /// NUNCA exibe a chave crua tipo "UNIT ARCHER".
        /// </summary>
        public static string UnitName(UnitConfigSO unit)
        {
            if (unit == null) return "TROPA";
            if (!string.IsNullOrEmpty(unit.displayName))
                return unit.displayName.ToUpperInvariant();
            string raw = !string.IsNullOrEmpty(unit.displayNameKey) ? unit.displayNameKey : unit.unitId;
            string human = Humanize(raw);
            return string.IsNullOrEmpty(human) ? "TROPA" : human.ToUpperInvariant();
        }

        /// <summary>
        /// Nome amigável de uma mutação. 1) displayName PT-BR gravado pelo MvpContentFactory;
        /// 2) fallback humaniza a chave de loc/id — NUNCA exibe a chave crua tipo "MUT_ARMOR_NAME".
        /// </summary>
        public static string MutationName(MutationConfigSO mutation)
        {
            if (mutation == null) return "MUTAÇÃO";
            if (!string.IsNullOrEmpty(mutation.displayName))
                return mutation.displayName.ToUpperInvariant();
            string raw = !string.IsNullOrEmpty(mutation.displayNameKey) ? mutation.displayNameKey : mutation.mutationId;
            string human = Humanize(raw);
            return string.IsNullOrEmpty(human) ? "MUTAÇÃO" : human.ToUpperInvariant();
        }

        /// <summary>Nome amigável de um mundo.</summary>
        public static string WorldName(WorldConfigSO world, int worldIndex)
        {
            if (world == null) return "MUNDO " + worldIndex;
            string human = Humanize(world.displayNameKey);
            return string.IsNullOrEmpty(human) ? "MUNDO " + worldIndex : human.ToUpperInvariant();
        }

        // ---------------------------------------------------------------- Moeda compacta

        /// <summary>"1.250", "12.480" — separador de milhar PT (ponto). Para chips de carteira.</summary>
        public static string Coins(long value)
        {
            return value.ToString("#,0", PtBr).Replace(',', '.');
        }

        private static readonly CultureInfo PtBr = CultureInfo.InvariantCulture;
    }
}
