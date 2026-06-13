using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Loop de dopamina do fim de corrida (missão Nota 10 / CANON §3): acumula as LEITURAS do
    /// jogador durante a fase (escolhas de portal via GateManager.WasBestChoice, golpes de
    /// fraqueza via OnBossElementalHit) e, na morte do boss, fotografa os managers
    /// (CrowdManager/BossManager/CombatSystem) em ComboMath.RunComboStats — o Domain decide
    /// QUAIS combos pagaram (Perfect Gate, Clutch, Overkill...), esta camada só dispara
    /// GameEvents.RaiseComboEarned por combo conquistado. Quem celebra/credita assina o bus
    /// (UI/Economy, Onda 3) — Gameplay nunca chama Meta/Services direto (doc 12 §2.3).
    /// </summary>
    public class ComboSystem : MonoBehaviour, IInitializable
    {
        public static ComboSystem Instance { get; private set; }

        /// <summary>Combos conquistados na última avaliação (debug/HUD; o LevelResult oficial é somado pelo GameManager via OnComboEarned, Onda 3).</summary>
        public int LastComboCount { get; private set; }

        /// <summary>Bônus total em moedas da última avaliação (soma dos ComboMath.BonusCoins).</summary>
        public int LastComboBonusCoins { get; private set; }

        // ---- Estado POR CORRIDA (soft reset em GameManager.LevelStarted, doc 12 §4.11) ----
        private int _totalGateChoices;     // pares de portal decididos
        private int _bestGateChoices;      // escolhas que eram a rota ótima do par (Perfect Gate)
        private int _weaknessHits;         // golpes Weakness (rate-limited na ORIGEM, ≥0,5 s — contrato §2)
        private bool _evaluatedThisRun;    // guarda anti duplo-OnBossDied (crédito duplo de moedas)

        // Buffer REUSADO entre lutas (zero alocação por avaliação, filosofia dos payloads struct).
        // 6 = nº de valores de ComboKind; ComboMath.Evaluate TRUNCA se o enum crescer — novo
        // ComboKind (append no fim, contrato §1.3) exige bump aqui.
        private readonly ComboKind[] _comboBuffer = new ComboKind[6];

        private bool _subscribedToGameManager;

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            // bus estático sobrevive a cenas: -= antes de += (doc 12 §3.2)
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnBossElementalHit += HandleBossElementalHit;
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnBossDied += HandleBossDied;
            TrySubscribeGameManager();
        }

        // GameManager nasce no Boot; cena Game aberta direto no editor pode não tê-lo no Init —
        // re-tenta no Update (idempotente, -= antes de +=; padrão do JuiceController).
        private void TrySubscribeGameManager()
        {
            if (_subscribedToGameManager || GameManager.Instance == null) return;
            GameManager.Instance.LevelStarted -= HandleLevelStarted;
            GameManager.Instance.LevelStarted += HandleLevelStarted;
            _subscribedToGameManager = true;
        }

        private void Update()
        {
            TrySubscribeGameManager();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnBossDied -= HandleBossDied;
            if (GameManager.Instance != null)
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
            _subscribedToGameManager = false;
        }

        // Soft reset por fase (doc 12 §4.11): retry/próxima fase reusam a MESMA cena — todo
        // estado por corrida zera AQUI, nunca em Awake.
        private void HandleLevelStarted(int levelIndex)
        {
            _totalGateChoices = 0;
            _bestGateChoices = 0;
            _weaknessHits = 0;
            _evaluatedThisRun = false;
            LastComboCount = 0;
            LastComboBonusCoins = 0;
        }

        // 1 por exército por par (doc 12 §4.3): o payload já carrega o REJEITADO — a classificação
        // rota ótima vs armadilha é do GateManager (Classify privado), nunca duplicada aqui.
        private void HandleGateConsumed(GateResult r)
        {
            _totalGateChoices++;
            GateManager gates = GateManager.Instance;
            if (gates != null && gates.WasBestChoice(r.gate, r.rejected))
                _bestGateChoices++;
        }

        private void HandleBossElementalHit(BossElementalHit hit)
        {
            if (hit.relation == ElementRelation.Weakness) _weaknessHits++;
        }

        // Vitória: OnBossDied dispara ANTES do ChangeState(Victory) (contrato §5) — os
        // RaiseComboEarned saem AQUI para o GameManager (Onda 3) somar em
        // LevelResult.comboCount/comboBonusCoins antes do ResolveEnd creditar.
        private void HandleBossDied(BossDied died)
        {
            if (_evaluatedThisRun) return;

            // Greybox-friendly (contrato §1.12): qualquer manager ausente → pula sem erro.
            CrowdManager crowd = CrowdManager.Instance;
            BossManager boss = BossManager.Instance;
            CombatSystem combat = CombatSystem.Instance;
            if (crowd == null || boss == null || combat == null) return;

            var fight = boss.LastFight;   // preenchido na morte, sobrevive ao fim da luta (contrato §5)
            ComboMath.RunComboStats stats = new ComboMath.RunComboStats
            {
                bestGateChoices = _bestGateChoices,
                totalGateChoices = _totalGateChoices,
                weaknessHits = _weaknessHits,
                unitsLostOnTrack = crowd.RunUnitsLost,
                survivors = crowd.Count,
                armyPeak = crowd.RunArmyPeak,
                bossFightSeconds = fight.fightSeconds,
                overkillDamage = Mathf.Max(0f, combat.TotalDamageDealt - fight.maxHp),
                bossMaxHp = fight.maxHp
            };

            int count = ComboMath.Evaluate(stats, won: true, _comboBuffer);
            int bonus = 0;
            for (int i = 0; i < count; i++)
            {
                int coins = ComboMath.BonusCoins(_comboBuffer[i]);
                bonus += coins;
                GameEvents.RaiseComboEarned(new ComboEarned(_comboBuffer[i], coins));
            }

            LastComboCount = count;
            LastComboBonusCoins = bonus;
            _evaluatedThisRun = true;
        }
    }
}
