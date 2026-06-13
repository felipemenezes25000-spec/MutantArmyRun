using System;
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

        // ---------------------------------------------------------------- Passe de Temporada (contrato SeasonPass)

        /// <summary>Recompensa de UM nó do passe moldada para a UI — kind/amount do Domain + rótulo PT-BR pronto.</summary>
        public struct SeasonRewardView
        {
            public SeasonRewardKind kind;
            public int amount;
            public string label;        // "+250", "+10 gemas", "+5 frag", "SKIN" — pronto para o chip
            public bool hasReward;
        }

        /// <summary>Um nível (tier) do passe: trilha grátis + premium, e se já foi atingido pela XP.</summary>
        public struct SeasonTierView
        {
            public int tier;            // 1..TierCount
            public bool reached;        // XP de passe já alcançou este nível
            public SeasonRewardView free;
            public SeasonRewardView premium;
        }

        /// <summary>Estado geral do passe para o cabeçalho da tela (XP/nível/posse).</summary>
        public struct SeasonPassView
        {
            public int currentTier;     // 1..TierCount
            public int tierCount;
            public long totalXp;
            public int xpIntoTier;      // 0..XpPerTier
            public int xpPerTier;
            public float tierProgress01;
            public bool owned;          // passe premium comprado
            public float priceUsd;
        }

        // Cursor de RESGATE da sessão: o maior nível já coletado. Sem campo de save dedicado (dono:
        // SISTEMAS), o resgate não persiste entre sessões — ver avisos de integração. -1 = nada coletado
        // ainda nesta sessão; ao coletar, sobe para o nível resgatado e impede duplo-crédito.
        private static int s_seasonClaimedTier = -1;

        // Id de produto do passe (espelha IAPManager.ProductSeasonPass; UI não referencia Services).
        private const string SeasonPassProductId = "season_pass_699";

        /// <summary>
        /// XP de passe DERIVADA de forma determinística do que o save já tem (brief: "XP = baseado em
        /// fases vencidas/missões"). Não adiciona campo ao SaveData: soma fases vencidas (peso maior),
        /// missões reivindicadas e o nível do jogador. Pura sobre o save, então tela e resgate veem o
        /// MESMO progresso.
        /// </summary>
        public static long SeasonPassXp()
        {
            SaveData d = Save;
            if (d == null) return 0L;

            long xp = (long)Mathf.Max(0, d.highestLevelCleared) * 60L;   // ≈ 1 nível a cada ~1,7 fases
            xp += (long)Mathf.Max(0, d.playerLevel - 1) * 40L;
            int claimedMissions = 0;
            if (d.dailyMissions != null)
                for (int i = 0; i < d.dailyMissions.Count; i++)
                    if (d.dailyMissions[i] != null && d.dailyMissions[i].claimed) claimedMissions++;
            xp += claimedMissions * 50L;
            return xp;
        }

        public static bool SeasonPassOwned()
        {
            // Fonte viva: hook do IAPManager publicado no blackboard (UI não vê Services). Fallback
            // ao SaveData (Domain, visível) quando o IAP ainda não montou — concessão local de teste.
            if (GameBootstrap.Current != null && GameBootstrap.Current.SeasonPassOwned != null)
                return GameBootstrap.Current.SeasonPassOwned();
            return Save != null && Save.seasonPassActive;
        }

        public static SeasonPassView GetSeasonPass()
        {
            long xp = SeasonPassXp();
            return new SeasonPassView
            {
                currentTier = SeasonPassMath.TierForXp(xp),
                tierCount = SeasonPassMath.TierCount,
                totalXp = xp,
                xpIntoTier = SeasonPassMath.XpIntoTier(xp),
                xpPerTier = SeasonPassMath.XpPerTier,
                tierProgress01 = SeasonPassMath.TierProgress01(xp),
                owned = SeasonPassOwned(),
                priceUsd = SeasonPassMath.PriceUsd
            };
        }

        /// <summary>Todos os níveis do passe com as duas trilhas e o estado de atingido (pinta a lista).</summary>
        public static IReadOnlyList<SeasonTierView> SeasonTiers()
        {
            long xp = SeasonPassXp();
            var list = new List<SeasonTierView>(SeasonPassMath.TierCount);
            for (int t = 1; t <= SeasonPassMath.TierCount; t++)
            {
                list.Add(new SeasonTierView
                {
                    tier = t,
                    reached = SeasonPassMath.IsTierReached(t, xp),
                    free = ToRewardView(SeasonPassMath.FreeReward(t)),
                    premium = ToRewardView(SeasonPassMath.PremiumReward(t))
                });
            }
            return list;
        }

        private static SeasonRewardView ToRewardView(SeasonReward r)
        {
            return new SeasonRewardView
            {
                kind = r.kind,
                amount = r.amount,
                label = SeasonRewardLabel(r),
                hasReward = r.HasReward
            };
        }

        /// <summary>Rótulo curto PT-BR de um nó do passe (chip da trilha).</summary>
        public static string SeasonRewardLabel(SeasonReward r)
        {
            switch (r.kind)
            {
                case SeasonRewardKind.Coins: return "+" + r.amount;
                case SeasonRewardKind.Gems: return "+" + r.amount + " gemas";
                case SeasonRewardKind.Shards: return "+" + r.amount + " frag";
                case SeasonRewardKind.Skin: return "SKIN";
                default: return "—";
            }
        }

        /// <summary>Há recompensa de passe pronta para resgatar (níveis atingidos não coletados nesta sessão)?</summary>
        public static bool CanClaimSeasonRewards()
        {
            int current = SeasonPassMath.TierForXp(SeasonPassXp());
            return current > s_seasonClaimedTier && current >= 1;
        }

        /// <summary>
        /// Resgata TODAS as recompensas atingidas e não coletadas nesta sessão (grátis sempre; premium
        /// só com o passe comprado). Credita pelo funil REAL da Meta (EconomySystem.Earn /
        /// UnitManager.GrantShards) — nada escrito no save direto. Devolve o agregado para a tela
        /// celebrar. Idempotente por sessão via o cursor s_seasonClaimedTier.
        /// </summary>
        public static bool TryClaimSeasonRewards(out long coins, out int gems, out int shards, out int skins)
        {
            coins = 0; gems = 0; shards = 0; skins = 0;
            int current = SeasonPassMath.TierForXp(SeasonPassXp());
            if (current <= s_seasonClaimedTier) return false;

            bool owned = SeasonPassOwned();
            int start = s_seasonClaimedTier < 0 ? 1 : s_seasonClaimedTier + 1;
            for (int t = start; t <= current; t++)
            {
                AccumulateReward(SeasonPassMath.FreeReward(t), ref coins, ref gems, ref shards, ref skins);
                if (owned) AccumulateReward(SeasonPassMath.PremiumReward(t), ref coins, ref gems, ref shards, ref skins);
            }

            if (EconomySystem.Instance != null)
            {
                if (coins > 0) EconomySystem.Instance.Earn(CurrencyType.Coin, coins, "season_pass_claim");
                if (gems > 0) EconomySystem.Instance.Earn(CurrencyType.Gem, gems, "season_pass_claim");
            }
            if (shards > 0) GrantSeasonShards(shards);

            s_seasonClaimedTier = current;
            return coins > 0 || gems > 0 || shards > 0 || skins > 0;
        }

        private static void AccumulateReward(SeasonReward r, ref long coins, ref int gems, ref int shards, ref int skins)
        {
            switch (r.kind)
            {
                case SeasonRewardKind.Coins: coins += r.amount; break;
                case SeasonRewardKind.Gems: gems += r.amount; break;
                case SeasonRewardKind.Shards: shards += r.amount; break;
                case SeasonRewardKind.Skin: skins += 1; break;
            }
        }

        /// <summary>Espalha fragmentos do passe pela 1ª tropa do catálogo (genérico, sem pool de evento dedicado).</summary>
        private static void GrantSeasonShards(int shards)
        {
            UnitManager um = UnitManager.Instance;
            if (um == null || shards <= 0) return;
            UnitConfigSO target = um.GetConfig(0);
            if (target != null) um.GrantShards(target.unitId, shards);
        }

        /// <summary>
        /// Dispara a compra do passe pelo blackboard de IAP (UI não vê Services). Com provider Null o
        /// callback responde false e a tela mantém "EM BREVE"/preço (doc 12 §7.4). Concedido → callback
        /// true e a tela re-renderiza a trilha premium liberada.
        /// </summary>
        public static void TryBuySeasonPass(Action<bool> onResult)
        {
            GameBootstrap root = GameBootstrap.Current;
            if (root == null || root.PurchaseProduct == null)
            {
                if (onResult != null) onResult(false);
                return;
            }
            root.PurchaseProduct(SeasonPassProductId, SeasonPassMath.PriceUsd, "season_pass_screen",
                                 onResult ?? (_ => { }));
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
                case MissionSystem.MissionHitWeakness: return "Acerte a fraqueza de 1 boss";
                case MissionSystem.MissionEarnCombos: return "Faça 3 combos";
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

        // ---------------------------------------------------------------- Eventos (contrato EventsScreen)
        //
        // INTEGRAÇÃO (aviso ao orquestrador / dono do Meta): ainda NÃO há EventSystem dedicado.
        // Tudo aqui é DETERMINÍSTICO e LOCAL — derivado do que o SaveData JÁ tem (progresso de
        // missões para o evento diário, highestLevelCleared para o semanal) e do relógio do
        // sistema (tempo restante até a virada do dia/semana UTC). Quando o Meta publicar um
        // EventSystem real, este bloco passa a delegar a ele, sem tocar na EventsScreen (mesma
        // estratégia de degradação graciosa do bloco de login/missões acima). O ranking é
        // explicitamente LOCAL/placeholder — a tela rotula "em breve online".

        /// <summary>Card de um evento (diário ou semanal) — moldado para a EventsScreen.</summary>
        public struct EventView
        {
            public string title;
            public string desc;
            public int progress;
            public int target;
            public long rewardCoins;
            public int rewardGems;
            public long secondsRemaining;    // até a virada (dia/semana UTC)
            public bool complete;
        }

        /// <summary>
        /// Evento DIÁRIO: "vença fases hoje". Progresso reaproveita a missão diária de vencer
        /// fases quando o MissionSystem existe (já é determinístico/local); senão, alvo fixo com
        /// progresso 0. Tempo restante = até a próxima meia-noite UTC.
        /// </summary>
        public static EventView DailyEvent()
        {
            var v = new EventView
            {
                title = "DESAFIO DIÁRIO",
                desc = "Vença 5 fases hoje",
                target = 5,
                rewardCoins = 500,
                rewardGems = 5,
                secondsRemaining = SecondsUntilNextUtcMidnight()
            };

            IReadOnlyList<MissionView> missions = DailyMissions();
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].id == MissionSystem.MissionWinLevels)
                {
                    v.progress = missions[i].progress;
                    v.target = Mathf.Max(v.target, missions[i].target);
                    break;
                }
            }
            v.complete = v.progress >= v.target;
            return v;
        }

        /// <summary>
        /// Evento SEMANAL: "alcance fases novas esta semana". Progresso = highestLevelCleared
        /// (proxy honesto e determinístico do avanço). Tempo restante = até a virada de semana
        /// (segunda-feira 00:00 UTC).
        /// </summary>
        public static EventView WeeklyEvent()
        {
            int highest = HighestLevelCleared;
            return new EventView
            {
                title = "MARATONA SEMANAL",
                desc = "Avance pelo mapa esta semana",
                progress = highest,
                target = Mathf.Max(10, ((highest / 10) + 1) * 10),   // próxima dezena de fases
                rewardCoins = 2000,
                rewardGems = 20,
                secondsRemaining = SecondsUntilNextUtcWeek(),
                complete = false
            };
        }

        /// <summary>Uma linha do ranking local (placeholder honesto — top 10 + jogador destacado).</summary>
        public struct LeaderboardRow
        {
            public int rank;          // 1–10
            public string name;
            public long score;        // pontuação (proxy: fases vencidas × 100 + moedas/10)
            public bool isPlayer;
        }

        /// <summary>
        /// Ranking LOCAL determinístico (placeholder): 9 nomes-bot com pontuações fixas + o
        /// jogador inserido pela sua pontuação real (highestLevelCleared/coins), ordenado e
        /// recortado no top 10 com o jogador SEMPRE presente. Honesto: a tela rotula que é
        /// local e que o online "chega em breve". Nada disto escreve no save.
        /// </summary>
        public static IReadOnlyList<LeaderboardRow> LocalLeaderboard()
        {
            long playerScore = PlayerScore();
            var rows = new List<LeaderboardRow>(11)
            {
                new LeaderboardRow { name = "Comandante Áurea", score = 9800 },
                new LeaderboardRow { name = "General Vex",      score = 8600 },
                new LeaderboardRow { name = "Capitã Nyx",       score = 7400 },
                new LeaderboardRow { name = "Sargento Rook",    score = 6100 },
                new LeaderboardRow { name = "Tática Lumen",     score = 5200 },
                new LeaderboardRow { name = "Bruto Kael",       score = 4300 },
                new LeaderboardRow { name = "Veloz Pip",        score = 3500 },
                new LeaderboardRow { name = "Recruta Mox",      score = 2400 },
                new LeaderboardRow { name = "Aprendiz Tib",     score = 1500 },
                new LeaderboardRow { name = "VOCÊ",             score = playerScore, isPlayer = true }
            };

            rows.Sort((a, b) => b.score.CompareTo(a.score));

            // recorta no top 10 garantindo o jogador presente: se ele caiu fora do corte,
            // substitui o 10º colocado por ele (a lista de bots tem 9, então com o jogador são
            // 10 — mas mantemos a salvaguarda caso a lista de bots cresça no futuro).
            if (rows.Count > 10)
            {
                int playerIdx = rows.FindIndex(r => r.isPlayer);
                LeaderboardRow player = playerIdx >= 0 ? rows[playerIdx] : new LeaderboardRow { name = "VOCÊ", isPlayer = true };
                rows = rows.GetRange(0, 10);
                if (playerIdx < 0 || playerIdx >= 10) rows[9] = player;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                LeaderboardRow r = rows[i];
                r.rank = i + 1;
                rows[i] = r;
            }
            return rows;
        }

        /// <summary>Pontuação do jogador (proxy determinístico p/ o ranking local).</summary>
        public static long PlayerScore() => (long)HighestLevelCleared * 100L + Coins / 10L;

        private static long SecondsUntilNextUtcMidnight()
        {
            System.DateTime now = System.DateTime.UtcNow;
            System.DateTime nextMidnight = now.Date.AddDays(1);
            return (long)(nextMidnight - now).TotalSeconds;
        }

        private static long SecondsUntilNextUtcWeek()
        {
            System.DateTime now = System.DateTime.UtcNow;
            // virada na próxima segunda-feira 00:00 UTC
            int daysUntilMonday = ((int)System.DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            System.DateTime nextMonday = now.Date.AddDays(daysUntilMonday);
            return (long)(nextMonday - now).TotalSeconds;
        }

        /// <summary>Formata um tempo restante como "2d 5h", "5h 12m" ou "12m" (para os cards de evento).</summary>
        public static string FormatRemaining(long seconds)
        {
            if (seconds < 0) seconds = 0;
            long days = seconds / 86400L;
            long hours = (seconds % 86400L) / 3600L;
            long minutes = (seconds % 3600L) / 60L;
            if (days > 0) return days + "d " + hours + "h";
            if (hours > 0) return hours + "h " + minutes + "m";
            return minutes + "m";
        }
    }
}
