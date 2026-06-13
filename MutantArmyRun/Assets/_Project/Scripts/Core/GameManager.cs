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

        /// <summary>
        /// Motivo da última derrota (doc 09 §4.5) — a ResultScreen exibe o texto. Definido por
        /// quem causa a derrota (CrowdManager no wipe do exército; BossFight no wipe da arena).
        /// Resetado no início de cada fase. ArmyWiped = "Exército eliminado" durante a corrida;
        /// BossWon = "O boss venceu" (wipe na arena). None = vitória/sem derrota.
        /// </summary>
        public DefeatReason LastDefeatReason { get; private set; } = DefeatReason.None;

        /// <summary>Registrado pela camada que detecta a derrota (Gameplay), lido pela UI.</summary>
        public void SetDefeatReason(DefeatReason reason) => LastDefeatReason = reason;

        private GameStateStack _states;
        private bool _endSelectionDone;            // guard anti duplo-clique/duplo-evento no fim de fase

        // ---- Acumuladores da missão Nota 10 (vida = 1 fase; reset em StartLevel) ----
        // O GameManager assina o PRÓPRIO bus (Core→Core, nunca Gameplay): ComboEarned chega na
        // morte do boss (~1,2 s ANTES do Victory — sequência cinematográfica do BossManager) e
        // FailReasonResolved chega no wipe ANTES do Defeat — o ResolveEnd só consolida no result.
        private int _runComboCount;
        private int _runComboBonusCoins;
        private FailReason _runFailReason = FailReason.None;

        // ---- Notificação de transições (assinada por Gameplay/Meta/UI) ----
        public event Action<GameState> StateEntered;
        public event Action<GameState> StateExited;
        public event Action<int> LevelStarted;     // analytics: level_start (doc 12 §4.9)

        // ---- Hooks do fim de fase (doc 12 §4.1 ResolveEnd), ligados pelos bootstraps ----
        public Func<bool, LevelResult> ResultBuilder { get; set; }     // LevelManager.BuildResult
        public Func<(int coins, int xp)> RunSnapshot { get; set; }     // EconomySystem: delta da RunWallet (lido ANTES do commit)
        public Action<bool> RunCommitter { get; set; }                 // EconomySystem.CommitRun (XP sempre)
        public Action<int> LevelRewardGranter { get; set; }            // EconomySystem.GrantLevelReward (CREDITA)
        public Func<int, int> LevelRewardProvider { get; set; }        // EconomySystem.GetLevelReward (só CONSULTA p/ exibição — não credita)
        public Action<LevelResult> LevelEndRecorder { get; set; }      // SaveSystem.RecordLevelEnd (save imediato)

        // ---- Hooks do revive (CANON §11: 1×/fase) ----
        public Func<bool> ReviveAlreadyUsed { get; set; }              // SaveSystem.Data.usedReviveThisLevel
        public Action ReviveCrowd { get; set; }                        // CrowdManager.Revive

        // ---- Bônus de meta no início da corrida (CANON §9 / doc 07 §5.3) ----
        // Owner = Meta (UpgradeSystem.GetRunStartBonuses); consumidor = Gameplay
        // (LevelManager.BeginRun). Meta e Gameplay são camadas-irmãs (§2.3) — o provider
        // trafega por aqui. Ausente (provider null) ⇒ RunStartBonuses.None (jogo neutro).
        public Func<RunStartBonuses> RunStartBonusProvider { get; set; }

        /// <summary>Chamado pelo GameBootstrap (§3.3) — a pilha nasce em Boot.</summary>
        public void Init()
        {
            Instance = this;
            _states = new GameStateStack();

            // Bus estático sobrevive a cenas: -= antes de += (re-Init nunca duplica, §3.2).
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnComboEarned += HandleComboEarned;
            GameEvents.OnFailReasonResolved -= HandleFailReasonResolved;
            GameEvents.OnFailReasonResolved += HandleFailReasonResolved;
        }

        private void OnDestroy()
        {
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnFailReasonResolved -= HandleFailReasonResolved;
        }

        // ---- Assinaturas do próprio bus (missão Nota 10): publicadores vivem no Gameplay ----

        private void HandleComboEarned(ComboEarned combo)
        {
            _runComboCount++;
            _runComboBonusCoins += combo.bonusCoins;
        }

        private void HandleFailReasonResolved(FailReason reason)
        {
            _runFailReason = reason;   // último veredito vence (Gameplay publica 1× por wipe)
        }

        public void StartLevel(LevelConfigSO level)
        {
            CurrentLevel = level;
            _endSelectionDone = false;
            LastDefeatReason = DefeatReason.None;   // nova fase: motivo de derrota zerado
            _runComboCount = 0;                     // acumuladores da missão Nota 10: vida = 1 fase
            _runComboBonusCoins = 0;
            _runFailReason = FailReason.None;
            LevelStarted?.Invoke(level.levelIndex);
            ChangeState(GameState.BossScout);      // cartão de ~2 s ANTES da corrida (CANON §3.1)
        }

        /// <summary>
        /// Reinicia a fase a partir de QUALQUER estado (ferramenta de dev: showcase/QA jump de
        /// fase). StartLevel sozinho falha se o estado atual não alcança BossScout pela tabela
        /// (ex.: Running/BossFight/ReviveOffer→BossScout é ilegal), então a troca de mundo no
        /// showcase morria em silêncio e tudo saía com o visual do W1. Aqui a pilha é zerada
        /// para o estado base (mesmo efeito de uma cena recém-carregada) e o fluxo refaz
        /// MainMenu→BossScout por transições LEGAIS — sem tocar na tabela do GameStateStack nem
        /// no fluxo de produção (Victory/Defeat→BossScout continua sendo o caminho normal de
        /// "próxima fase"). Não dispara ResolveEnd/recompensa: é só um pulo visual.
        /// </summary>
        public void RestartLevelFromAnyState(LevelConfigSO level)
        {
            if (level == null) return;
            ExitStatesBeforeReset();               // cleanup dos donos (BossManager solta a view, etc.)
            _states = new GameStateStack();        // volta à base (Boot) — espelha uma cena nova
            ChangeState(GameState.MainMenu);       // Boot→MainMenu (legal)
            StartLevel(level);                     // MainMenu→BossScout→… (legal)
        }

        /// <summary>
        /// Abandono mid-run (PAUSA→MENU/REINICIAR) zera a pilha SEM passar por ChangeState, então
        /// o ExitState(BossFight/Running) normal nunca dispara e os donos do estado (BossManager
        /// soltando a view pooled do boss, CameraRig limpando o enquadramento) ficam sem cleanup —
        /// boss-fantasma vivo e câmera travada na nova corrida (mesma cena, soft reset). Aqui
        /// drenamos a pilha de cima p/ baixo disparando StateExited de cada nível ANTES de recriá-la,
        /// para que cada assinante faça sua limpeza. Os 3 assinantes de StateExited são idempotentes
        /// (BossManager.ReleaseView, BossHudController.Hide, UIManager só age em ReviveOffer).
        /// </summary>
        private void ExitStatesBeforeReset()
        {
            if (_states == null) return;
            while (_states.Depth > 1)              // overlays (ex.: ReviveOffer sobre BossFight)
                ExitState(_states.Pop());
            ExitState(_states.Current);            // estado base da corrida (BossFight/Running/…)
        }

        /// <summary>
        /// Vai para o MainMenu a partir de QUALQUER estado (abandono pela PAUSA — botão MENU do
        /// PauseOverlay). Running/BossFight→MainMenu NÃO está na tabela do GameStateStack (só
        /// Victory/Defeat→MainMenu é legal — "voltar ao menu" pós-fase), então um ChangeState
        /// cru mid-run logaria transição ilegal e o estado ficaria preso. Aqui a pilha é zerada
        /// para a base (espelha uma cena nova) e segue Boot→MainMenu por transição LEGAL — mesmo
        /// padrão do RestartLevelFromAnyState. Não dispara ResolveEnd/recompensa: é abandono.
        /// </summary>
        public void GoToMainMenuFromAnyState()
        {
            ExitStatesBeforeReset();               // mesmo cleanup do restart (solta a view do boss, etc.)
            _states = new GameStateStack();        // volta à base (Boot)
            ChangeState(GameState.MainMenu);       // Boot→MainMenu (legal)
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
            int runCoins = 0;
            int runXp = 0;
            if (RunSnapshot != null)
            {
                (runCoins, runXp) = RunSnapshot();
                result.runCoins = runCoins;
                result.runXp = runXp;
            }

            // TOTAL ganho na fase, para a tela de resultado EXIBIR (não credita nada — o
            // crédito é feito UMA vez por RunCommitter/LevelRewardGranter abaixo). Vitória:
            // recompensa de fase (CANON §8) + moedas da corrida; derrota: moedas descartadas,
            // delta 0, mas a XP ganha aparece sempre.
            int levelReward = won && LevelRewardProvider != null ? LevelRewardProvider(levelIndex) : 0;
            result.coinsAwarded = won ? (long)levelReward + runCoins : 0L;
            result.xpAwarded = runXp;

            // Missão Nota 10: combos e fail reason já chegaram pelo bus ANTES desta resolução
            // (ComboEarned na morte do boss; FailReasonResolved no wipe) — mesmo padrão dos
            // campos *Awarded acima: preencher AQUI, antes do RaiseLevelFinished. O bônus de
            // combo NÃO é creditado neste passo: ele entrou na RunWallet (EconomySystem)
            // durante a luta e já veio dentro de runCoins — comboBonusCoins é INFORMATIVO,
            // a tela detalha "dos quais X vieram de combos" sem pagar 2×.
            result.comboCount = _runComboCount;
            result.comboBonusCoins = _runComboBonusCoins;
            result.failReason = won ? FailReason.None : _runFailReason;   // vitória NUNCA carrega motivo

            if (RunCommitter != null) RunCommitter(won);                       // RunWallet → carteira (§4.6); XP sempre
            if (won && LevelRewardGranter != null) LevelRewardGranter(levelIndex);
            if (LevelEndRecorder != null) LevelEndRecorder(result);            // save imediato pós-fase
            GameEvents.RaiseLevelFinished(result);                             // UI/Ads/Analytics reagem
        }
    }

    /// <summary>Motivo da derrota exibido na ResultScreen (doc 09 §4.5).</summary>
    public enum DefeatReason
    {
        None = 0,       // vitória / sem derrota
        ArmyWiped = 1,  // exército zerou durante a corrida ("Exército eliminado")
        BossWon = 2     // exército zerou na arena do boss ("O boss venceu")
    }
}
