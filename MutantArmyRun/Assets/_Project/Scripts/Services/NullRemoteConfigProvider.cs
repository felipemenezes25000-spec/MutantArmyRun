using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Provider de Remote Config sem SDK (doc 12 §4.10): devolve os Defaults embutidos,
    /// que espelham o CANON — o jogo é jogável de fábrica, sem rede. O wrapper de Firebase
    /// substitui este componente no prefab [Services] mantendo os mesmos defaults.
    /// </summary>
    public class NullRemoteConfigProvider : MonoBehaviour, IRemoteConfigProvider
    {
        // Tabela EXATA do doc 12 §4.10. Chaves sempre via RcKeys (Core) — sem string mágica.
        private static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>
        {
            [RcKeys.LevelRewardBase] = 100f,
            [RcKeys.LevelRewardGrowth] = 1.10f,
            [RcKeys.UpgradeCostBase] = 100f,
            [RcKeys.UpgradeCostGrowth] = 1.35f,
            [RcKeys.InterMinLevel] = 6,
            [RcKeys.InterLevelGap] = 3,
            [RcKeys.SupplyOverflowCoinRate] = 2,
            [RcKeys.SupplyCap] = 60,
            [RcKeys.BossHpGlobalMult] = 1f,
            [RcKeys.ChestRareGemPrice] = 300,
        };

        public Task InitAsync(bool online, int timeoutMs)
        {
            // Sem rede para buscar: os defaults já estão "ativados" desde a construção.
            return Task.CompletedTask;
        }

        public int GetInt(string key, int fallbackValue)
        {
            object value;
            if (Defaults.TryGetValue(key, out value))
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return fallbackValue;
        }

        public float GetFloat(string key, float fallbackValue)
        {
            object value;
            if (Defaults.TryGetValue(key, out value))
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            return fallbackValue;
        }

        public bool GetBool(string key, bool fallbackValue)
        {
            object value;
            if (Defaults.TryGetValue(key, out value) && value is bool typed) return typed;
            return fallbackValue;
        }
    }
}
