using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// As 8 trilhas de upgrade de meta (CANON §9; 4 no MVP): +5% por nível, custo
    /// 100 × 1,35^n. Curvas delegadas ao Domain.EconomyMath; níveis persistidos em
    /// SaveData.upgradeTracks (trackId = nome do enum). A compra passa pelo funil
    /// transacional do EconomySystem — nunca escreve moeda no save direto.
    /// </summary>
    public class UpgradeSystem : MonoBehaviour, IInitializable
    {
        public static UpgradeSystem Instance { get; private set; }

        [Header("1 UpgradeConfigSO por trilha (inMvp marca as 4 do MVP)")]
        [SerializeField] private UpgradeConfigSO[] _trackConfigs;

        private IRemoteConfigProvider _remoteConfig;   // injetado: Meta não enxerga Services (§2.3)

        private const int DefaultMaxLevel = 50;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable, após o EconomySystem.</summary>
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

        public int GetTrackLevel(UpgradeTrack track)
        {
            if (SaveSystem.Instance == null) return 0;
            string id = track.ToString();
            TrackProgress p = SaveSystem.Instance.Data.upgradeTracks.Find(x => x.trackId == id);
            return p != null ? p.level : 0;
        }

        /// <summary>
        /// Bônus fracionário da trilha (0.05 × nível) — consumido pelo CombatSystem e pelo
        /// EconomySystem (doc 12 §4.4/§4.6). Exceção: StartArmy retorna UNIDADES inteiras
        /// (+1 a cada 2 níveis) — para esse caso use GetStartArmyBonusUnits().
        /// </summary>
        public float GetBonus(UpgradeTrack track)
        {
            return EconomyMath.TrackBonus(track, GetTrackLevel(track));
        }

        /// <summary>Unidades extras no início da fase (trilha Exército inicial, CANON §9).</summary>
        public int GetStartArmyBonusUnits()
        {
            return (int)EconomyMath.TrackBonus(UpgradeTrack.StartArmy, GetTrackLevel(UpgradeTrack.StartArmy));
        }

        /// <summary>Custo do PRÓXIMO nível da trilha. RC sobrescreve; fallback é o SO ou o CANON.</summary>
        public int GetUpgradeCost(UpgradeTrack track)
        {
            UpgradeConfigSO config = FindConfig(track);
            float soBase = config != null ? config.costBase : 100f;
            float soGrowth = config != null ? config.costGrowth : 1.35f;
            float costBase = GetRcFloat(RcKeys.UpgradeCostBase, soBase);
            float growth = GetRcFloat(RcKeys.UpgradeCostGrowth, soGrowth);
            return EconomyMath.UpgradeCost(GetTrackLevel(track), costBase, growth);
        }

        public int GetMaxLevel(UpgradeTrack track)
        {
            UpgradeConfigSO config = FindConfig(track);
            return config != null ? config.maxLevel : DefaultMaxLevel;
        }

        /// <summary>
        /// Compra transacional: TrySpend primeiro; só com débito confirmado o nível sobe.
        /// Retorna false sem efeito em saldo insuficiente ou trilha no teto.
        /// </summary>
        public bool TryBuyUpgrade(UpgradeTrack track)
        {
            if (SaveSystem.Instance == null || EconomySystem.Instance == null) return false;

            int level = GetTrackLevel(track);
            if (level >= GetMaxLevel(track)) return false;

            int cost = GetUpgradeCost(track);
            if (!EconomySystem.Instance.TrySpend(CurrencyType.Coin, cost, "upgrade_" + track)) return false;

            string id = track.ToString();
            TrackProgress p = SaveSystem.Instance.Data.upgradeTracks.Find(x => x.trackId == id);
            if (p == null)
            {
                p = new TrackProgress { trackId = id };
                SaveSystem.Instance.Data.upgradeTracks.Add(p);
            }
            p.level = level + 1;
            SaveSystem.Instance.MarkDirty();
            return true;
        }

        private UpgradeConfigSO FindConfig(UpgradeTrack track)
        {
            if (_trackConfigs == null) return null;
            for (int i = 0; i < _trackConfigs.Length; i++)
            {
                if (_trackConfigs[i] != null && _trackConfigs[i].track == track) return _trackConfigs[i];
            }
            return null;
        }

        private float GetRcFloat(string key, float fallback)
            => _remoteConfig != null ? _remoteConfig.GetFloat(key, fallback) : fallback;
    }
}
