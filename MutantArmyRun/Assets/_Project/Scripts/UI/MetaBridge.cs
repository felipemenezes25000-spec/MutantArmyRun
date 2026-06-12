using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Fachada de LEITURA/ESCRITA que as telas de meta (Tropas/Upgrades/Loja/Mapa/Diário)
    /// consomem. Existe por DOIS motivos de fronteira:
    ///
    /// (1) Forma do CONTRATO compartilhado (brief F4): as telas falam em TroopProgress,
    ///     efeito acumulado de trilha, ChestResult e missões/login — estruturas estáveis,
    ///     independentes de qual nome o agente de SISTEMAS deu ao método concreto. Este
    ///     bridge traduz o contrato ↔ a API REAL do Meta de hoje (UpgradeSystem.GetTrackLevel/
    ///     TryBuyUpgrade, UnitManager.GetProgress/TryEvolve, RewardSystem.TryBuyChest…) num
    ///     ÚNICO ponto: se SISTEMAS renomear/estender, ajusta-se aqui, não em 5 telas.
    ///
    /// (2) Degradação graciosa: missões diárias e calendário de login ainda não têm sistema
    ///     dedicado no Meta. O bridge deriva o estado do que o SaveData JÁ tem (loginStreak,
    ///     lastDailyChestUnix, sessionCount, highestLevelCleared) — sem ADITIVAR campos no
    ///     SaveData (dono: SISTEMAS). Quando SISTEMAS publicar MissionSystem/LoginReward, este
    ///     bridge passa a delegar a ele (ver avisos de integração no resumo da task).
    ///
    /// NÃO é um MonoBehaviour: métodos estáticos puros sobre as instâncias singleton do Meta.
    /// Toda mutação continua transacional (passa por EconomySystem/UnitManager/UpgradeSystem) —
    /// o bridge nunca escreve moeda no save direto.
    /// </summary>
    public static class MetaBridge
    {
        // ---------------------------------------------------------------- Carteira (contrato Economy)

        public static long Coins => EconomySystem.Instance != null ? EconomySystem.Instance.Coins : SaveCoins();
        public static int Gems => EconomySystem.Instance != null ? EconomySystem.Instance.Gems : SaveGems();
        public static int PlayerLevel => Save != null ? Save.playerLevel : 1;
        public static int HighestLevelCleared => Save != null ? Save.highestLevelCleared : 0;

        private static SaveData Save => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        private static long SaveCoins() { SaveData d = Save; return d != null ? d.coins : 0L; }
        private static int SaveGems() { SaveData d = Save; return d != null ? d.gems : 0; }

        // ---------------------------------------------------------------- Tropas (contrato CardSystem)

        /// <summary>Snapshot da carta de uma tropa — moldado no contrato TroopProgress.</summary>
        public struct TroopView
        {
            public UnitConfigSO config;
            public bool unlocked;
            public int level;            // 1–10
            public int shards;
            public int shardsToNext;     // 0 quando no nível máx.
            public bool maxed;
            public bool canEvolve;       // fragmentos suficientes E não maxado (custo de moeda checado no TryEvolve)
            public long evolveCoinCost;  // custo em moedas exibido no detalhe (estimativa coerente com o Meta)
        }

        /// <summary>Catálogo completo de tropas (ordem do catálogo do UnitManager = typeId).</summary>
        public static IReadOnlyList<UnitConfigSO> AllTroops()
        {
            var list = new List<UnitConfigSO>();
            UnitManager um = UnitManager.Instance;
            if (um == null) return list;
            for (int i = 0; i < um.CatalogSize; i++)
            {
                UnitConfigSO c = um.GetConfig(i);
                if (c != null) list.Add(c);
            }
            return list;
        }

        public static TroopView GetTroop(string unitId)
        {
            var view = new TroopView { level = 1 };
            UnitManager um = UnitManager.Instance;
            if (um == null || string.IsNullOrEmpty(unitId)) return view;

            view.config = um.GetConfig(unitId);
            // Contrato final do Meta: GetProgress devolve o struct TroopProgress (valor, nunca null).
            TroopProgress p = um.GetProgress(unitId);
            view.unlocked = p.unlocked;
            view.level = Mathf.Max(1, p.level);
            view.shards = p.shards;

            view.maxed = view.level >= UnitManager.MaxUnitLevel;
            view.shardsToNext = view.maxed ? 0 : um.GetShardsToNextLevel(unitId);
            view.canEvolve = um.CanEvolve(unitId);
            // Custo real de moedas da evolução (doc 07 §6: 100 × 2^(n−1) × raridade) — exposto pelo Meta.
            view.evolveCoinCost = view.maxed ? 0L : um.EvolveCoinCost(unitId, view.level);
            return view;
        }

        public static bool CanEvolve(string unitId) => GetTroop(unitId).canEvolve;

        /// <summary>Evolução transacional — delega ao UnitManager (gasta fragmentos + moedas, sobe nível).</summary>
        public static bool TryEvolve(string unitId)
        {
            return UnitManager.Instance != null && UnitManager.Instance.TryEvolve(unitId);
        }

        public static float GetEffectiveHp(string unitId)
            => UnitManager.Instance != null ? UnitManager.Instance.GetEffectiveHp(unitId) : 0f;

        public static float GetEffectiveDps(string unitId)
            => UnitManager.Instance != null ? UnitManager.Instance.GetEffectiveDps(unitId) : 0f;

        public static float GetEffectiveMoveSpeed(string unitId)
            => UnitManager.Instance != null ? UnitManager.Instance.GetEffectiveMoveSpeed(unitId) : 0f;

        // ---------------------------------------------------------------- Upgrades (contrato UpgradeSystem)

        /// <summary>As 8 trilhas na ordem canônica (CANON §9) — a UI itera sobre isto.</summary>
        public static readonly UpgradeTrack[] AllTracks =
        {
            UpgradeTrack.StartDamage, UpgradeTrack.StartHealth, UpgradeTrack.Speed,
            UpgradeTrack.RewardMultiplier, UpgradeTrack.StartArmy, UpgradeTrack.CritChance,
            UpgradeTrack.BossDamage, UpgradeTrack.ObstacleResist
        };

        public static int GetUpgradeLevel(UpgradeTrack t)
            => UpgradeSystem.Instance != null ? UpgradeSystem.Instance.GetTrackLevel(t) : 0;

        public static int GetUpgradeMaxLevel(UpgradeTrack t)
            => UpgradeSystem.Instance != null ? UpgradeSystem.Instance.GetMaxLevel(t) : 50;

        public static long GetUpgradeCost(UpgradeTrack t)
            => UpgradeSystem.Instance != null ? UpgradeSystem.Instance.GetUpgradeCost(t) : 0L;

        /// <summary>
        /// Efeito ACUMULADO da trilha como fração (ex.: 0.25 = +25%). StartArmy é especial:
        /// retorna o número de UNIDADES extras (use <see cref="IsUnitTrack"/> para exibir "un.").
        /// </summary>
        public static float GetUpgradeEffect(UpgradeTrack t)
            => UpgradeSystem.Instance != null ? UpgradeSystem.Instance.GetBonus(t) : 0f;

        public static bool IsUpgradeMaxed(UpgradeTrack t) => GetUpgradeLevel(t) >= GetUpgradeMaxLevel(t);

        public static bool CanBuyUpgrade(UpgradeTrack t) => !IsUpgradeMaxed(t) && Coins >= GetUpgradeCost(t);

        /// <summary>Compra transacional — delega ao UpgradeSystem (gasta moedas, sobe nível).</summary>
        public static bool TryBuyUpgrade(UpgradeTrack t)
            => UpgradeSystem.Instance != null && UpgradeSystem.Instance.TryBuyUpgrade(t);

        /// <summary>StartArmy mede unidades inteiras; as demais trilhas são percentuais (CANON §9).</summary>
        public static bool IsUnitTrack(UpgradeTrack t) => t == UpgradeTrack.StartArmy;

        // ---------------------------------------------------------------- Loja / Baús (contrato RewardSystem)

        /// <summary>Os tipos de baú compráveis na loja, do mais barato ao mais caro.</summary>
        public enum ShopChest { Rare, Epic, Legendary }

        public static ChestType ToChestType(ShopChest c)
        {
            switch (c)
            {
                case ShopChest.Epic: return ChestType.Epic;
                case ShopChest.Legendary: return ChestType.Legendary;
                default: return ChestType.Rare;
            }
        }

        /// <summary>
        /// Compra de baú por GEMAS pelo contrato real (RewardSystem.OpenChest via TryBuyChest):
        /// debita as gemas e devolve o ChestResult já creditado (moedas/gemas/fragmentos no funil
        /// transacional). A tela mostra o resumo a partir do resultado — as % de drop vêm do
        /// Domain.ChestMath (publicadas na própria UI, doc 09 P9).
        /// </summary>
        public static bool TryBuyChest(ShopChest chest, int gemPrice, out ChestResult result)
        {
            result = default;
            return RewardSystem.Instance != null &&
                   RewardSystem.Instance.TryBuyChest(ToChestType(chest), gemPrice, out result);
        }

        public static bool CanClaimDailyChest()
            => RewardSystem.Instance != null && RewardSystem.Instance.CanClaimDailyChest();

        /// <summary>Baú grátis diário pelo tipo (Comum no MVP) — devolve o ChestResult aberto.</summary>
        public static bool TryClaimDailyChest(out ChestResult result)
        {
            result = default;
            return RewardSystem.Instance != null &&
                   RewardSystem.Instance.TryClaimDailyChest(ChestType.Common, out result);
        }

        // ---------------------------------------------------------------- Diário: login + missões (MissionSystem)

        /// <summary>
        /// Estado do calendário de login — delega ao MissionSystem (struct LoginReward do contrato).
        /// </summary>
        public struct LoginView
        {
            public int streakDay;        // 1–7 (cicla)
            public bool claimedToday;
            public long todayCoins;
            public int todayGems;
        }

        public static LoginView TodayLogin()
        {
            var v = new LoginView { streakDay = 1 };
            if (MissionSystem.Instance == null) return v;
            LoginReward r = MissionSystem.Instance.TodayLogin();
            v.streakDay = Mathf.Clamp(r.day, 1, 7);
            v.todayCoins = r.coins;
            v.todayGems = r.gems;
            v.claimedToday = r.claimedToday;
            return v;
        }

        /// <summary>Recompensa de moedas do dia do ciclo (1–7) — para pintar o calendário inteiro.</summary>
        public static long LoginCoinsForDay(int day1to7)
            => MissionMath.LoginRewardCoins(Mathf.Clamp(day1to7, 1, 7));

        public static int LoginGemsForDay(int day1to7)
            => MissionMath.LoginRewardGems(Mathf.Clamp(day1to7, 1, 7));

        /// <summary>Reclamação do login do dia — delega ao MissionSystem (avança streak, credita).</summary>
        public static bool TryClaimLogin()
            => MissionSystem.Instance != null && MissionSystem.Instance.ClaimLogin();

        // ---- Missões ----

        /// <summary>Missão diária moldada para a UI — espelha o struct DailyMission do MissionSystem.</summary>
        public struct MissionView
        {
            public string id;
            public string desc;          // descrição PT-BR resolvida da descKey
            public int progress;
            public int target;
            public bool complete;
            public bool claimed;
            public long rewardCoins;
            public int rewardGems;
        }

        public static IReadOnlyList<MissionView> DailyMissions()
        {
            var list = new List<MissionView>(3);
            if (MissionSystem.Instance == null) return list;
            IReadOnlyList<DailyMission> missions = MissionSystem.Instance.DailyMissions();
            for (int i = 0; i < missions.Count; i++)
            {
                DailyMission m = missions[i];
                list.Add(new MissionView
                {
                    id = m.id,
                    desc = MissionDesc(m.id),
                    progress = m.progress,
                    target = m.target,
                    complete = m.IsComplete,
                    claimed = m.claimed,
                    rewardCoins = m.rewardCoins,
                    rewardGems = m.rewardGems
                });
            }
            return list;
        }

        /// <summary>Reclama a recompensa de uma missão completa — delega ao MissionSystem.</summary>
        public static bool TryClaimMission(string id)
            => MissionSystem.Instance != null && MissionSystem.Instance.ClaimMission(id);

        /// <summary>Descrição PT-BR de cada missão (sem tabela de loc no MVP, doc 09 §6).</summary>
        private static string MissionDesc(string id)
        {
            switch (id)
            {
                case MissionSystem.MissionWinLevels: return "Vença 3 fases";
                case MissionSystem.MissionChooseGates: return "Atravesse 5 portais";
                case MissionSystem.MissionDefeatBosses: return "Derrote 1 boss";
                default: return id;
            }
        }

        // ---------------------------------------------------------------- Mapa (contrato Mundos)

        public struct WorldView
        {
            public WorldConfigSO config;
            public int worldIndex;       // 1–10
            public int firstLevel;       // 1, 11, 21, …
            public int clearedInWorld;   // 0–10
            public bool unlocked;        // o 1º acessível; os demais por highestLevelCleared
        }

        public const int WorldCount = 10;
        public const int LevelsPerWorld = 10;

        /// <summary>Os 10 mundos derivados do catálogo de fases (GameSettings), com progresso por save.</summary>
        public static IReadOnlyList<WorldView> Worlds()
        {
            var list = new List<WorldView>(WorldCount);
            GameSettingsSO settings = GameSettingsSO.Load();
            int highest = HighestLevelCleared;

            for (int w = 1; w <= WorldCount; w++)
            {
                int first = (w - 1) * LevelsPerWorld + 1;
                int last = w * LevelsPerWorld;
                WorldConfigSO cfg = null;
                if (settings != null)
                {
                    LevelConfigSO lv = settings.GetLevel(first);
                    if (lv != null) cfg = lv.world;
                }
                int cleared = Mathf.Clamp(highest - (first - 1), 0, LevelsPerWorld);
                // Mundo desbloqueado se a 1ª fase dele é a próxima jogável ou anterior
                // (highest+1 >= first). O mundo 1 sempre acessível.
                bool unlocked = highest + 1 >= first || w == 1;
                list.Add(new WorldView
                {
                    config = cfg,
                    worldIndex = w,
                    firstLevel = first,
                    clearedInWorld = cleared,
                    unlocked = unlocked
                });
                if (cfg == null) { /* catálogo incompleto: card ainda exibe nome genérico */ }
                _ = last;
            }
            return list;
        }

        /// <summary>Próxima fase jogável do mundo (1ª não-vencida, ou a 1ª se nenhuma vencida).</summary>
        public static int NextPlayableLevelInWorld(WorldView world)
        {
            int next = HighestLevelCleared + 1;
            int first = world.firstLevel;
            int last = world.firstLevel + LevelsPerWorld - 1;
            if (next < first) return first;     // mundo ainda intocado: começa na 1ª
            if (next > last) return last;       // mundo já zerado: rejoga a última (farm)
            return next;
        }
    }
}
