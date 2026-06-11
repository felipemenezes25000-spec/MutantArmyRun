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
    /// Evolução usa a curva canônica de fragmentos do Domain (10 × 2^(n−1), CANON §8).
    /// </summary>
    public class UnitManager : MonoBehaviour, IInitializable
    {
        public static UnitManager Instance { get; private set; }

        [Header("Catálogo — o índice no array É o typeId dos arrays SoA do CrowdManager")]
        [SerializeField] private UnitConfigSO[] _catalog;

        private IRemoteConfigProvider _remoteConfig;   // injetado: Meta não enxerga Services (§2.3)

        public const int MaxUnitLevel = 10;            // CANON §8: nível máximo 10

        // Chave local (fora do RcKeys do Core): entra lá quando o doc 07 fixar o custo.
        private const string UnitEvolveCoinPerShardKey = "unit_evolve_coin_per_shard";

        public int CatalogSize => _catalog != null ? _catalog.Length : 0;

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
        }

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
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

        // ---- Progressão por tropa (SaveData.units) ----

        public UnitProgress GetProgress(string unitId)
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
            UnitProgress p = GetProgress(unitId);
            return p != null ? p.level : 1;
        }

        public int GetShards(string unitId)
        {
            UnitProgress p = GetProgress(unitId);
            return p != null ? p.shards : 0;
        }

        public bool IsUnlocked(string unitId)
        {
            UnitProgress p = GetProgress(unitId);
            return p != null && p.unlocked;
        }

        public void Unlock(string unitId)
        {
            UnitProgress p = GetProgress(unitId);
            if (p == null || p.unlocked) return;
            p.unlocked = true;
            SaveSystem.Instance.MarkDirty();
        }

        public void AddShards(string unitId, int amount)
        {
            if (amount <= 0) return;
            UnitProgress p = GetProgress(unitId);
            if (p == null) return;
            p.shards += amount;
            SaveSystem.Instance.MarkDirty();
        }

        /// <summary>Fragmentos para o PRÓXIMO nível da tropa (CANON §8: 10 × 2^(n−1)).</summary>
        public int GetShardsToNextLevel(string unitId)
        {
            return EconomyMath.ShardsToLevel(GetUnitLevel(unitId));
        }

        /// <summary>
        /// Evolução transacional (CANON §8: fragmentos da própria tropa + moedas): valida
        /// teto e fragmentos, debita moedas via TrySpend e só então consome os fragmentos.
        /// </summary>
        public bool TryEvolve(string unitId)
        {
            if (EconomySystem.Instance == null) return false;
            UnitProgress p = GetProgress(unitId);
            if (p == null || p.level >= MaxUnitLevel) return false;

            int shardsNeeded = EconomyMath.ShardsToLevel(p.level);
            if (shardsNeeded <= 0 || p.shards < shardsNeeded) return false;

            // CANON §8 fixa os fragmentos mas não a parcela em moedas; valor provisório
            // proporcional aos fragmentos, calibrado pelo doc 07 via Remote Config.
            int coinPerShard = GetRcInt(UnitEvolveCoinPerShardKey, 10);
            long coinCost = (long)shardsNeeded * coinPerShard;
            if (coinCost > 0 && !EconomySystem.Instance.TrySpend(CurrencyType.Coin, coinCost, "unit_evolve_" + unitId))
                return false;

            p.shards -= shardsNeeded;
            p.level++;
            SaveSystem.Instance.MarkDirty();
            return true;
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

        private int GetRcInt(string key, int fallback)
            => _remoteConfig != null ? _remoteConfig.GetInt(key, fallback) : fallback;
    }
}
