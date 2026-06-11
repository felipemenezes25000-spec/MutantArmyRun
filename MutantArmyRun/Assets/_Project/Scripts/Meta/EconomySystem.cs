using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// Carteira persistente + RunWallet (doc 12 §4.6). Ganhos e gastos são TRANSACIONAIS:
    /// toda mutação de moeda passa por Earn/TrySpend — muta o save, marca dirty e publica
    /// CurrencyChange no bus, no mesmo frame (regra "dados prontos → tela mostra", §3.2).
    /// A moeda temporária da corrida delega ao Domain.RunWallet: vitória comita coins×1 no
    /// ResolveEnd; o "DOBRAR x2" do rewarded credita o MESMO delta de novo via GrantRunDouble
    /// (a wallet já foi comitada e zerada — recomitar devolveria 0); derrota descarta as
    /// moedas; a XP é SEMPRE comitada. Exceção canônica: moedas do overflow de Supply são creditadas NA
    /// HORA — o CrowdManager (Gameplay) não enxerga Meta (fronteira de asmdef, doc 12 §2.3),
    /// então ele levanta GameEvents.RaiseSupplyOverflow e ESTE sistema assina o evento e
    /// credita via Earn(Coin, coinsGranted, "supply_overflow") no mesmo frame.
    /// </summary>
    public class EconomySystem : MonoBehaviour, IInitializable
    {
        public static EconomySystem Instance { get; private set; }

        private readonly RunWallet _runWallet = new RunWallet();

        // Injetado pelo bootstrap: Meta não enxerga Services (doc 12 §2.3), então o acesso
        // ao Remote Config entra pela interface declarada no Core.
        private IRemoteConfigProvider _remoteConfig;

        public long Coins => SaveSystem.Instance != null ? SaveSystem.Instance.Data.coins : 0L;
        public int Gems => SaveSystem.Instance != null ? SaveSystem.Instance.Data.gems : 0;

        public int RunCoins => _runWallet.Coins;
        public int RunXp => _runWallet.Xp;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable, após Save e RemoteConfig.</summary>
        public void Init()
        {
            Init(ResolveRemoteConfig());
        }

        /// <summary>Overload com provider explícito — testes injetam um fake determinístico.</summary>
        public void Init(IRemoteConfigProvider remoteConfig)
        {
            Instance = this;
            _remoteConfig = remoteConfig;

            // Overflow de Supply credita NA HORA (CANON §3.2 / doc 12 §4.6). O CrowdManager
            // levanta o evento; -= antes de += para Init repetido não duplicar a inscrição.
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;

            // Auto-wiring nos hooks do fim de fase (doc 12 §4.1): commit do RunWallet e
            // recompensa de vitória — Meta enxerga Core, nunca o inverso.
            if (GameManager.Instance != null)
            {
                // RunSnapshot é lido pelo ResolveEnd ANTES do RunCommitter zerar a RunWallet:
                // é o que preenche o delta (runCoins/runXp) exibido na tela de resultado.
                GameManager.Instance.RunSnapshot = () => (RunCoins, RunXp);
                GameManager.Instance.RunCommitter = won => CommitRun(won);
                GameManager.Instance.LevelRewardGranter = GrantLevelReward;
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;   // bus estático sobrevive a cenas (doc 12 §3.2)
        }

        /// <summary>Crédito imediato do excedente de Supply — fanfarra, nunca punição (CANON §3.2).</summary>
        private void HandleSupplyOverflow(SupplyOverflow o)
            => Earn(CurrencyType.Coin, o.coinsGranted, "supply_overflow");

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            // Provider resolvido no prefab [Services] — mesma regra do GameBootstrap (§3.3).
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
        }

        /// <summary>Ganho DENTRO da corrida — acumula na RunWallet, nunca toca o save.</summary>
        public void EarnInRun(int coins, int xp = 0)
        {
            _runWallet.EarnCoins(coins);
            _runWallet.EarnXp(xp);
            // HUD da corrida atualiza por evento (doc 12 §3.2) — source distingue do saldo persistente.
            if (coins > 0)
                GameEvents.RaiseCurrencyChanged(new CurrencyChange(CurrencyType.Coin, coins, "run_earn"));
        }

        /// <summary>Chamado pelo GameManager.ResolveEnd (doc 12 §4.1) via hook RunCommitter.</summary>
        public void CommitRun(bool won, int multiplier = 1)
        {
            var (coins, xp) = _runWallet.BuildCommit(won, multiplier);
            if (coins > 0) Earn(CurrencyType.Coin, coins, "run_commit");
            if (xp > 0) Earn(CurrencyType.Xp, xp, "run_xp");   // XP comitada SEMPRE, vitória ou derrota
        }

        /// <summary>
        /// "DOBRAR ×2" do rewarded (CANON §11): o ResolveEnd já comitou e ZEROU a RunWallet
        /// antes da tela de resultado aparecer — recomitar com multiplier 2 devolveria (0,0).
        /// Dobrar = creditar o delta já comitado uma SEGUNDA vez. Chamado pela camada que
        /// enxerga Services, no callback de sucesso de AdsManager.ShowRewarded(DoubleReward).
        /// </summary>
        public void GrantRunDouble(long baseCoins)
            => Earn(CurrencyType.Coin, baseCoins, "ad_double");

        /// <summary>CANON §8: fase 1 = 100 moedas; cresce growth^(fase−1); recalibrável por Remote Config.</summary>
        public int GetLevelReward(int levelIndex)
        {
            float baseReward = GetRcFloat(RcKeys.LevelRewardBase, 100f);
            float growth = GetRcFloat(RcKeys.LevelRewardGrowth, 1.10f);
            float mult = 1f;
            if (UpgradeSystem.Instance != null)
                mult += UpgradeSystem.Instance.GetBonus(UpgradeTrack.RewardMultiplier);
            return EconomyMath.LevelReward(levelIndex, baseReward, growth, mult);
        }

        public void GrantLevelReward(int levelIndex)
        {
            Earn(CurrencyType.Coin, GetLevelReward(levelIndex), "level_win");
        }

        /// <summary>Funil único de GANHO persistente: muta o save, marca dirty, publica no bus.</summary>
        public void Earn(CurrencyType type, long amount, string source)
        {
            if (amount <= 0 || SaveSystem.Instance == null) return;
            SaveData d = SaveSystem.Instance.Data;
            switch (type)
            {
                case CurrencyType.Coin:
                    d.coins += amount;
                    break;
                case CurrencyType.Gem:
                    d.gems += (int)amount;
                    break;
                case CurrencyType.Xp:
                    d.playerXp += (int)amount;
                    CheckPlayerLevelUp(d);
                    break;
            }
            SaveSystem.Instance.MarkDirty();
            GameEvents.RaiseCurrencyChanged(new CurrencyChange(type, amount, source));
        }

        /// <summary>
        /// Funil único de GASTO: saldo insuficiente retorna false sem efeito algum.
        /// Save em batch via MarkDirty — nunca I/O por transação (doc 12 §4.6).
        /// </summary>
        public bool TrySpend(CurrencyType type, long amount, string sink)
        {
            if (amount <= 0 || SaveSystem.Instance == null) return false;
            if (type == CurrencyType.Xp) return false;          // XP não é moeda gastável (CANON §8)

            SaveData d = SaveSystem.Instance.Data;
            long balance = type == CurrencyType.Coin ? d.coins : d.gems;
            if (balance < amount) return false;

            if (type == CurrencyType.Coin) d.coins -= amount;
            else d.gems -= (int)amount;

            SaveSystem.Instance.MarkDirty();
            GameEvents.RaiseCurrencyChanged(new CurrencyChange(type, -amount, sink));
            return true;
        }

        /// <summary>
        /// Nível de jogador (CANON §8 define os DESBLOQUEIOS por nível, não a curva de XP).
        /// Curva provisória recalibrável por RC sem rebuild: subir do nível n custa n × base.
        /// </summary>
        // Chave local (fora do RcKeys do Core): entra lá quando o doc 07 fixar a curva.
        private const string PlayerXpPerLevelKey = "player_xp_per_level";

        private void CheckPlayerLevelUp(SaveData d)
        {
            int xpPerLevel = GetRcInt(PlayerXpPerLevelKey, 100);
            if (xpPerLevel <= 0) return;
            int required = d.playerLevel * xpPerLevel;
            while (d.playerXp >= required)
            {
                d.playerXp -= required;
                d.playerLevel++;
                required = d.playerLevel * xpPerLevel;
            }
        }

        private float GetRcFloat(string key, float fallback)
            => _remoteConfig != null ? _remoteConfig.GetFloat(key, fallback) : fallback;

        private int GetRcInt(string key, int fallback)
            => _remoteConfig != null ? _remoteConfig.GetInt(key, fallback) : fallback;
    }
}
