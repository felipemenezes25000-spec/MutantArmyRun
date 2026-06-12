using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// Retenção diária (doc 07 §2/§3): 3 missões diárias + login com streak de 7 dias. As missões
    /// AVANÇAM assinando os eventos do jogo (OnLevelFinished → vencer fase / derrotar boss;
    /// OnGateConsumed → escolher portal) e dão recompensa ao reclamar. Reset por dia-calendário
    /// UTC (Domain.MissionMath) — consistente entre fusos e troca de relógio. Persistido em
    /// SaveData.dailyMissions/lastMissionResetUnix/lastLoginRewardUnix/loginStreak.
    ///
    /// CONTRATO DE API (consumido pelo agente de telas):
    /// IReadOnlyList&lt;DailyMission&gt; DailyMissions() · bool ClaimMission(id) ·
    /// LoginReward TodayLogin() · bool ClaimLogin().
    /// </summary>
    public class MissionSystem : MonoBehaviour, IInitializable
    {
        public static MissionSystem Instance { get; private set; }

        // Ids estáveis dos 3 tipos (CANON: vencer N fases, escolher N portais, derrotar N bosses).
        public const string MissionWinLevels = "win_levels";
        public const string MissionChooseGates = "choose_gates";
        public const string MissionDefeatBosses = "defeat_bosses";

        private IRemoteConfigProvider _remoteConfig;
        private const string WorldMultKey = "chest_coin_mult_world";   // Mb (doc 07 §9); fallback 1.0

        /// <summary>Disparado quando uma missão avança ou é reclamada (telas re-renderizam, doc 12 §3.2).</summary>
        public event Action OnMissionsChanged;
        /// <summary>Disparado quando o login diário é reclamado.</summary>
        public event Action OnLoginClaimed;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable, após Economy/Reward.</summary>
        public void Init()
        {
            Init(ResolveRemoteConfig());
        }

        public void Init(IRemoteConfigProvider remoteConfig)
        {
            Instance = this;
            _remoteConfig = remoteConfig;

            EnsureDailyMissions();   // gera/reseta as 3 do dia se virou o dia UTC

            // Hooks dos eventos do jogo (doc 12 §3.2). -= antes de += para re-Init não duplicar.
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnGateConsumed += HandleGateConsumed;
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnGateConsumed -= HandleGateConsumed;
        }

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
        }

        // ==================================================================
        // Geração / reset diário (Domain.MissionMath, dia-calendário UTC)
        // ==================================================================

        private void EnsureDailyMissions()
        {
            if (SaveSystem.Instance == null) return;
            SaveData d = SaveSystem.Instance.Data;
            long now = NowUnix();

            bool needReset = d.dailyMissions == null || d.dailyMissions.Count == 0
                             || MissionMath.IsNewUtcDay(d.lastMissionResetUnix, now);
            if (!needReset) return;

            d.dailyMissions = new List<MissionProgress>
            {
                new MissionProgress { missionId = MissionWinLevels, progress = 0, target = 3, claimed = false },
                new MissionProgress { missionId = MissionChooseGates, progress = 0, target = 5, claimed = false },
                new MissionProgress { missionId = MissionDefeatBosses, progress = 0, target = 1, claimed = false }
            };
            d.lastMissionResetUnix = now;
            SaveSystem.Instance.MarkDirty();
            OnMissionsChanged?.Invoke();
        }

        // ==================================================================
        // Avanço por eventos do jogo
        // ==================================================================

        private void HandleLevelFinished(LevelResult r)
        {
            if (!r.won) return;
            // toda vitória = 1 fase vencida E 1 boss derrotado (CANON §6: toda fase termina em boss).
            Advance(MissionWinLevels, 1);
            Advance(MissionDefeatBosses, 1);
        }

        private void HandleGateConsumed(GateResult g)
        {
            Advance(MissionChooseGates, 1);
        }

        /// <summary>Avança o progresso de uma missão (clamp no alvo); persiste e notifica se mudou.</summary>
        public void Advance(string missionId, int amount)
        {
            if (amount <= 0 || SaveSystem.Instance == null) return;
            EnsureDailyMissions();
            MissionProgress m = Find(missionId);
            if (m == null || m.claimed || m.progress >= m.target) return;

            m.progress = Mathf.Min(m.target, m.progress + amount);
            SaveSystem.Instance.MarkDirty();
            OnMissionsChanged?.Invoke();
        }

        // ==================================================================
        // CONTRATO DE API — missões (agente de telas)
        // ==================================================================

        /// <summary>As 3 missões do dia como structs de valor (contrato de telas).</summary>
        public IReadOnlyList<DailyMission> DailyMissions()
        {
            EnsureDailyMissions();
            var list = new List<DailyMission>();
            if (SaveSystem.Instance == null) return list;

            float mb = WorldMult();
            long coins = MissionMath.MissionCoins(mb);
            foreach (MissionProgress m in SaveSystem.Instance.Data.dailyMissions)
            {
                list.Add(new DailyMission(
                    id: m.missionId,
                    descKey: "mission_" + m.missionId,
                    progress: m.progress,
                    target: m.target,
                    claimed: m.claimed,
                    rewardCoins: coins,
                    rewardGems: MissionMath.MissionRewardGems));
            }
            return list;
        }

        /// <summary>
        /// Reclama a recompensa de uma missão COMPLETA (contrato de telas): credita moedas/gemas
        /// via EconomySystem, marca claimed. Concede o bônus de gemas ao reclamar a 3ª (doc 07 §2.2).
        /// Retorna false se inexistente, incompleta ou já reclamada.
        /// </summary>
        public bool ClaimMission(string id)
        {
            if (SaveSystem.Instance == null || EconomySystem.Instance == null) return false;
            EnsureDailyMissions();
            MissionProgress m = Find(id);
            if (m == null || m.claimed || !MissionMath.IsComplete(m.progress, m.target)) return false;

            long coins = MissionMath.MissionCoins(WorldMult());
            EconomySystem.Instance.Earn(CurrencyType.Coin, coins, "mission_" + id);
            EconomySystem.Instance.Earn(CurrencyType.Gem, MissionMath.MissionRewardGems, "mission_" + id);
            m.claimed = true;

            // Bônus por completar AS TRÊS (doc 07 §2.2): +10 gemas, uma vez (quando a última fecha).
            if (AllClaimed())
                EconomySystem.Instance.Earn(CurrencyType.Gem, MissionMath.AllMissionsBonusGems, "mission_all_bonus");

            SaveSystem.Instance.MarkDirty();
            OnMissionsChanged?.Invoke();
            return true;
        }

        // ==================================================================
        // CONTRATO DE API — login diário com streak (agente de telas)
        // ==================================================================

        /// <summary>
        /// Estado do login de HOJE (contrato de telas): o dia do ciclo (streak que VIRARIA hoje),
        /// as moedas/gemas correspondentes e se já foi reclamado neste dia UTC.
        /// </summary>
        public LoginReward TodayLogin()
        {
            if (SaveSystem.Instance == null) return new LoginReward(1, MissionMath.LoginRewardCoins(1), MissionMath.LoginRewardGems(1), false);
            SaveData d = SaveSystem.Instance.Data;
            long now = NowUnix();
            bool claimedToday = !MissionMath.IsNewUtcDay(d.lastLoginRewardUnix, now) && d.lastLoginRewardUnix > 0;

            // Se já reclamou hoje, o streak vigente é d.loginStreak; senão, o que ele VIRARIA ao reclamar.
            int streakIfClaimed = claimedToday
                ? Math.Max(1, d.loginStreak)
                : MissionMath.NextLoginStreak(d.loginStreak, d.lastLoginRewardUnix, now);

            return new LoginReward(
                day: MissionMath.LoginCycleDay(streakIfClaimed) + 1,   // 1..7 para a UI
                coins: MissionMath.LoginRewardCoins(streakIfClaimed),
                gems: MissionMath.LoginRewardGems(streakIfClaimed),
                claimedToday: claimedToday);
        }

        /// <summary>
        /// Reclama o login do dia (contrato de telas): avança o streak (Domain), credita a
        /// recompensa do dia do ciclo e marca a data. 1×/dia UTC; já reclamado retorna false.
        /// </summary>
        public bool ClaimLogin()
        {
            if (SaveSystem.Instance == null || EconomySystem.Instance == null) return false;
            SaveData d = SaveSystem.Instance.Data;
            long now = NowUnix();
            if (!MissionMath.IsNewUtcDay(d.lastLoginRewardUnix, now)) return false;   // já reclamou hoje

            int newStreak = MissionMath.NextLoginStreak(d.loginStreak, d.lastLoginRewardUnix, now);
            d.loginStreak = newStreak;
            d.lastLoginRewardUnix = now;

            long coins = MissionMath.LoginRewardCoins(newStreak);
            int gems = MissionMath.LoginRewardGems(newStreak);
            if (coins > 0) EconomySystem.Instance.Earn(CurrencyType.Coin, coins, "login_day_" + newStreak);
            if (gems > 0) EconomySystem.Instance.Earn(CurrencyType.Gem, gems, "login_day_" + newStreak);

            SaveSystem.Instance.MarkDirty();
            OnLoginClaimed?.Invoke();
            return true;
        }

        // ---- Internos ----

        private MissionProgress Find(string id)
        {
            if (SaveSystem.Instance == null || SaveSystem.Instance.Data.dailyMissions == null) return null;
            return SaveSystem.Instance.Data.dailyMissions.Find(x => x.missionId == id);
        }

        private bool AllClaimed()
        {
            List<MissionProgress> ms = SaveSystem.Instance.Data.dailyMissions;
            if (ms == null || ms.Count == 0) return false;
            for (int i = 0; i < ms.Count; i++)
                if (!ms[i].claimed) return false;
            return true;
        }

        private float WorldMult()
        {
            // Mb por mundo (doc 07 §2): no MVP usa 1.0; RC pode prover por mundo no futuro.
            float mb = _remoteConfig != null ? _remoteConfig.GetFloat(WorldMultKey, 1f) : 1f;
            return mb < 1f ? 1f : mb;
        }

        private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>Visão de VALOR de uma missão diária para a UI (contrato de telas).</summary>
    public readonly struct DailyMission
    {
        public readonly string id;
        public readonly string descKey;
        public readonly int progress;
        public readonly int target;
        public readonly bool claimed;
        public readonly long rewardCoins;
        public readonly int rewardGems;

        public DailyMission(string id, string descKey, int progress, int target, bool claimed,
                            long rewardCoins, int rewardGems)
        {
            this.id = id;
            this.descKey = descKey;
            this.progress = progress;
            this.target = target;
            this.claimed = claimed;
            this.rewardCoins = rewardCoins;
            this.rewardGems = rewardGems;
        }

        /// <summary>True quando o progresso atingiu o alvo (atalho para a tela habilitar o resgate).</summary>
        public bool IsComplete => target > 0 && progress >= target;
    }

    /// <summary>Visão de VALOR do login diário do dia (contrato de telas).</summary>
    public readonly struct LoginReward
    {
        public readonly int day;            // 1..7 no ciclo
        public readonly long coins;
        public readonly int gems;
        public readonly bool claimedToday;

        public LoginReward(int day, long coins, int gems, bool claimedToday)
        {
            this.day = day;
            this.coins = coins;
            this.gems = gems;
            this.claimedToday = claimedToday;
        }
    }
}
