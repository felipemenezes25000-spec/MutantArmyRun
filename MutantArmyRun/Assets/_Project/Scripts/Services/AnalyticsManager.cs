using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Fila local + açúcar tipado para a taxonomia COMPLETA do doc 11 (23 eventos
    /// obrigatórios do BRIEF + 11 proprietários) + 9 sensores da missão Nota 10
    /// (boss_weakness_hit, boss_resisted_hit, boss_special_warning, boss_fight_start,
    /// boss_defeated via bus, rare_boss_announced, combo_earned, enemy_killed,
    /// enemy_wave_cleared, gate_best_choice — todos fiados por evento no Init).
    /// O transporte mora no IAnalyticsProvider
    /// (Null no MVP — console em DEV; Firebase depois). Convenções do doc 11 §3:
    /// snake_case, booleans como int 0/1, e os parâmetros globais (session_id, level,
    /// world, app_version, seconds_in_session) anexados automaticamente a TODO evento.
    /// Eventos disparados antes do Init ficam em fila; consentimento negado descarta.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour, IInitializable
    {
        public static AnalyticsManager Instance { get; private set; }

        private readonly Queue<QueuedEvent> _preInitQueue = new Queue<QueuedEvent>();
        private IAnalyticsProvider _provider;
        private bool _ready;
        private bool _consentDenied;
        private string _sessionId = "";
        private float _sessionStartRealtime;
        private int _contextLevel;
        private int _contextWorld;

        private struct QueuedEvent
        {
            public string name;
            public Dictionary<string, object> parameters;
        }

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable — o provider
        /// já foi inicializado pelo bootstrap (passo 3); aqui só se resolve e usa.</summary>
        public void Init()
        {
            bool online = Application.internetReachability != NetworkReachability.NotReachable;
            string consent = GameBootstrap.Current != null ? GameBootstrap.Current.ConsentStatus : "unknown";
            Init(ResolveProvider(), online, consent);
        }

        /// <summary>Overload do doc 12 §4.9 com provider explícito — testes injetam um fake.</summary>
        public void Init(IAnalyticsProvider provider, bool online, string consentStatus)
        {
            Instance = this;
            _provider = provider;
            _sessionId = Guid.NewGuid().ToString("N");
            _sessionStartRealtime = Time.realtimeSinceStartup;
            _consentDenied = consentStatus == "denied";        // UMP: analytics consent-gated
            _ready = !_consentDenied && _provider != null;

            // Contratos doc 12 §4.3/§4.9: gate_selected nasce do bus (Gameplay nunca chama
            // Services direto) e level_start do GameManager (Services enxerga Core, §2.3);
            // -= antes de += para Init repetido não duplicar a inscrição.
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnGateConsumed += HandleGateConsumed;
            // Fiação da missão Nota 10 (CONTRACT §5): tudo nasce no bus — boss elemental,
            // combos, especiais, morte/raro de boss, inimigos de pista e fim de fase rico.
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnBossElementalHit += HandleBossElementalHit;
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnComboEarned += HandleComboEarned;
            GameEvents.OnBossSpecialWarning -= HandleBossSpecialWarning;
            GameEvents.OnBossSpecialWarning += HandleBossSpecialWarning;
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnBossDied += HandleBossDied;
            GameEvents.OnRareBossAnnounced -= HandleRareBossAnnounced;
            GameEvents.OnRareBossAnnounced += HandleRareBossAnnounced;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            GameEvents.OnTrackEnemyKilled += HandleTrackEnemyKilled;
            GameEvents.OnEnemyWaveCleared -= HandleEnemyWaveCleared;
            GameEvents.OnEnemyWaveCleared += HandleEnemyWaveCleared;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            // gate_best_choice: o veredito é cosmético (JuiceEvents), mas o agregado por
            // jogador é sensor do funil de aprendizado — denominador é gate_selected.
            JuiceEvents.OnGoodGateChoice -= HandleGoodGateChoice;
            JuiceEvents.OnGoodGateChoice += HandleGoodGateChoice;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelStarted -= LogLevelStart;
                GameManager.Instance.LevelStarted += LogLevelStart;
                // boss_fight_start: a entrada em BossFight é o único sinal disponível daqui
                // (o boss_start rico exige contexto do Gameplay — call site é da Onda 4+).
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateEntered += HandleStateEntered;
            }

            if (_consentDenied) _preInitQueue.Clear();         // negado: descarta, não acumula
            while (_ready && _preInitQueue.Count > 0)
            {
                QueuedEvent e = _preInitQueue.Dequeue();
                Send(e.name, e.parameters);
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;   // bus estático sobrevive a cenas (doc 12 §3.2)
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnBossSpecialWarning -= HandleBossSpecialWarning;
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnRareBossAnnounced -= HandleRareBossAnnounced;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            GameEvents.OnEnemyWaveCleared -= HandleEnemyWaveCleared;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            JuiceEvents.OnGoodGateChoice -= HandleGoodGateChoice;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelStarted -= LogLevelStart;
                GameManager.Instance.StateEntered -= HandleStateEntered;
            }
        }

        /// <summary>gate_selected(chosen, rejected) — mede rota ótima vs armadilha (doc 11 §4.3).</summary>
        private void HandleGateConsumed(GateResult r)
            => LogGateSelected(r.gate != null ? r.gate.gateId : "",
                               r.rejected != null ? r.rejected.gateId : "");

        // ---- Handlers da missão Nota 10 (payloads do bus → açúcar tipado abaixo) ----

        // Weakness → boss_weakness_hit; Resisted/Immune → boss_resisted_hit (immune é o caso
        // extremo do mesmo aprendizado); Neutral não vira evento — seria ruído de hit comum.
        private void HandleBossElementalHit(BossElementalHit h)
        {
            if (h.relation == ElementRelation.Weakness)
                LogBossWeaknessHit(Snake(h.element.ToString()), Snake(h.relation.ToString()), h.damage);
            else if (h.relation == ElementRelation.Resisted || h.relation == ElementRelation.Immune)
                LogBossResistedHit(Snake(h.element.ToString()), Snake(h.relation.ToString()), h.damage);
        }

        private void HandleComboEarned(ComboEarned c)
            => LogComboEarned(Snake(c.kind.ToString()), c.bonusCoins);

        private void HandleBossSpecialWarning(BossSpecialTelegraph t)
            => LogBossSpecialWarning(t.bossId, t.seconds);

        // boss_defeated com o payload do bus (boss_id/was_rare/fight_seconds); o overload
        // rico (units_lost/overkill_pct) segue disponível para um call site futuro com
        // contexto completo do Gameplay.
        private void HandleBossDied(BossDied d)
            => LogBossDefeated(d.bossId, d.wasRare, d.fightSeconds);

        private void HandleRareBossAnnounced(RareBossAnnounce a)
            => LogRareBossAnnounced(a.bossId, a.hpMultiplier, a.rewardMultiplier);

        private void HandleTrackEnemyKilled(TrackEnemyKilled k)
            => LogEnemyKilled(Snake(k.kind.ToString()), k.coins);

        private void HandleEnemyWaveCleared(EnemyWaveCleared w)
            => LogEnemyWaveCleared(w.enemiesKilled);

        // level_complete/level_fail nascem do LevelResult do fim de fase (doc 12 §4.1):
        // combos e fail_reason ricos são preenchidos pelo GameManager nesta onda (W3-C).
        private void HandleLevelFinished(LevelResult r)
        {
            if (r.won) LogLevelComplete(r);
            else LogLevelFail(r);
        }

        private void HandleGoodGateChoice(Vector3 worldPosition)
            => LogGateBestChoice();

        private void HandleStateEntered(GameState state)
        {
            if (state == GameState.BossFight) LogBossFightStart();
        }

        /// <summary>Parâmetros globais "level"/"world" — atualizados pelo fluxo de fase.</summary>
        public void SetLevelContext(int level, int world)
        {
            _contextLevel = level;
            _contextWorld = world;
        }

        public void Log(string name, Dictionary<string, object> p = null)
        {
            if (_consentDenied) return;
            if (_ready) Send(name, p);
            else _preInitQueue.Enqueue(new QueuedEvent { name = name, parameters = p });
        }

        private void Send(string name, Dictionary<string, object> p)
        {
            Dictionary<string, object> full = p ?? new Dictionary<string, object>();
            // Globais do doc 11 §3 — nunca sobrescrevem um valor explícito do evento.
            full["session_id"] = _sessionId;
            if (!full.ContainsKey("level")) full["level"] = _contextLevel;
            if (!full.ContainsKey("world")) full["world"] = _contextWorld;
            full["app_version"] = Application.version;
            full["seconds_in_session"] = (int)(Time.realtimeSinceStartup - _sessionStartRealtime);
            _provider.Log(name, full);
        }

        /// <summary>Booleans são logados como int 0/1 (limitação do SDK — doc 11 §3).</summary>
        private static int B(bool value) => value ? 1 : 0;

        /// <summary>
        /// PascalCase → snake_case para valores de enum na taxonomia (doc 11 §3):
        /// "PerfectGate" → "perfect_gate". ATENÇÃO: renomear um membro de enum muda o valor
        /// logado — os enums da missão são append-only (CONTRACT §1.3), então é estável.
        /// </summary>
        private static string Snake(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return "";
            var sb = new System.Text.StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static IAnalyticsProvider ResolveProvider()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IAnalyticsProvider>(true)
                : null;
        }

        // ================= FTUE e fases (doc 11 §4.1) =================

        public void LogTutorialStart(float timeToStartSec)
            => Log("tutorial_start", new Dictionary<string, object>
            {
                ["time_to_start_sec"] = timeToStartSec,
            });

        public void LogTutorialComplete(float durationSec, int gatesTaken)
            => Log("tutorial_complete", new Dictionary<string, object>
            {
                ["duration_sec"] = durationSec,
                ["gates_taken"] = gatesTaken,
            });

        /// <summary>Açúcar mínimo do doc 12 §4.9 — assinatura é contrato.</summary>
        public void LogLevelStart(int lvl)
            => Log("level_start", new Dictionary<string, object> { ["level"] = lvl });

        public void LogLevelStart(int lvl, int attempt, int armySizeStart, int supplyCap,
                                  int upgradePowerScore, string source)
            => Log("level_start", new Dictionary<string, object>
            {
                ["level"] = lvl,
                ["attempt"] = attempt,
                ["army_size_start"] = armySizeStart,
                ["supply_cap"] = supplyCap,
                ["upgrade_power_score"] = upgradePowerScore,
                ["source"] = source,
            });

        public void LogLevelComplete(int attempt, float durationSec, float runDurationSec,
                                     float bossDurationSec, int armySizeAtBoss, int unitsSurvived,
                                     int coinsEarned, int gatesTaken, int gatesMissed,
                                     string mutationsActive, int supplyOverflowTotal)
            => Log("level_complete", new Dictionary<string, object>
            {
                ["attempt"] = attempt,
                ["duration_sec"] = durationSec,
                ["run_duration_sec"] = runDurationSec,
                ["boss_duration_sec"] = bossDurationSec,
                ["army_size_at_boss"] = armySizeAtBoss,
                ["units_survived"] = unitsSurvived,
                ["coins_earned"] = coinsEarned,
                ["gates_taken"] = gatesTaken,
                ["gates_missed"] = gatesMissed,
                ["mutations_active"] = mutationsActive,
                ["supply_overflow_total"] = supplyOverflowTotal,
            });

        public void LogLevelFail(int attempt, string failReason, float failProgressPct,
                                 int armySizeMax, float bossHpPctRemaining)
            => Log("level_fail", new Dictionary<string, object>
            {
                ["attempt"] = attempt,
                ["fail_reason"] = failReason,           // boss / obstacle / bad_gate / trap_zone
                ["fail_progress_pct"] = failProgressPct,
                ["army_size_max"] = armySizeMax,
                ["boss_hp_pct_remaining"] = bossHpPctRemaining,   // −1 se não chegou ao boss
            });

        // ================= Boss (doc 11 §4.2) =================

        public void LogBossStart(string bossId, string bossElement, string bossWeakness,
                                 int armySize, int supplyUsed, string armyMainElement,
                                 bool hasWeaknessElement, string mutationsActive)
            => Log("boss_start", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["boss_element"] = bossElement,
                ["boss_weakness"] = bossWeakness,
                ["army_size"] = armySize,
                ["supply_used"] = supplyUsed,
                ["army_main_element"] = armyMainElement,
                ["has_weakness_element"] = B(hasWeaknessElement),
                ["mutations_active"] = mutationsActive,
            });

        public void LogBossDefeated(string bossId, float fightDurationSec, int unitsLost,
                                    bool usedRevive, bool weaknessExploited, float overkillPct)
            => Log("boss_defeated", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["fight_duration_sec"] = fightDurationSec,
                ["units_lost"] = unitsLost,
                ["used_revive"] = B(usedRevive),
                ["weakness_exploited"] = B(weaknessExploited),
                ["overkill_pct"] = overkillPct,
            });

        public void LogBossFailed(string bossId, float fightDurationSec, float bossHpPctRemaining,
                                  bool usedRevive, bool weaknessExploited)
            => Log("boss_failed", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["fight_duration_sec"] = fightDurationSec,
                ["boss_hp_pct_remaining"] = bossHpPctRemaining,
                ["used_revive"] = B(usedRevive),
                ["weakness_exploited"] = B(weaknessExploited),
            });

        // ================= Portais (doc 11 §4.3 — coração do jogo) =================

        /// <summary>Açúcar mínimo do doc 12 §4.9 — assinatura é contrato (mede rota ótima vs armadilha).</summary>
        public void LogGateSelected(string chosen, string rejected)
            => Log("gate_selected", new Dictionary<string, object>
            {
                ["gate"] = chosen,
                ["rejected"] = rejected,
            });

        public void LogGateSelected(int gatePairIndex, string gateType, string gateValue,
                                    string gateSide, string alternativeType, string alternativeValue,
                                    int armySizeBefore, int armySizeAfter, int supplyBefore,
                                    int supplyAfter, bool wasOptimal, string riskOutcome)
            => Log("gate_selected", new Dictionary<string, object>
            {
                ["gate_pair_index"] = gatePairIndex,
                ["gate_type"] = gateType,
                ["gate_value"] = gateValue,
                ["gate_side"] = gateSide,               // left / right
                ["alternative_type"] = alternativeType,
                ["alternative_value"] = alternativeValue,
                ["army_size_before"] = armySizeBefore,
                ["army_size_after"] = armySizeAfter,
                ["supply_before"] = supplyBefore,
                ["supply_after"] = supplyAfter,
                ["was_optimal"] = B(wasOptimal),
                ["risk_outcome"] = riskOutcome,         // win / lose / na
            });

        public void LogGateMissed(int gatePairIndex, string leftType, string leftValue,
                                  string rightType, string rightValue, int armySize)
            => Log("gate_missed", new Dictionary<string, object>
            {
                ["gate_pair_index"] = gatePairIndex,
                ["left_type"] = leftType,
                ["left_value"] = leftValue,
                ["right_type"] = rightType,
                ["right_value"] = rightValue,
                ["army_size"] = armySize,
            });

        // ================= Coleção e economia (doc 11 §4.4) =================

        public void LogUnitUnlocked(string unitId, string rarity, string source)
            => Log("unit_unlocked", new Dictionary<string, object>
            {
                ["unit_id"] = unitId,
                ["rarity"] = rarity,
                ["source"] = source,                    // level_reward / chest / shop / season_pass / event
            });

        public void LogUnitUpgraded(string unitId, string rarity, int fromLevel, int toLevel,
                                    int shardsSpent, int coinsSpent, int shardsRemaining)
            => Log("unit_upgraded", new Dictionary<string, object>
            {
                ["unit_id"] = unitId,
                ["rarity"] = rarity,
                ["from_level"] = fromLevel,
                ["to_level"] = toLevel,
                ["shards_spent"] = shardsSpent,
                ["coins_spent"] = coinsSpent,
                ["shards_remaining"] = shardsRemaining,
            });

        public void LogChestOpened(string chestType, string source, int gemsSpent, int coinsGranted,
                                   int gemsGranted, int shardsGranted, string bestRarity)
            => Log("chest_opened", new Dictionary<string, object>
            {
                ["chest_type"] = chestType,
                ["source"] = source,
                ["gems_spent"] = gemsSpent,
                ["coins_granted"] = coinsGranted,
                ["gems_granted"] = gemsGranted,
                ["shards_granted"] = shardsGranted,
                ["best_rarity"] = bestRarity,
            });

        // ================= Ads (doc 11 §4.5) =================

        /// <summary>Açúcar mínimo do doc 12 §4.9 — assinatura é contrato.</summary>
        public void LogRewardedShown(string placement)
            => Log("rewarded_ad_shown", new Dictionary<string, object> { ["placement"] = placement });

        public void LogRewardedShown(string placement, string adNetwork, float ecpmUsd)
            => Log("rewarded_ad_shown", new Dictionary<string, object>
            {
                ["placement"] = placement,
                ["ad_network"] = adNetwork,
                ["ecpm_usd"] = ecpmUsd,
            });

        public void LogRewardedCompleted(string placement, string rewardGranted = "",
                                         string adNetwork = "none", float ecpmUsd = 0f)
            => Log("rewarded_ad_completed", new Dictionary<string, object>
            {
                ["placement"] = placement,
                ["reward_granted"] = rewardGranted,     // ex.: "coins_x2:240"
                ["ad_network"] = adNetwork,
                ["ecpm_usd"] = ecpmUsd,
            });

        /// <summary>Açúcar mínimo do doc 12 §4.9 — assinatura é contrato.</summary>
        public void LogInterstitialShown()
            => Log("interstitial_shown");

        public void LogInterstitialShown(int levelsSinceLast, int sessionInterstitialCount,
                                         string adNetwork = "none", float ecpmUsd = 0f)
            => Log("interstitial_shown", new Dictionary<string, object>
            {
                ["levels_since_last"] = levelsSinceLast,
                ["session_interstitial_count"] = sessionInterstitialCount,
                ["ad_network"] = adNetwork,
                ["ecpm_usd"] = ecpmUsd,
            });

        /// <summary>Denominador real do funil de rewarded (doc 11 §5/§6.2).</summary>
        public void LogRewardedOfferShown(string placement)
            => Log("rewarded_offer_shown", new Dictionary<string, object> { ["placement"] = placement });

        // ================= IAP e passe (doc 11 §4.6) =================

        public void LogPurchaseStarted(string productId, float priceUsd, string sourceScreen)
            => Log("purchase_started", new Dictionary<string, object>
            {
                ["product_id"] = productId,
                ["price_usd"] = priceUsd,
                ["source_screen"] = sourceScreen,       // shop / offer_popup / defeat_screen / pass_screen
            });

        public void LogPurchaseCompleted(string productId, float priceUsd, string currencyLocal,
                                         bool isFirstPurchase, float hoursSinceInstall)
            => Log("purchase_completed", new Dictionary<string, object>
            {
                ["product_id"] = productId,
                ["price_usd"] = priceUsd,
                ["currency_local"] = currencyLocal,
                ["is_first_purchase"] = B(isFirstPurchase),
                ["hours_since_install"] = hoursSinceInstall,
            });

        public void LogSeasonPassOpened(string seasonId, int passTierCurrent, bool isPurchased)
            => Log("season_pass_opened", new Dictionary<string, object>
            {
                ["season_id"] = seasonId,
                ["pass_tier_current"] = passTierCurrent,
                ["is_purchased"] = B(isPurchased),
            });

        public void LogSeasonPassPurchased(string seasonId, float priceUsd, int daysIntoSeason,
                                           int passTierCurrent)
            => Log("season_pass_purchased", new Dictionary<string, object>
            {
                ["season_id"] = seasonId,
                ["price_usd"] = priceUsd,
                ["days_into_season"] = daysIntoSeason,
                ["pass_tier_current"] = passTierCurrent,
            });

        // ================= Retenção explícita (doc 11 §4.7) =================

        public void LogDay1Retention(int levelsCompletedD0, string payerType)
            => Log("day_1_retention", new Dictionary<string, object>
            {
                ["levels_completed_d0"] = levelsCompletedD0,
                ["payer_type"] = payerType,             // none / minnow / dolphin / whale
            });

        public void LogDay3Retention(int highestLevel)
            => Log("day_3_retention", new Dictionary<string, object>
            {
                ["highest_level"] = highestLevel,
            });

        public void LogDay7Retention(int highestLevel, int totalRewardedCompleted)
            => Log("day_7_retention", new Dictionary<string, object>
            {
                ["highest_level"] = highestLevel,
                ["total_rewarded_completed"] = totalRewardedCompleted,
            });

        // ================= Eventos proprietários (doc 11 §5 — sensores dos diferenciais) =================

        public void LogBossScoutViewed(string bossId, string bossWeakness, string viewType,
                                       int reminderCountLevel)
            => Log("boss_scout_viewed", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["boss_weakness"] = bossWeakness,
                ["view_type"] = viewType,               // pre_level / reminder
                ["reminder_count_level"] = reminderCountLevel,
            });

        public void LogSupplyOverflow(int unitsConverted, int coinsGranted, int supplyCap,
                                      string triggerGateType)
            => Log("supply_overflow", new Dictionary<string, object>
            {
                ["units_converted"] = unitsConverted,
                ["coins_granted"] = coinsGranted,
                ["supply_cap"] = supplyCap,
                ["trigger_gate_type"] = triggerGateType,
            });

        public void LogMutationApplied(string mutationId, int slotIndex, string mutationsActive)
            => Log("mutation_applied", new Dictionary<string, object>
            {
                ["mutation_id"] = mutationId,
                ["slot_index"] = slotIndex,             // 1–3 (CANON §3.3)
                ["mutations_active"] = mutationsActive,
            });

        public void LogMutationReplaced(string newMutationId, string replacedMutationId,
                                        bool wasVoluntary)
            => Log("mutation_replaced", new Dictionary<string, object>
            {
                ["new_mutation_id"] = newMutationId,
                ["replaced_mutation_id"] = replacedMutationId,
                ["was_voluntary"] = B(wasVoluntary),
            });

        public void LogReviveOffered(string bossId, float bossHpPctRemaining, int attempt)
            => Log("revive_offered", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["boss_hp_pct_remaining"] = bossHpPctRemaining,
                ["attempt"] = attempt,
            });

        public void LogReviveAccepted(string bossId, float bossHpPctRemaining, bool wonAfterRevive)
            => Log("revive_accepted", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["boss_hp_pct_remaining"] = bossHpPctRemaining,
                ["won_after_revive"] = B(wonAfterRevive),
            });

        public void LogNearWin(string bossId, float bossHpPctRemaining, bool reviveWasOffered,
                               bool reviveWasAccepted)
            => Log("near_win", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["boss_hp_pct_remaining"] = bossHpPctRemaining,   // < 0.10 por definição (doc 11 §5)
                ["revive_was_offered"] = B(reviveWasOffered),
                ["revive_was_accepted"] = B(reviveWasAccepted),
            });

        public void LogTutorialStep(string stepId, int stepIndex, float durationSec, bool wasSkipped)
            => Log("tutorial_step", new Dictionary<string, object>
            {
                ["step_id"] = stepId,                   // tap_to_move / first_gate / first_boss...
                ["step_index"] = stepIndex,
                ["duration_sec"] = durationSec,
                ["was_skipped"] = B(wasSkipped),
            });

        public void LogScreenView(string screenName, string source)
            => Log("screen_view", new Dictionary<string, object>
            {
                ["screen_name"] = screenName,           // home / shop / units / upgrades / pass / settings
                ["source"] = source,
            });

        public void LogOfferPopup(string offerId, string trigger, float hoursSinceInstall)
            => Log("offer_popup", new Dictionary<string, object>
            {
                ["offer_id"] = offerId,                 // starter_offer_299
                ["trigger"] = trigger,                  // first_48h / first_defeat
                ["hours_since_install"] = hoursSinceInstall,
            });

        // ================= Missão Nota 10 (sensores de boss/combo/inimigos — fiados no Init) =================

        /// <summary>Hit na FRAQUEZA do boss (rate-limited ≥0,5 s na origem — amostra, não censo).</summary>
        public void LogBossWeaknessHit(string element, string relation, float damage)
            => Log("boss_weakness_hit", new Dictionary<string, object>
            {
                ["element"] = element,                  // fire / ice / lightning...
                ["relation"] = relation,                // weakness
                ["damage"] = damage,
            });

        /// <summary>Hit RESISTIDO/IMUNE — sensor do aprendizado elemental (exército errado).</summary>
        public void LogBossResistedHit(string element, string relation, float damage)
            => Log("boss_resisted_hit", new Dictionary<string, object>
            {
                ["element"] = element,
                ["relation"] = relation,                // resisted / immune
                ["damage"] = damage,
            });

        /// <summary>Telegraph do ataque especial exibido (janela de leitura, CANON §6).</summary>
        public void LogBossSpecialWarning(string bossId, float telegraphSeconds)
            => Log("boss_special_warning", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["telegraph_seconds"] = telegraphSeconds,
            });

        /// <summary>boss_defeated com o payload do bus (OnBossDied) — overload do rico acima.</summary>
        public void LogBossDefeated(string bossId, bool wasRare, float fightSeconds)
            => Log("boss_defeated", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["was_rare"] = B(wasRare),
                ["fight_seconds"] = fightSeconds,
            });

        /// <summary>Entrada na arena do boss — denominador do funil boss_defeated/level_fail.</summary>
        public void LogBossFightStart()
            => Log("boss_fight_start");

        /// <summary>Variante rara anunciada no BossScout (HP ×1.5, recompensa ×3 — missão §4.2).</summary>
        public void LogRareBossAnnounced(string bossId, float hpMult, float rewardMult)
            => Log("rare_boss_announced", new Dictionary<string, object>
            {
                ["boss_id"] = bossId,
                ["hp_mult"] = hpMult,
                ["reward_mult"] = rewardMult,
            });

        /// <summary>Combo conquistado (ComboMath) — mede quais conquistas o jogador alcança.</summary>
        public void LogComboEarned(string comboKind, int bonusCoins)
            => Log("combo_earned", new Dictionary<string, object>
            {
                ["combo_kind"] = comboKind,             // perfect_gate / weakness_hit / boss_breaker...
                ["bonus_coins"] = bonusCoins,
            });

        /// <summary>Inimigo de PISTA morto (não é unidade do exército — doc 12 §4.11).</summary>
        public void LogEnemyKilled(string enemyKind, int coins)
            => Log("enemy_killed", new Dictionary<string, object>
            {
                ["enemy_kind"] = enemyKind,             // weak_horde / tank / ranged / healer
                ["coins"] = coins,
            });

        public void LogEnemyWaveCleared(int enemiesKilled)
            => Log("enemy_wave_cleared", new Dictionary<string, object>
            {
                ["enemies_killed"] = enemiesKilled,
            });

        /// <summary>Rota ótima escolhida (veredito do GateManager) — numerador do was_optimal.</summary>
        public void LogGateBestChoice()
            => Log("gate_best_choice");

        /// <summary>
        /// level_complete a partir do LevelResult do bus — overload do rico acima (que exige
        /// contexto de Gameplay sem call site ainda). Globais level/world anexados no Send.
        /// </summary>
        public void LogLevelComplete(LevelResult r)
            => Log("level_complete", new Dictionary<string, object>
            {
                ["level"] = r.levelIndex,
                ["duration_sec"] = r.durationSeconds,
                ["units_survived"] = r.survivors,
                ["coins_earned"] = r.coinsAwarded,
                ["xp_earned"] = r.xpAwarded,
                ["combo_count"] = r.comboCount,
                ["combo_bonus_coins"] = r.comboBonusCoins,
            });

        /// <summary>level_fail com o fail_reason RICO (FailReasonResolver) em snake_case.</summary>
        public void LogLevelFail(LevelResult r)
            => Log("level_fail", new Dictionary<string, object>
            {
                ["level"] = r.levelIndex,
                ["fail_reason"] = Snake(r.failReason.ToString()),   // wrong_element / hit_by_lava...
                ["duration_sec"] = r.durationSeconds,
                ["xp_earned"] = r.xpAwarded,
            });
    }
}
