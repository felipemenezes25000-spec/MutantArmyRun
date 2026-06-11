using System;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Máquina de estados do jogo (doc 12 §4.1) — a PILHA e a tabela de transições vivem no
    /// Domain (<see cref="GameStateStack"/>); este manager só orquestra EnterState/ExitState/
    /// ResolveEnd. Overlays (ReviveOffer, pausa) entram por Push SOBRE a corrida — aceitar o
    /// revive é um Pop que devolve EXATAMENTE a luta.
    ///
    /// Direção de dependência (doc 12 §2.3): Core não enxerga Gameplay/Meta/Services/UI.
    /// Quem reage a estados assina <see cref="StateEntered"/>/<see cref="StateExited"/>
    /// (LevelManager.BeginRun em Running, BossManager.BeginFight em BossFight,
    /// UIManager.ShowBossScout em BossScout); os passos de dados do fim de fase são hooks
    /// preenchidos pelos bootstraps (§3.3) — as camadas de cima enxergam Core, nunca o inverso.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State => _states.Current;
        public LevelConfigSO CurrentLevel { get; private set; }

        private GameStateStack _states;
        private bool _endSelectionDone;            // guard anti duplo-clique/duplo-evento no fim de fase

        // ---- Notificação de transições (assinada por Gameplay/Meta/UI) ----
        public event Action<GameState> StateEntered;
        public event Action<GameState> StateExited;
        public event Action<int> LevelStarted;     // analytics: level_start (doc 12 §4.9)

        // ---- Hooks do fim de fase (doc 12 §4.1 ResolveEnd), ligados pelos bootstraps ----
        public Func<bool, LevelResult> ResultBuilder { get; set; }     // LevelManager.BuildResult
        public Func<(int coins, int xp)> RunSnapshot { get; set; }     // EconomySystem: delta da RunWallet (lido ANTES do commit)
        public Action<bool> RunCommitter { get; set; }                 // EconomySystem.CommitRun (XP sempre)
        public Action<int> LevelRewardGranter { get; set; }            // EconomySystem.GrantLevelReward
        public Action<LevelResult> LevelEndRecorder { get; set; }      // SaveSystem.RecordLevelEnd (save imediato)

        // ---- Hooks do revive (CANON §11: 1×/fase) ----
        public Func<bool> ReviveAlreadyUsed { get; set; }              // SaveSystem.Data.usedReviveThisLevel
        public Action ReviveCrowd { get; set; }                        // CrowdManager.Revive

        /// <summary>Chamado pelo GameBootstrap (§3.3) — a pilha nasce em Boot.</summary>
        public void Init()
        {
            Instance = this;
            _states = new GameStateStack();
        }

        public void StartLevel(LevelConfigSO level)
        {
            CurrentLevel = level;
            _endSelectionDone = false;
            LevelStarted?.Invoke(level.levelIndex);
            ChangeState(GameState.BossScout);      // cartão de ~2 s ANTES da corrida (CANON §3.1)
        }

        /// <summary>Troca o TOPO da pilha (transição lateral). Ilegal loga erro e é ignorada.</summary>
        public void ChangeState(GameState next)
        {
            if (_states == null)
            {
                Debug.LogError("[GameManager] ChangeState antes do Init() — bootstrap fora de ordem (§3.3).");
                return;
            }
            GameState previous = _states.Current;
            if (!_states.ChangeState(next))        // tabela Allowed vive no Domain — nunca corrompe o fluxo
            {
                Debug.LogError($"Transição ilegal {previous}→{next}");
                return;
            }
            ExitState(previous);
            EnterState(next);
        }

        /// <summary>PUSH: estado temporário SOBRE a corrida — quem está embaixo congela, não morre.</summary>
        public void PushState(GameState overlay)
        {
            _states.Push(overlay);
            EnterState(overlay);
        }

        public void PopState()
        {
            ExitState(_states.Pop());              // estado de baixo retoma intacto
        }

        /// <summary>Derrota no boss com revive disponível: PUSH preserva a luta (CANON §11: 1×/fase).</summary>
        public void OfferRevive()
        {
            bool used = ReviveAlreadyUsed != null && ReviveAlreadyUsed();
            if (used)
            {
                ChangeState(GameState.Defeat);
                return;
            }
            PushState(GameState.ReviveOffer);      // BossFight continua vivo embaixo
        }

        public void ResolveRevive(bool revived)
        {
            PopState();                            // sai do ReviveOffer
            if (revived)
            {
                if (ReviveCrowd != null) ReviveCrowd();    // segunda chance + invencibilidade breve
            }
            else
            {
                ChangeState(GameState.Defeat);
            }
        }

        private void EnterState(GameState s)
        {
            // BossScout/Running/BossFight: quem age é o assinante (UIManager/LevelManager/BossManager).
            StateEntered?.Invoke(s);
            switch (s)
            {
                case GameState.Victory: ResolveEnd(won: true); break;
                case GameState.Defeat: ResolveEnd(won: false); break;
            }
        }

        private void ExitState(GameState s)
        {
            StateExited?.Invoke(s);                // cleanup por estado mora no assinante dono do estado
        }

        private void ResolveEnd(bool won)
        {
            if (_endSelectionDone) return;         // duplo-clique/duplo-evento: ignora
            _endSelectionDone = true;

            int levelIndex = CurrentLevel != null ? CurrentLevel.levelIndex : 0;
            LevelResult result = ResultBuilder != null
                ? ResultBuilder(won)
                : new LevelResult(levelIndex, won, 0, 0f, 0, 0, 0f);

            // Delta da corrida (runCoins/runXp) vem da Meta ANTES do commit zerar a RunWallet:
            // Gameplay não enxerga Meta (§2.3), então o result é completado AQUI — mantém a
            // ordem "result construído antes do commit" do doc 12 §4.1.
            if (RunSnapshot != null)
            {
                var (coins, xp) = RunSnapshot();
                result.runCoins = coins;
                result.runXp = xp;
            }

            if (RunCommitter != null) RunCommitter(won);                       // RunWallet → carteira (§4.6); XP sempre
            if (won && LevelRewardGranter != null) LevelRewardGranter(levelIndex);
            if (LevelEndRecorder != null) LevelEndRecorder(result);            // save imediato pós-fase
            GameEvents.RaiseLevelFinished(result);                             // UI/Ads/Analytics reagem
        }
    }
}
