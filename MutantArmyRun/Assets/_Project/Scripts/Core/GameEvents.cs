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

        public static void RaiseGateConsumed(GateResult r) => OnGateConsumed?.Invoke(r);
        public static void RaiseCrowdChanged(int count, int supplyUsed) => OnCrowdChanged?.Invoke(count, supplyUsed);
        public static void RaiseSupplyOverflow(SupplyOverflow o) => OnSupplyOverflow?.Invoke(o);
        public static void RaiseMutationGained(MutationConfigSO m) => OnMutationGained?.Invoke(m);
        public static void RaiseUnitDied(UnitDeath d) => OnUnitDied?.Invoke(d);
        public static void RaiseBossPhaseChanged(BossPhase p) => OnBossPhaseChanged?.Invoke(p);
        public static void RaiseLevelFinished(LevelResult r) => OnLevelFinished?.Invoke(r);
        public static void RaiseCurrencyChanged(CurrencyChange c) => OnCurrencyChanged?.Invoke(c);
    }
}
