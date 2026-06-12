using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// Catálogo de tropas + progressão por tropa (doc 12 §3.1): níveis 1–10 e fragmentos
    /// persistidos em SaveData.units; stats EFETIVOS = base do SO × escala de nível ×
    /// upgrades de meta. O índice no catálogo é o typeId usado pelos arrays SoA do
    /// CrowdManager (doc 12 §4.2) — GetSupplyCost(byte) é parte desse contrato.
    /// Evolução usa a curva canônica de fragmentos do Domain (10 × 2^(n−1), CANON §8) + a
    /// curva de moedas por raridade (100 × 2^(n−1) × raridade, doc 07 §6).
    ///
    /// CONTRATO DE API (cartas/evolução, consumido pelo agente de telas):
    /// AllTroops() · GetProgress(unitId)→TroopProgress · CanEvolve · TryEvolve ·
    /// GrantShards · UnlockTroop. Tropas comuns começam desbloqueadas; raras/épicas/lendárias
    /// só desbloqueiam ao juntar 10 fragmentos (drop de baú/boss).
    /// </summary>
    public class UnitManager : MonoBehaviour, IInitializable
    {
        public static UnitManager Instance { get; private set; }

        [Header("Catálogo — o índice no array É o typeId dos arrays SoA do CrowdManager")]
        [SerializeField] private UnitConfigSO[] _catalog;

        private IRemoteConfigProvider _remoteConfig;   // injetado: Meta não enxerga Services (§2.3)

        public const int MaxUnitLevel = 10;            // CANON §8: nível máximo 10
        private const int UnlockShardThreshold = 10;   // doc 07 §2.3: 10 fragmentos = desbloqueia a tropa

        public int CatalogSize => _catalog != null ? _catalog.Length : 0;

        /// <summary>Disparado quando uma tropa muda (evolução/fragmentos/desbloqueio) — telas re-renderizam (doc 12 §3.2).</summary>
        public event Action<string> OnTroopChanged;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable, após Economy/Upgrade.</summary>
        public void Init()
        {
            Init(ResolveRemoteConfig());
        }

        /// <summary>Overload com provider explícito — testes injetam um fake determinístico.</summary>
        public void Init(IRemoteConfigProvider remoteConfig)
        {
            Instance = this;
            _remoteConfig = remoteConfig;
            SeedStartingUnlocks();
        }

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
        }

        /// <summary>
        /// Estado inicial (doc 07 §2.3): tropas COMUNS nascem desbloqueadas (o jogador começa com
        /// elas); raras/épicas/lendárias ficam bloqueadas até juntar 10 fragmentos. Idempotente —
        /// só liga unlock de comum ainda não tocada; nunca rebaixa progresso existente.
        /// </summary>
        private void SeedStartingUnlocks()
        {
            if (_catalog == null || SaveSystem.Instance == null) return;
            bool dirty = false;
            for (int i = 0; i < _catalog.Length; i++)
            {
                UnitConfigSO cfg = _catalog[i];
                if (cfg == null || cfg.rarity != Rarity.Common) continue;
                UnitProgress p = GetUnitProgress(cfg.unitId);
                if (p != null && !p.unlocked)
                {
                    p.unlocked = true;
                    dirty = true;
                }
            }
            if (dirty) SaveSystem.Instance.MarkDirty();
        }

        // ---- Catálogo ----

        public UnitConfigSO GetConfig(int typeId)
        {
            if (_catalog == null || typeId < 0 || typeId >= _catalog.Length) return null;
            return _catalog[typeId];
        }

        public UnitConfigSO GetConfig(string unitId)
        {
            int typeId = GetTypeId(unitId);
            return typeId >= 0 ? _catalog[typeId] : null;
        }

        public int GetTypeId(string unitId)
        {
            if (_catalog == null || string.IsNullOrEmpty(unitId)) return -1;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i] != null && _catalog[i].unitId == unitId) return i;
            }
            return -1;
        }

        /// <summary>Contrato do CrowdManager (doc 12 §4.2): custo de Supply por typeId.</summary>
        public int GetSupplyCost(byte typeId)
        {
            UnitConfigSO config = GetConfig(typeId);
            return config != null ? config.supplyCost : 1;
        }

        // ==================================================================
        // CONTRATO DE API — cartas/evolução (agente de telas)
        // ==================================================================

        /// <summary>Catálogo completo de tropas (contrato de telas: lista as cartas).</summary>
        public IReadOnlyList<UnitConfigSO> AllTroops()
        {
            return _catalog ?? Array.Empty<UnitConfigSO>();
        }

        /// <summary>
        /// Progresso da tropa como struct de valor (contrato de telas): unlocked/level/shards e
        /// quantos fragmentos faltam para o próximo passo. shardsToNext = custo de desbloqueio
        /// (10) quando bloqueada; custo do próximo nível (Domain) quando desbloqueada; 0 no teto.
        /// </summary>
        public TroopProgress GetProgress(string unitId)
        {
            UnitProgress p = GetUnitProgress(unitId);
            if (p == null) return new TroopProgress(false, 1, 0, UnlockShardThreshold);

            int shardsToNext = !p.unlocked
                ? UnlockShardThreshold
                : (p.level >= MaxUnitLevel ? 0 : EconomyMath.ShardsToLevel(p.level));
            return new TroopProgress(p.unlocked, p.level, p.shards, shardsToNext);
        }

        /// <summary>
        /// Pode evoluir AGORA? (contrato de telas) — desbloqueada, abaixo do teto, com fragmentos
        /// suficientes e moedas suficientes. Consulta pura, sem efeito colateral.
        /// </summary>
        public bool CanEvolve(string unitId)
        {
            UnitProgress p = GetUnitProgress(unitId);
            if (p == null || !p.unlocked || p.level >= MaxUnitLevel) return false;

            int shardsNeeded = EconomyMath.ShardsToLevel(p.level);
            if (shardsNeeded <= 0 || p.shards < shardsNeeded) return false;

            long coinCost = EvolveCoinCost(unitId, p.level);
            if (coinCost > 0 && EconomySystem.Instance != null && EconomySystem.Instance.Coins < coinCost)
                return false;
            return true;
        }

        /// <summary>
        /// Evolução transacional (contrato de telas; CANON §8 + doc 07 §6): valida teto/fragmentos,
        /// debita moedas via TrySpend e só então consome os fragmentos e sobe o nível 1→10.
        /// Em sucesso: MarkDirty + OnTroopChanged.
        /// </summary>
        public bool TryEvolve(string unitId)
        {
            if (EconomySystem.Instance == null) return false;
            UnitProgress p = GetUnitProgress(unitId);
            if (p == null || !p.unlocked || p.level >= MaxUnitLevel) return false;

            int shardsNeeded = EconomyMath.ShardsToLevel(p.level);
            if (shardsNeeded <= 0 || p.shards < shardsNeeded) return false;

            long coinCost = EvolveCoinCost(unitId, p.level);
            if (coinCost > 0 && !EconomySystem.Instance.TrySpend(CurrencyType.Coin, coinCost, "unit_evolve_" + unitId))
                return false;

            p.shards -= shardsNeeded;
            p.level++;
            SaveSystem.Instance.MarkDirty();
            OnTroopChanged?.Invoke(unitId);
            return true;
        }

        /// <summary>
        /// Concede fragmentos (contrato de telas; drop de baú/boss). Se a tropa estava bloqueada e
        /// atinge 10 fragmentos, desbloqueia no nível 1 (doc 07 §2.3) consumindo os 10. Tropa no
        /// teto converte overflow em moedas (1 = 10) — nada é desperdiçado (doc 07 §2.3).
        /// </summary>
        public void GrantShards(string unitId, int amount)
        {
            if (amount <= 0) return;
            UnitProgress p = GetUnitProgress(unitId);
            if (p == null) return;

            p.shards += amount;

            // Desbloqueio automático ao cruzar o limiar (10 fragmentos = tropa nv 1).
            if (!p.unlocked && p.shards >= UnlockShardThreshold)
            {
                p.shards -= UnlockShardThreshold;
                p.unlocked = true;
                p.level = 1;
            }

            // Tropa no teto: fragmentos viram moedas (overflow do doc 07 §2.3).
            if (p.unlocked && p.level >= MaxUnitLevel && p.shards > 0)
            {
                int overflow = p.shards;
                p.shards = 0;
                if (EconomySystem.Instance != null)
                    EconomySystem.Instance.Earn(CurrencyType.Coin, overflow * 10L, "shard_overflow_" + unitId);
            }

            SaveSystem.Instance.MarkDirty();
            OnTroopChanged?.Invoke(unitId);
        }

        /// <summary>Desbloqueia a tropa diretamente (contrato de telas; recompensa de baú/evento).</summary>
        public void UnlockTroop(string unitId)
        {
            UnitProgress p = GetUnitProgress(unitId);
            if (p == null || p.unlocked) return;
            p.unlocked = true;
            if (p.level < 1) p.level = 1;
            SaveSystem.Instance.MarkDirty();
            OnTroopChanged?.Invoke(unitId);
        }

        // ---- Progressão por tropa (SaveData.units) — acessores internos/compat ----

        /// <summary>Progresso persistente (POCO do save). Cria a entrada na 1ª consulta. Uso interno/compat.</summary>
        public UnitProgress GetUnitProgress(string unitId)
        {
            if (SaveSystem.Instance == null || string.IsNullOrEmpty(unitId)) return null;
            UnitProgress p = SaveSystem.Instance.Data.units.Find(x => x.unitId == unitId);
            if (p == null)
            {
                p = new UnitProgress { unitId = unitId };
                SaveSystem.Instance.Data.units.Add(p);
                SaveSystem.Instance.MarkDirty();
            }
            return p;
        }

        public int GetUnitLevel(string unitId)
        {
            UnitProgress p = GetUnitProgress(unitId);
            return p != null ? p.level : 1;
        }

        public int GetShards(string unitId)
        {
            UnitProgress p = GetUnitProgress(unitId);
            return p != null ? p.shards : 0;
        }

        public bool IsUnlocked(string unitId)
        {
            UnitProgress p = GetUnitProgress(unitId);
            return p != null && p.unlocked;
        }

        /// <summary>Compat (RewardSystem chamava Unlock/AddShards) — delegam ao contrato novo.</summary>
        public void Unlock(string unitId) => UnlockTroop(unitId);
        public void AddShards(string unitId, int amount) => GrantShards(unitId, amount);

        /// <summary>Fragmentos para o PRÓXIMO nível da tropa (CANON §8: 10 × 2^(n−1)).</summary>
        public int GetShardsToNextLevel(string unitId)
        {
            return EconomyMath.ShardsToLevel(GetUnitLevel(unitId));
        }

        /// <summary>Custo em moedas da evolução n→n+1 desta tropa (doc 07 §6: 100 × 2^(n−1) × raridade).</summary>
        public long EvolveCoinCost(string unitId, int level)
        {
            UnitConfigSO cfg = GetConfig(unitId);
            int rarityMult = cfg != null ? EconomyMath.RarityCoinMultiplier(cfg.rarity) : 1;
            // RC pode recalibrar o multiplicador base; a curva 2^(n−1) é canônica.
            return EconomyMath.EvolveCoinCost(level, rarityMult);
        }

        // ---- Stats efetivos: base × nível × upgrades de meta (doc 12 §3.1) ----

        public float GetEffectiveHp(string unitId)
        {
            UnitConfigSO config = GetConfig(unitId);
            if (config == null) return 0f;
            float bonus = UpgradeSystem.Instance != null
                ? UpgradeSystem.Instance.GetBonus(UpgradeTrack.StartHealth) : 0f;
            return config.baseHp * EvaluateLevelCurve(config.levelHpCurve, GetUnitLevel(unitId)) * (1f + bonus);
        }

        public float GetEffectiveDps(string unitId)
        {
            UnitConfigSO config = GetConfig(unitId);
            if (config == null) return 0f;
            float bonus = UpgradeSystem.Instance != null
                ? UpgradeSystem.Instance.GetBonus(UpgradeTrack.StartDamage) : 0f;
            return config.baseDps * EvaluateLevelCurve(config.levelDpsCurve, GetUnitLevel(unitId)) * (1f + bonus);
        }

        public float GetEffectiveMoveSpeed(string unitId)
        {
            UnitConfigSO config = GetConfig(unitId);
            if (config == null) return 0f;
            float bonus = UpgradeSystem.Instance != null
                ? UpgradeSystem.Instance.GetBonus(UpgradeTrack.Speed) : 0f;
            return config.moveSpeed * (1f + bonus);
        }

        private static float EvaluateLevelCurve(AnimationCurve curve, int level)
        {
            // SO sem curva configurada: escala provisória +10%/nível, dentro da banda
            // DPS+HP/Supply do CANON §5 — o doc 03 calibra as curvas reais por tropa.
            if (curve == null || curve.length == 0) return 1f + 0.10f * (level - 1);
            return curve.Evaluate(level);
        }
    }

    /// <summary>
    /// Visão de VALOR do progresso de uma tropa para a UI (contrato de telas). Distinta do POCO
    /// de save UnitProgress: este struct é imutável, calculado sob demanda e nunca persistido.
    /// shardsToNext: fragmentos que faltam para o próximo passo (desbloqueio se bloqueada, próximo
    /// nível se desbloqueada, 0 no teto).
    /// </summary>
    public readonly struct TroopProgress
    {
        public readonly bool unlocked;
        public readonly int level;
        public readonly int shards;
        public readonly int shardsToNext;

        public TroopProgress(bool unlocked, int level, int shards, int shardsToNext)
        {
            this.unlocked = unlocked;
            this.level = level;
            this.shards = shards;
            this.shardsToNext = shardsToNext;
        }
    }
}
