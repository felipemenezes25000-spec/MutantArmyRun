using System;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// As 8 trilhas de upgrade de meta (CANON §9): +5% por nível, custo 100 × 1,35^n. Curvas
    /// delegadas ao Domain.EconomyMath; níveis persistidos em SaveData.upgradeTracks (trackId =
    /// nome do enum). A compra passa pelo funil transacional do EconomySystem — nunca escreve
    /// moeda no save direto.
    ///
    /// CONTRATO DE API (consumido pelo agente de telas): GetLevel/GetCost/GetEffect/MaxLevel/TryBuy.
    /// Os efeitos chegam ao gameplay no início da corrida via GameManager.RunStartBonusProvider
    /// (Meta e Gameplay são camadas-irmãs, §2.3): o LevelManager.BeginRun lê o struct e empurra
    /// para CombatSystem/CrowdAnchor/CrowdManager. RewardMultiplier e os stats por tropa
    /// (StartDamage/StartHealth/Speed) já são lidos por EconomySystem/UnitManager via GetEffect.
    /// </summary>
    public class UpgradeSystem : MonoBehaviour, IInitializable
    {
        public static UpgradeSystem Instance { get; private set; }

        [Header("1 UpgradeConfigSO por trilha (inMvp marca as 4 do MVP)")]
        [SerializeField] private UpgradeConfigSO[] _trackConfigs;

        private IRemoteConfigProvider _remoteConfig;   // injetado: Meta não enxerga Services (§2.3)

        private const int DefaultMaxLevel = 50;

        /// <summary>Disparado após cada compra (telas re-renderizam sem polling, doc 12 §3.2).</summary>
        public event Action<UpgradeTrack> OnUpgradeChanged;

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

            // Provider de bônus de início de corrida (CANON §9): o LevelManager.BeginRun (Gameplay)
            // lê este Func no Running e empurra para os setters de combate/velocidade/exército.
            if (GameManager.Instance != null)
                GameManager.Instance.RunStartBonusProvider = GetRunStartBonuses;
        }

        private void OnDestroy()
        {
            // só limpa o hook se ainda aponta para ESTE sistema (re-bootstrap em teste pode trocar)
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.RunStartBonusProvider == (Func<RunStartBonuses>)GetRunStartBonuses)
                gm.RunStartBonusProvider = null;
        }

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
        }

        // ==================================================================
        // CONTRATO DE API (agente de telas)
        // ==================================================================

        /// <summary>Nível atual da trilha (0 se nunca comprada). Contrato de telas.</summary>
        public int GetLevel(UpgradeTrack track)
        {
            if (SaveSystem.Instance == null) return 0;
            string id = track.ToString();
            TrackProgress p = SaveSystem.Instance.Data.upgradeTracks.Find(x => x.trackId == id);
            return p != null ? p.level : 0;
        }

        /// <summary>Custo em moedas do PRÓXIMO nível da trilha (CANON §8). Contrato de telas.</summary>
        public long GetCost(UpgradeTrack track)
        {
            UpgradeConfigSO config = FindConfig(track);
            float soBase = config != null ? config.costBase : 100f;
            float soGrowth = config != null ? config.costGrowth : 1.35f;
            float costBase = GetRcFloat(RcKeys.UpgradeCostBase, soBase);
            float growth = GetRcFloat(RcKeys.UpgradeCostGrowth, soGrowth);
            return EconomyMath.UpgradeCost(GetLevel(track), costBase, growth);
        }

        /// <summary>
        /// Efeito ACUMULADO da trilha (contrato de telas): valor fracionário, ex.: 0.25 = +25%.
        /// Exceção StartArmy: retorna UNIDADES inteiras (+1 a cada 2 níveis) como float.
        /// Speed respeita o cap de corrida de +50% (doc 07 §5.3); ObstacleResist retorna a
        /// REDUÇÃO de perdas (1 − fator composto, ex.: 0.226 = −22,6%).
        /// </summary>
        public float GetEffect(UpgradeTrack track)
        {
            int level = GetLevel(track);
            switch (track)
            {
                case UpgradeTrack.StartArmy:
                    return EconomyMath.TrackBonus(UpgradeTrack.StartArmy, level);   // unidades
                case UpgradeTrack.Speed:
                    return EconomyMath.SpeedRunMultiplier(level) - 1f;              // +% de corrida (capado)
                case UpgradeTrack.ObstacleResist:
                    return 1f - EconomyMath.ObstacleLossFactor(level);             // redução de perdas
                default:
                    return EconomyMath.TrackBonus(track, level);                   // +5%/nível
            }
        }

        /// <summary>Nível máximo da trilha (SO ou default). Contrato de telas.</summary>
        public int MaxLevel(UpgradeTrack track)
        {
            UpgradeConfigSO config = FindConfig(track);
            return config != null ? config.maxLevel : DefaultMaxLevel;
        }

        /// <summary>
        /// Compra transacional (contrato de telas): TrySpend primeiro; só com débito confirmado o
        /// nível sobe. Retorna false sem efeito em saldo insuficiente ou trilha no teto.
        /// Em sucesso: MarkDirty + OnUpgradeChanged.
        /// </summary>
        public bool TryBuy(UpgradeTrack track)
        {
            if (SaveSystem.Instance == null || EconomySystem.Instance == null) return false;

            int level = GetLevel(track);
            if (level >= MaxLevel(track)) return false;

            long cost = GetCost(track);
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
            OnUpgradeChanged?.Invoke(track);
            return true;
        }

        // ==================================================================
        // Bônus consumidos por outros sistemas (compat + internos)
        // ==================================================================

        /// <summary>
        /// Bônus fracionário cru da trilha (0.05 × nível) — consumido pelo EconomySystem
        /// (RewardMultiplier) e pelo UnitManager (StartHealth/StartDamage/Speed nas stats por
        /// tropa). Mantido por compat; para a UI use GetEffect. StartArmy retorna unidades.
        /// </summary>
        public float GetBonus(UpgradeTrack track)
        {
            return EconomyMath.TrackBonus(track, GetLevel(track));
        }

        /// <summary>Unidades extras no início da fase (trilha Exército inicial, CANON §9).</summary>
        public int GetStartArmyBonusUnits()
        {
            return (int)EconomyMath.TrackBonus(UpgradeTrack.StartArmy, GetLevel(UpgradeTrack.StartArmy));
        }

        /// <summary>
        /// Snapshot dos bônus de início de corrida (CANON §9 / doc 07 §5.3). Lido pelo
        /// LevelManager.BeginRun via GameManager.RunStartBonusProvider.
        /// </summary>
        public RunStartBonuses GetRunStartBonuses()
        {
            return new RunStartBonuses(
                startDamage: EconomyMath.TrackBonus(UpgradeTrack.StartDamage, GetLevel(UpgradeTrack.StartDamage)),
                startHealth: EconomyMath.TrackBonus(UpgradeTrack.StartHealth, GetLevel(UpgradeTrack.StartHealth)),
                speedRunMult: EconomyMath.SpeedRunMultiplier(GetLevel(UpgradeTrack.Speed)),
                extraStartUnits: GetStartArmyBonusUnits(),
                bossDamage: EconomyMath.TrackBonus(UpgradeTrack.BossDamage, GetLevel(UpgradeTrack.BossDamage)),
                critChance: EconomyMath.TrackBonus(UpgradeTrack.CritChance, GetLevel(UpgradeTrack.CritChance)),
                obstacleLossFactor: EconomyMath.ObstacleLossFactor(GetLevel(UpgradeTrack.ObstacleResist)));
        }

        // ---- Aliases legados (mantêm compilando o código que já existia) ----
        public int GetTrackLevel(UpgradeTrack track) => GetLevel(track);
        public int GetUpgradeCost(UpgradeTrack track) => (int)GetCost(track);
        public int GetMaxLevel(UpgradeTrack track) => MaxLevel(track);
        public bool TryBuyUpgrade(UpgradeTrack track) => TryBuy(track);

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
