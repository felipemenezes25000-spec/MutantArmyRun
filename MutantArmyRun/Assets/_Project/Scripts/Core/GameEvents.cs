using System;

namespace MutantArmy.Core
{
    /// <summary>
    /// Bus central de eventos de ESTADO DE JOGO (doc 12 §3.2): C# events tipados com payload
    /// em struct — zero alocação por Raise, rastreável por "Find References", ordem
    /// determinística por assinatura. Fluxo de telas e reação cosmética usam SO events;
    /// dados usam este bus. Regras de uso:
    /// - Sempre limpar inscrições em OnDisable (bus estático sobrevive a cenas).
    /// - Listeners síncronos de DADOS (economia, save, analytics) rodam no mesmo frame do
    ///   Raise; troca de tela é deferida para o frame seguinte ("dados prontos → tela mostra").
    /// - UI atualiza por evento, nunca por polling em Update().
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GateResult> OnGateConsumed;          // 1 por exército por par (§4.3)
        public static event Action<int, int> OnCrowdChanged;            // (count, supplyUsed)
        public static event Action<SupplyOverflow> OnSupplyOverflow;    // excedente → moedas (fanfarra)
        public static event Action<MutationConfigSO> OnMutationGained;
        public static event Action<UnitDeath> OnUnitDied;               // ponto de extensão (§4.2): VFX/analytics
        public static event Action<BossPhase> OnBossPhaseChanged;
        public static event Action<LevelResult> OnLevelFinished;        // vitória ou derrota + stats
        public static event Action<CurrencyChange> OnCurrencyChanged;
        public static event Action<float> OnRunProgress;                // 0..1 da pista — Raise SÓ em mudança (≥0,5%), nunca polling da UI

        // ---- Eventos da missão Nota 10 (boss/combos/inimigos de pista) ----
        public static event Action<BossElementalHit> OnBossElementalHit;       // rate-limited NA ORIGEM (≥0,5 s entre Raises)
        public static event Action<ComboEarned> OnComboEarned;
        public static event Action<BossSpecialTelegraph> OnBossSpecialWarning; // janela de leitura ANTES do golpe (CANON §6)
        public static event Action<BossDied> OnBossDied;                       // álbum + morte cinematográfica
        public static event Action<TrackEnemyKilled> OnTrackEnemyKilled;       // inimigo de PISTA (não é UnitDeath do exército)
        public static event Action<EnemyWaveCleared> OnEnemyWaveCleared;
        // Derrota justa: Gameplay resolve a razão (FailReasonResolver) e publica ANTES da
        // transição p/ Defeat — o GameManager assina o próprio bus p/ preencher LevelResult
        // (Core não enxerga Gameplay, doc 12 §2.3).
        public static event Action<MutantArmy.Domain.FailReason> OnFailReasonResolved;
        public static event Action<RareBossAnnounce> OnRareBossAnnounced;      // rolado no BossScout, antes da fase
        // HP normalizado 0..1 do boss p/ a barra do HUD — Raise SÓ em mudança ≥0,5% (padrão
        // OnRunProgress, nunca polling); 1.0 no BeginFight, 0 na morte.
        public static event Action<float> OnBossHpChanged;

        public static void RaiseGateConsumed(GateResult r) => OnGateConsumed?.Invoke(r);
        public static void RaiseCrowdChanged(int count, int supplyUsed) => OnCrowdChanged?.Invoke(count, supplyUsed);
        public static void RaiseSupplyOverflow(SupplyOverflow o) => OnSupplyOverflow?.Invoke(o);
        public static void RaiseMutationGained(MutationConfigSO m) => OnMutationGained?.Invoke(m);
        public static void RaiseUnitDied(UnitDeath d) => OnUnitDied?.Invoke(d);
        public static void RaiseBossPhaseChanged(BossPhase p) => OnBossPhaseChanged?.Invoke(p);
        public static void RaiseLevelFinished(LevelResult r) => OnLevelFinished?.Invoke(r);
        public static void RaiseCurrencyChanged(CurrencyChange c) => OnCurrencyChanged?.Invoke(c);
        public static void RaiseRunProgress(float progress01) => OnRunProgress?.Invoke(progress01);
        public static void RaiseBossElementalHit(BossElementalHit h) => OnBossElementalHit?.Invoke(h);
        public static void RaiseComboEarned(ComboEarned c) => OnComboEarned?.Invoke(c);
        public static void RaiseBossSpecialWarning(BossSpecialTelegraph t) => OnBossSpecialWarning?.Invoke(t);
        public static void RaiseBossDied(BossDied d) => OnBossDied?.Invoke(d);
        public static void RaiseTrackEnemyKilled(TrackEnemyKilled k) => OnTrackEnemyKilled?.Invoke(k);
        public static void RaiseEnemyWaveCleared(EnemyWaveCleared w) => OnEnemyWaveCleared?.Invoke(w);
        public static void RaiseFailReasonResolved(MutantArmy.Domain.FailReason r) => OnFailReasonResolved?.Invoke(r);
        public static void RaiseRareBossAnnounced(RareBossAnnounce a) => OnRareBossAnnounced?.Invoke(a);
        public static void RaiseBossHpChanged(float normalizedHp) => OnBossHpChanged?.Invoke(normalizedHp);
    }
}
