using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Payloads do event bus (doc 12 §3.2): structs para Raise sem alocação no heap.
    /// Payload é imutável por disparo — listener que precisa do dado depois guarda cópia própria.
    /// </summary>
    public struct GateResult
    {
        public GateConfigSO gate;     // o portal consumido (1 por exército por par, doc 12 §4.3)
        public GateConfigSO rejected; // o irmão recusado — gate_selected mede rota ótima vs armadilha (doc 11)
        public int newCount;          // contagem do exército APÓS a reconciliação

        public GateResult(GateConfigSO gate, GateConfigSO rejected, int newCount)
        {
            this.gate = gate;
            this.rejected = rejected;
            this.newCount = newCount;
        }
    }

    /// <summary>Excedente de Supply convertido em moedas (CANON §3.2 — fanfarra, nunca punição).</summary>
    public struct SupplyOverflow
    {
        public int unitsConverted;    // unidades removidas pelo plano do SupplyLedger
        public int coinsGranted;      // unidades × supply_overflow_coin_rate (creditadas NA HORA, doc 12 §4.6)

        public SupplyOverflow(int unitsConverted, int coinsGranted)
        {
            this.unitsConverted = unitsConverted;
            this.coinsGranted = coinsGranted;
        }
    }

    /// <summary>Morte de unidade — ponto de extensão para VFX de desmonte e analytics (doc 12 §4.2).</summary>
    public struct UnitDeath
    {
        public byte typeId;           // índice do tipo nos arrays SoA do CrowdManager
        public Vector3 position;      // onde tocar o VFX de desmonte em peças

        public UnitDeath(byte typeId, Vector3 position)
        {
            this.typeId = typeId;
            this.position = position;
        }
    }

    /// <summary>Mudança de fase de agressividade do boss (doc 12 §4.5, limiares 0.5/0.25).</summary>
    public struct BossPhase
    {
        public int phase;                     // 0/1/2 — via Domain.CombatMath.BossPhase
        public ElementType activeWeakness;    // fraqueza atual (rotativa no Alien Supremo, CANON §6)

        public BossPhase(int phase, ElementType activeWeakness)
        {
            this.phase = phase;
            this.activeWeakness = activeWeakness;
        }
    }

    /// <summary>
    /// Resultado de fim de fase (doc 12 §3.2/§4.1): vitória ou derrota + stats da corrida.
    /// A tela de resultado é passiva e mostra o TOTAL creditado na fase (coinsAwarded/
    /// xpAwarded), nunca o total da carteira. runCoins/runXp guardam só o delta DA CORRIDA
    /// (base do "DOBRAR x2", que dobra apenas as moedas coletadas — CANON §11); coinsAwarded
    /// soma a recompensa de vitória da fase (GrantLevelReward, CANON §8) ao runCoins, que é
    /// o número grande exibido na vitória. O GameManager.ResolveEnd preenche os campos
    /// *Awarded ANTES do Raise — a tela só mostra.
    /// </summary>
    public struct LevelResult
    {
        public int levelIndex;
        public bool won;
        public int survivors;         // unidades vivas no fim (alimenta LevelRecord.bestSurvivors)
        public float damageDealt;     // dano total causado ao boss na corrida
        public int runCoins;          // moedas SÓ da RunWallet ANTES do commit (base do "DOBRAR x2")
        public int runXp;             // XP da corrida (comitada SEMPRE, vitória ou derrota)
        public float durationSeconds;
        public long coinsAwarded;     // TOTAL creditado na fase = recompensa de vitória + runCoins (delta exibido)
        public int xpAwarded;         // TOTAL de XP ganho na fase (delta exibido)

        public LevelResult(int levelIndex, bool won, int survivors, float damageDealt,
                           int runCoins, int runXp, float durationSeconds)
            : this(levelIndex, won, survivors, damageDealt, runCoins, runXp, durationSeconds,
                   coinsAwarded: 0L, xpAwarded: 0)
        {
        }

        public LevelResult(int levelIndex, bool won, int survivors, float damageDealt,
                           int runCoins, int runXp, float durationSeconds,
                           long coinsAwarded, int xpAwarded)
        {
            this.levelIndex = levelIndex;
            this.won = won;
            this.survivors = survivors;
            this.damageDealt = damageDealt;
            this.runCoins = runCoins;
            this.runXp = runXp;
            this.durationSeconds = durationSeconds;
            this.coinsAwarded = coinsAwarded;
            this.xpAwarded = xpAwarded;
        }
    }

    /// <summary>Transação de moeda na carteira persistente (doc 12 §4.6). amount negativo = gasto.</summary>
    public struct CurrencyChange
    {
        public CurrencyType type;
        public long amount;
        public string source;         // "run_commit", "level_win", id de sink...

        public CurrencyChange(CurrencyType type, long amount, string source)
        {
            this.type = type;
            this.amount = amount;
            this.source = source;
        }
    }
}
