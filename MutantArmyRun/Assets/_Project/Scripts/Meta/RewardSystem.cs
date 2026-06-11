using System;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// Concessão de recompensas (doc 12 §3.1): baús, drops de boss (cartas/fragmentos) e
    /// baú diário. Funil único: TODO reward configurado (RewardConfigSO) passa por
    /// GrantReward, que credita via EconomySystem (transacional) e resolve o drop de carta
    /// com System.Random injetável — nunca UnityEngine.Random (reprodutível em teste).
    /// O "DOBRAR x2" da tela de vitória NÃO passa por aqui: é EconomySystem.CommitRun(won,
    /// multiplier: 2) após o rewarded (doc 12 §4.6/§4.13).
    /// </summary>
    public class RewardSystem : MonoBehaviour, IInitializable
    {
        public static RewardSystem Instance { get; private set; }

        private System.Random _rng;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable, após Economy/Upgrade/Unit.</summary>
        public void Init()
        {
            Init(new System.Random());
        }

        /// <summary>Overload com RNG injetado — testes fixam a seed e auditam os drops.</summary>
        public void Init(System.Random rng)
        {
            Instance = this;
            _rng = rng ?? new System.Random();
            // Gameplay não enxerga Meta (doc 12 §2.3): o drop de boss chega por EVENTO,
            // não por chamada direta do BossManager. -= antes de += evita inscrição dupla
            // se o Init rodar de novo (re-bootstrap em teste).
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnLevelFinished += HandleLevelFinished;
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelFinished -= HandleLevelFinished;
        }

        /// <summary>
        /// Vitória implica boss morto (TODA fase termina em boss, CANON §6) — concede o
        /// killReward do boss da fase atual. Roda no mesmo frame do Raise, antes da troca
        /// de tela (regra "dados prontos → tela mostra", doc 12 §3.2).
        /// </summary>
        private void HandleLevelFinished(LevelResult r)
        {
            if (!r.won || GameManager.Instance == null) return;
            LevelConfigSO level = GameManager.Instance.CurrentLevel;
            if (level != null && level.boss != null) GrantBossReward(level.boss);
        }

        /// <summary>Funil único de concessão: moedas/gemas/XP via EconomySystem + drop de carta.</summary>
        public void GrantReward(RewardConfigSO reward, string source)
        {
            if (reward == null || EconomySystem.Instance == null) return;
            if (reward.coins > 0) EconomySystem.Instance.Earn(CurrencyType.Coin, reward.coins, source);
            if (reward.gems > 0) EconomySystem.Instance.Earn(CurrencyType.Gem, reward.gems, source);
            if (reward.playerXp > 0) EconomySystem.Instance.Earn(CurrencyType.Xp, reward.playerXp, source);
            TryDropCard(reward, source);
        }

        /// <summary>Chamado pelo BossManager.Die (doc 12 §4.5): gemas + chance de carta/fragmento.</summary>
        public void GrantBossReward(BossConfigSO boss)
        {
            if (boss == null) return;
            GrantReward(boss.killReward, "boss_" + boss.bossId);
        }

        /// <summary>
        /// Drop de carta/fragmento: rola cardDropChance UMA vez; carta sorteada do cardPool.
        /// Tropa nova é desbloqueada; tropa conhecida recebe os fragmentos (CANON §8).
        /// </summary>
        private void TryDropCard(RewardConfigSO reward, string source)
        {
            if (reward.cardPool == null || reward.cardPool.Length == 0) return;
            if (reward.cardDropChance <= 0f || _rng.NextDouble() >= reward.cardDropChance) return;
            if (UnitManager.Instance == null) return;

            UnitConfigSO unit = reward.cardPool[_rng.Next(reward.cardPool.Length)];
            if (unit == null) return;

            if (!UnitManager.Instance.IsUnlocked(unit.unitId)) UnitManager.Instance.Unlock(unit.unitId);
            if (reward.shardAmount > 0) UnitManager.Instance.AddShards(unit.unitId, reward.shardAmount);
        }

        // ---- Baú diário (CANON §11: baú extra diário é um dos 5 placements de rewarded) ----

        /// <summary>Disponível 1× por dia-calendário UTC (consistente entre fusos e troca de relógio).</summary>
        public bool CanClaimDailyChest()
        {
            if (SaveSystem.Instance == null) return false;
            long last = SaveSystem.Instance.Data.lastDailyChestUnix;
            if (last <= 0) return true;
            DateTime lastDay = DateTimeOffset.FromUnixTimeSeconds(last).UtcDateTime.Date;
            return DateTime.UtcNow.Date > lastDay;
        }

        public bool TryClaimDailyChest(RewardConfigSO chestReward)
        {
            if (chestReward == null || !CanClaimDailyChest()) return false;
            SaveSystem.Instance.Data.lastDailyChestUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveSystem.Instance.MarkDirty();
            GrantReward(chestReward, "daily_chest");
            return true;
        }

        // ---- Baú comprado na loja (CANON §8: baú raro = 300 gemas, chave RC chest_rare_gem_price) ----

        /// <summary>Compra transacional: só concede o conteúdo com o débito de gemas confirmado.</summary>
        public bool TryBuyChest(RewardConfigSO chestReward, int gemPrice, string source)
        {
            if (chestReward == null || EconomySystem.Instance == null) return false;
            if (!EconomySystem.Instance.TrySpend(CurrencyType.Gem, gemPrice, source)) return false;
            GrantReward(chestReward, source);
            return true;
        }
    }
}
