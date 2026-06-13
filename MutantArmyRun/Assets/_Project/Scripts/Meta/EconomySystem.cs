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

        // Variante RARA (missão Nota 10 §4.2): OnBossDied com wasRare arma a flag e a recompensa
        // de VITÓRIA da fase multiplica ×3 (RareBossMath.RewardMultiplier) — tanto no crédito
        // (GrantLevelReward) quanto na consulta de exibição (LevelRewardProvider), para o número
        // grande da tela bater com o creditado. Reset por fase (LevelStarted).
        private bool _rareBossKilledThisLevel;

        // Injetado pelo bootstrap: Meta não enxerga Services (doc 12 §2.3), então o acesso
        // ao Remote Config entra pela interface declarada no Core.
        private IRemoteConfigProvider _remoteConfig;

        public long Coins => SaveSystem.Instance != null ? SaveSystem.Instance.Data.coins : 0L;
        public int Gems => SaveSystem.Instance != null ? SaveSystem.Instance.Data.gems : 0;

        public int RunCoins => _runWallet.Coins;
        public int RunXp => _runWallet.Xp;

        /// <summary>
        /// Disparado quando a XP faz o jogador SUBIR de nível (CANON §8) — uma vez por nível ganho.
        /// Carrega o novo nível. A UI (CelebrationOverlay) assina para celebrar "NÍVEL N!" sem
        /// acoplamento; Meta não enxerga UI (doc 12 §2.3), então é a UI que se inscreve no evento.
        /// </summary>
        public event System.Action<int> OnPlayerLevelUp;

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

            // Missão Nota 10: bônus de combo e drop de inimigo de pista entram na RunWallet
            // NA HORA (dentro da corrida) — o commit do ResolveEnd já os inclui, e por estarem
            // em runCoins eles entram na base do "DOBRAR ×2" (decisão de design: recompensa
            // generosa). Crédito 1×: o comboBonusCoins do LevelResult é só INFORMATIVO.
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnComboEarned += HandleComboEarned;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            GameEvents.OnTrackEnemyKilled += HandleTrackEnemyKilled;
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnBossDied += HandleBossDied;

            // Auto-wiring nos hooks do fim de fase (doc 12 §4.1): commit do RunWallet e
            // recompensa de vitória — Meta enxerga Core, nunca o inverso.
            if (GameManager.Instance != null)
            {
                // RunSnapshot é lido pelo ResolveEnd ANTES do RunCommitter zerar a RunWallet:
                // é o que preenche o delta (runCoins/runXp) exibido na tela de resultado.
                GameManager.Instance.RunSnapshot = () => (RunCoins, RunXp);
                GameManager.Instance.RunCommitter = won => CommitRun(won);
                GameManager.Instance.LevelRewardGranter = GrantLevelReward;
                // Só CONSULTA o valor da recompensa para a tela EXIBIR o total — o crédito
                // segue exclusivamente por LevelRewardGranter (nunca duplica, doc 12 §4.6).
                // Variante com bônus de boss raro: exibição e crédito veem o MESMO ×3.
                GameManager.Instance.LevelRewardProvider = GetLevelRewardWithRareBonus;

                // XP DE FASE (CANON §8): cada corrida concede XP de verdade. Creditada na
                // RunWallet UMA vez no início da fase (LevelStarted); o ResolveEnd a lê via
                // RunSnapshot e a comita SEMPRE (vitória ou derrota) — sem duplicar. O retry/
                // próxima fase passa por StartLevel→LevelStarted de novo, já com a wallet zerada
                // pelo commit anterior. -= antes de += para Init repetido não duplicar a inscrição.
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
                GameManager.Instance.LevelStarted += HandleLevelStarted;
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;   // bus estático sobrevive a cenas (doc 12 §3.2)
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            GameEvents.OnBossDied -= HandleBossDied;
            if (GameManager.Instance != null)
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
        }

        /// <summary>Início de fase: zera a flag de boss raro e credita a XP de fase na RunWallet.</summary>
        private void HandleLevelStarted(int levelIndex)
        {
            _rareBossKilledThisLevel = false;   // a variante rara vale só para a fase em que morreu
            GrantPhaseXp(levelIndex);
        }

        /// <summary>
        /// XP da fase creditada na RunWallet no início da corrida (CANON §8). Curva pura do
        /// Domain (EconomyMath.LevelXp), recalibrável por Remote Config. Único ponto de
        /// concessão — o commit do fim de fase a transfere para o save (playerXp/nível).
        /// </summary>
        private void GrantPhaseXp(int levelIndex)
        {
            EarnInRun(0, GetLevelXp(levelIndex));
        }

        /// <summary>XP base/incremento por fase (CANON §8); recalibrável por Remote Config.</summary>
        public int GetLevelXp(int levelIndex)
        {
            int baseXp = GetRcInt(RcKeys.LevelXpBase, 20);
            int step = GetRcInt(RcKeys.LevelXpStep, 10);
            return EconomyMath.LevelXp(levelIndex, baseXp, step);
        }

        /// <summary>Crédito imediato do excedente de Supply — fanfarra, nunca punição (CANON §3.2).</summary>
        private void HandleSupplyOverflow(SupplyOverflow o)
            => Earn(CurrencyType.Coin, o.coinsGranted, "supply_overflow");

        /// <summary>
        /// Bônus de combo creditado 1× na RunWallet, AINDA dentro da corrida: o ComboSystem
        /// dispara na morte do boss, ~1,2 s antes do Victory (sequência cinematográfica) —
        /// o commit do ResolveEnd já o inclui em runCoins, e ele entra na base do DOBRAR ×2.
        /// </summary>
        private void HandleComboEarned(ComboEarned combo)
            => EarnInRun(combo.bonusCoins, 0, ComboSource(combo.kind));

        /// <summary>Drop de inimigo de pista (TrackEnemyManager) → RunWallet, na hora.</summary>
        private void HandleTrackEnemyKilled(TrackEnemyKilled kill)
            => EarnInRun(kill.coins, 0, "enemy_kill");

        /// <summary>Boss da fase morreu na variante RARA → recompensa de vitória multiplica ×3.</summary>
        private void HandleBossDied(BossDied died)
        {
            if (died.wasRare) _rareBossKilledThisLevel = true;
        }

        // Source estável por tipo de combo (analytics/HUD distinguem a origem sem alocação por Raise).
        private static string ComboSource(ComboKind kind)
        {
            switch (kind)
            {
                case ComboKind.PerfectGate: return "combo_perfect_gate";
                case ComboKind.WeaknessHit: return "combo_weakness_hit";
                case ComboKind.BossBreaker: return "combo_boss_breaker";
                case ComboKind.Clutch: return "combo_clutch";
                case ComboKind.NoLoss: return "combo_no_loss";
                case ComboKind.Overkill: return "combo_overkill";
                default: return "combo_unknown";   // ComboKind novo (append-only) cai aqui até ganhar case
            }
        }

        private static IRemoteConfigProvider ResolveRemoteConfig()
        {
            // Provider resolvido no prefab [Services] — mesma regra do GameBootstrap (§3.3).
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
        }

        /// <summary>Ganho DENTRO da corrida — acumula na RunWallet, nunca toca o save.</summary>
        public void EarnInRun(int coins, int xp = 0)
            => EarnInRun(coins, xp, "run_earn");

        /// <summary>
        /// Ganho DENTRO da corrida com source explícito ("combo_*", "enemy_kill"): mesmo funil
        /// da RunWallet, mas o CurrencyChange carrega a ORIGEM — analytics/HUD distinguem o
        /// ganho sem cheirar o gameplay. ATENÇÃO: é delta temporário (vira persistente só no
        /// "run_commit") — quem assina o bus não deve somar estes sources no saldo da carteira.
        /// </summary>
        public void EarnInRun(int coins, int xp, string source)
        {
            _runWallet.EarnCoins(coins);
            _runWallet.EarnXp(xp);
            // HUD da corrida atualiza por evento (doc 12 §3.2) — source distingue do saldo persistente.
            if (coins > 0)
                GameEvents.RaiseCurrencyChanged(new CurrencyChange(CurrencyType.Coin, coins, source));
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

        /// <summary>
        /// Recompensa de vitória JÁ com o multiplicador de boss raro da fase corrente
        /// (RareBossMath.RewardMultiplier ×3 quando o boss morreu na variante rara — missão §4.2).
        /// É o que o LevelRewardProvider expõe ao ResolveEnd: o coinsAwarded exibido na tela
        /// bate com o crédito do GrantLevelReward, que usa a MESMA conta. GetLevelReward segue
        /// puro para consultas fora da fase (mapa/loja nunca mostram o ×3 por engano).
        /// </summary>
        public int GetLevelRewardWithRareBonus(int levelIndex)
        {
            int baseReward = GetLevelReward(levelIndex);
            return _rareBossKilledThisLevel
                ? Mathf.RoundToInt(baseReward * RareBossMath.RewardMultiplier)
                : baseReward;
        }

        public void GrantLevelReward(int levelIndex)
        {
            Earn(CurrencyType.Coin, GetLevelRewardWithRareBonus(levelIndex), "level_win");
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
        /// Nível de jogador (CANON §8 define os DESBLOQUEIOS por nível). Curva de XP do doc 07 §3.3,
        /// agora canônica no Domain (EconomyMath.PlayerLevelXpToNext): o limiar do próximo nível é o
        /// DELTA acumulado nível→nível+1, batendo nos marcos do CANON §16 (nv 2 = 120, nv 4 = 380…).
        /// playerXp guarda a XP DENTRO do nível atual; o excedente sobe níveis em loop.
        /// </summary>
        private void CheckPlayerLevelUp(SaveData d)
        {
            int required = EconomyMath.PlayerLevelXpToNext(d.playerLevel);
            while (required > 0 && d.playerXp >= required)
            {
                d.playerXp -= required;
                d.playerLevel++;
                // Notifica a UI a CADA nível ganho (um Earn de XP grande pode subir vários de uma vez):
                // a celebração mostra o nível novo. Invocado após o ++ para carregar o nível final do passo.
                OnPlayerLevelUp?.Invoke(d.playerLevel);
                required = EconomyMath.PlayerLevelXpToNext(d.playerLevel);
            }
        }

        private float GetRcFloat(string key, float fallback)
            => _remoteConfig != null ? _remoteConfig.GetFloat(key, fallback) : fallback;

        private int GetRcInt(string key, int fallback)
            => _remoteConfig != null ? _remoteConfig.GetInt(key, fallback) : fallback;
    }
}
