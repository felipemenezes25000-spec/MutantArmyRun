using System;
using System.Collections.Generic;
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
    ///
    /// CONTRATO DE API (baús, consumido pelo agente de telas): ChestType {Common,Rare,Epic,
    /// Legendary,World} · struct ChestResult · ChestResult OpenChest(ChestType). As drop tables
    /// vêm do Domain.ChestMath (% explícitas por raridade, doc 07 §4); o pity de Lendário é
    /// global e persistido em SaveData.chestPityCounter. OpenChest credita moedas/gemas via
    /// EconomySystem e os fragmentos via UnitManager — tudo no funil transacional.
    /// </summary>
    public class RewardSystem : MonoBehaviour, IInitializable
    {
        public static RewardSystem Instance { get; private set; }

        private System.Random _rng;

        // Pity recalibrável por RC (doc 07 §9: chest_pity_legendary, default 50).
        private const string PityKey = "chest_pity_legendary";
        private const int DefaultPity = 50;
        private IRemoteConfigProvider _remoteConfig;

        /// <summary>Disparado ao abrir um baú — telas tocam a animação de slow-open (doc 12 §3.2).</summary>
        public event Action<ChestResult> OnChestOpened;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable, após Economy/Upgrade/Unit.</summary>
        public void Init()
        {
            Init(new System.Random(), ResolveRemoteConfig());
        }

        /// <summary>Overload com RNG injetado — testes fixam a seed e auditam os drops.</summary>
        public void Init(System.Random rng) => Init(rng, ResolveRemoteConfig());

        public void Init(System.Random rng, IRemoteConfigProvider remoteConfig)
        {
            Instance = this;
            _rng = rng ?? new System.Random();
            _remoteConfig = remoteConfig;
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

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
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
            if (level == null) return;
            if (level.boss != null) GrantBossReward(level.boss);
            // Recompensa extra da fase (CANON §16: fase 10 = baú épico + 50 gemas) — o
            // winReward do LevelConfigSO entra por aqui; sem isto o asset nunca paga.
            if (level.winReward != null) GrantReward(level.winReward, "level_win_" + level.levelIndex);
        }

        /// <summary>Funil único de concessão: moedas/gemas/XP via EconomySystem + drop de carta.</summary>
        public void GrantReward(RewardConfigSO reward, string source)
        {
            if (reward == null || EconomySystem.Instance == null) return;
            if (reward.coins > 0) EconomySystem.Instance.Earn(CurrencyType.Coin, reward.coins, source);
            if (reward.gems > 0) EconomySystem.Instance.Earn(CurrencyType.Gem, reward.gems, source);
            if (reward.playerXp > 0) EconomySystem.Instance.Earn(CurrencyType.Xp, reward.playerXp, source);
            TryDropCard(reward, source);

            // Baú embutido na recompensa (CANON §16: fase 10 = baú épico). Abre na hora e o
            // conteúdo entra pelo mesmo funil — sem este passo o campo chest do SO era decorativo.
            if (reward.chest != ChestType.None) OpenChest(reward.chest, reward.cardPool, source + "_chest");
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

            // GrantShards já desbloqueia ao cruzar 10 fragmentos (doc 07 §2.3); shardAmount default.
            int shards = reward.shardAmount > 0 ? reward.shardAmount : 1;
            UnitManager.Instance.GrantShards(unit.unitId, shards);
        }

        // ==================================================================
        // CONTRATO DE API — baús (agente de telas)
        // ==================================================================

        /// <summary>
        /// Abre um baú do tipo dado (contrato de telas), sorteando o pool do CATÁLOGO desbloqueável.
        /// Sobrecarga sem pool explícito — usa o catálogo do UnitManager filtrado por raridade.
        /// </summary>
        public ChestResult OpenChest(ChestType type)
        {
            return OpenChest(type, null, "chest_" + type);
        }

        /// <summary>
        /// Núcleo da abertura (doc 07 §4): rola N pacotes (raridade por % da tabela + pity),
        /// sorteia 1 tropa daquela raridade no pool, entrega os fragmentos fixos da raridade,
        /// e credita moedas/gemas. <paramref name="pool"/> null = catálogo completo do UnitManager.
        /// Resultado agregado por unitId (telas mostram "+N fragmentos do Mago", etc.).
        /// </summary>
        public ChestResult OpenChest(ChestType type, UnitConfigSO[] pool, string source)
        {
            ChestKind kind = MapKind(type);
            ChestMath.ChestTable table = ChestMath.TableFor(kind);
            int pity = GetPityThreshold();

            // Agrega fragmentos por tropa para o resultado (telas listam por carta).
            var shardsByUnit = new Dictionary<string, int>();
            bool guaranteeMet = false;   // doc 07 §4: ≥1 pacote no piso de raridade do baú

            for (int packet = 0; packet < table.Packets; packet++)
            {
                Rarity rarity = ChestMath.RollPacketRarity(table, _rng.NextDouble());
                rarity = ChestMath.ApplyPity(rarity, GetPityCounter(), pity);

                // Garantia de piso: força o ÚLTIMO pacote ao piso se nenhum anterior o atingiu.
                bool isLast = packet == table.Packets - 1;
                if (table.HasGuarantee && isLast && !guaranteeMet && (int)rarity < (int)table.GuaranteedFloor)
                    rarity = table.GuaranteedFloor;

                if (table.HasGuarantee && (int)rarity >= (int)table.GuaranteedFloor) guaranteeMet = true;

                AdvancePity(rarity);

                UnitConfigSO unit = PickUnitOfRarity(rarity, pool);
                if (unit == null) continue;

                int shards = ChestMath.ShardsForRarity(rarity);
                if (shardsByUnit.TryGetValue(unit.unitId, out int cur)) shardsByUnit[unit.unitId] = cur + shards;
                else shardsByUnit[unit.unitId] = shards;
            }

            // Moedas/gemas do baú (× Mb resolvido pela tela/economia futura; MVP usa o bruto).
            long coins = ChestMath.RollCoins(table, _rng.NextDouble());
            int gems = table.Gems;

            // Concede tudo pelo funil transacional + nos cards.
            if (coins > 0 && EconomySystem.Instance != null)
                EconomySystem.Instance.Earn(CurrencyType.Coin, coins, source);
            if (gems > 0 && EconomySystem.Instance != null)
                EconomySystem.Instance.Earn(CurrencyType.Gem, gems, source);
            if (UnitManager.Instance != null)
                foreach (var kv in shardsByUnit)
                    UnitManager.Instance.GrantShards(kv.Key, kv.Value);

            ChestResult result = BuildResult(shardsByUnit, coins, gems);
            OnChestOpened?.Invoke(result);
            return result;
        }

        private static ChestKind MapKind(ChestType type)
        {
            switch (type)
            {
                case ChestType.Rare: return ChestKind.Rare;
                case ChestType.Epic: return ChestKind.Epic;
                case ChestType.Legendary: return ChestKind.Legendary;
                case ChestType.World: return ChestKind.World;
                default: return ChestKind.Common;   // None/Common
            }
        }

        /// <summary>Sorteia 1 tropa da raridade no pool (ou no catálogo); rebaixa se não houver da raridade.</summary>
        private UnitConfigSO PickUnitOfRarity(Rarity rarity, UnitConfigSO[] pool)
        {
            IReadOnlyList<UnitConfigSO> source = pool != null && pool.Length > 0
                ? (IReadOnlyList<UnitConfigSO>)pool
                : (UnitManager.Instance != null ? UnitManager.Instance.AllTroops() : null);
            if (source == null || source.Count == 0) return null;

            // 1ª passada: exatamente a raridade sorteada. Fallback: maior raridade ≤ sorteada
            // disponível (MVP sem Lendárias rebaixa para Épico — doc 07 §4).
            UnitConfigSO best = null;
            int bestRarity = -1;
            var exact = new List<UnitConfigSO>();
            for (int i = 0; i < source.Count; i++)
            {
                UnitConfigSO u = source[i];
                if (u == null) continue;
                if (u.rarity == rarity) exact.Add(u);
                else if ((int)u.rarity <= (int)rarity && (int)u.rarity > bestRarity)
                {
                    bestRarity = (int)u.rarity;
                    best = u;
                }
            }
            if (exact.Count > 0) return exact[_rng.Next(exact.Count)];
            return best;   // rebaixa para a maior raridade disponível ≤ sorteada
        }

        private static ChestResult BuildResult(Dictionary<string, int> shardsByUnit, long coins, int gems)
        {
            int n = shardsByUnit.Count;
            string[] ids = new string[n];
            int[] shards = new int[n];
            int idx = 0;
            foreach (var kv in shardsByUnit)
            {
                ids[idx] = kv.Key;
                shards[idx] = kv.Value;
                idx++;
            }
            return new ChestResult(ids, shards, coins, gems);
        }

        // ---- Pity persistido (SaveData.chestPityCounter) ----

        private int GetPityThreshold()
        {
            int rc = _remoteConfig != null ? _remoteConfig.GetInt(PityKey, DefaultPity) : DefaultPity;
            return rc > 0 ? rc : DefaultPity;
        }

        private int GetPityCounter()
        {
            return SaveSystem.Instance != null ? SaveSystem.Instance.Data.chestPityCounter : 0;
        }

        // Cada pacote incrementa o contador; um Lendário (sorteado ou por pity) zera (doc 07 §4).
        private void AdvancePity(Rarity rarity)
        {
            if (SaveSystem.Instance == null) return;
            SaveData d = SaveSystem.Instance.Data;
            if (rarity == Rarity.Legendary) d.chestPityCounter = 0;
            else d.chestPityCounter++;
            SaveSystem.Instance.MarkDirty();
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

        /// <summary>Baú diário pelo tipo (contrato de telas): abre 1×/dia UTC e devolve o conteúdo.</summary>
        public bool TryClaimDailyChest(ChestType type, out ChestResult result)
        {
            result = default;
            if (!CanClaimDailyChest()) return false;
            SaveSystem.Instance.Data.lastDailyChestUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveSystem.Instance.MarkDirty();
            result = OpenChest(type, null, "daily_chest");
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

        /// <summary>Compra de baú por tipo (contrato de telas): debita gemas e devolve o ChestResult.</summary>
        public bool TryBuyChest(ChestType type, int gemPrice, out ChestResult result)
        {
            result = default;
            if (EconomySystem.Instance == null) return false;
            if (!EconomySystem.Instance.TrySpend(CurrencyType.Gem, gemPrice, "buy_chest_" + type)) return false;
            result = OpenChest(type, null, "buy_chest_" + type);
            return true;
        }
    }

    /// <summary>
    /// Resultado da abertura de um baú (contrato de telas). unitIds[i] recebeu shards[i] fragmentos
    /// (índices paralelos); coins/gems já creditados. Arrays vazios = baú só de moeda. Struct de
    /// valor — a tela copia o que precisa para a animação de slow-open.
    /// </summary>
    public readonly struct ChestResult
    {
        public readonly string[] unitIds;
        public readonly int[] shards;
        public readonly long coins;
        public readonly int gems;

        public ChestResult(string[] unitIds, int[] shards, long coins, int gems)
        {
            this.unitIds = unitIds ?? Array.Empty<string>();
            this.shards = shards ?? Array.Empty<int>();
            this.coins = coins;
            this.gems = gems;
        }
    }
}
