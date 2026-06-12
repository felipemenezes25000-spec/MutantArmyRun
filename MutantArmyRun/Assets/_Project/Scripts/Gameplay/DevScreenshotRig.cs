using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Autoteste VISUAL headless-friendly: com o argumento de linha de comando
    /// -screenshotRun, pula o menu (auto-start da fase 1 com AutoPilot ON) e captura
    /// screenshots OFFSCREEN — câmera renderiza num RenderTexture próprio (RenderRequest
    /// do SRP; fallback Camera.Render no built-in) e ReadPixels → PNG 540×960. NUNCA usa
    /// ScreenCapture.CaptureScreenshot: ela depende do backbuffer da janela e falha
    /// minimizada/em segundo plano (regra dura: o desktop do usuário é intocável).
    /// Captura a cada 2 s + momentos-chave (Boss Scout, 1º portal, arena, golpe final,
    /// telas de vitória/derrota) em Build\shots com nomes ordenáveis e ROTULADOS PELA FASE
    /// (shot_010_L1_menu.png, shot_NNN_L2_portal.png…). Joga VÁRIAS fases em sequência
    /// (1→2→3→4): ao VENCER, avança para a próxima via StartLevel (soft reset, mesma cena)
    /// e segue capturando — as fases 2–4 têm portais de classe/elemento/mutação curados,
    /// então os shots mostram o exército TRANSFORMADO e as mutações ativas.
    /// Application.Quit() ao terminar a última fase (ou na derrota) ou no watchdog de 180 s.
    /// Sem o argumento, o componente é 100% inerte (pode viver na cena Game de produção).
    ///
    /// MODO ALTERNATIVO -showcaseRun: em vez de jogar até vencer, faz um TOUR pela FASE 1 de
    /// cada um dos 10 mundos (índices 1, 11, 21, …, 91), deixa cada um rodar ~3 s com AutoPilot,
    /// captura 1–2 screenshots por mundo ROTULADOS PELO MUNDO (shot_NNN_W1_…, shot_NNN_W5_…) e
    /// passa pro próximo, encerrando ao fim (watchdog 90 s). Permite VER os 10 mundos sem jogar
    /// tudo. Os dois modos são mutuamente exclusivos; -screenshotRun (4 fases) segue intacto.
    /// </summary>
    public class DevScreenshotRig : MonoBehaviour
    {
        private const string Flag = "-screenshotRun";
        private const string ShowcaseFlag = "-showcaseRun";   // tour dos 10 mundos (não joga até vencer)
        private const string DirArgPrefix = "-shotsDir=";
        private const string DefaultShotsDir = @"C:\Users\Felipe\Downloads\jogo test\Build\shots";
        private const string GameSceneName = "Game";

        private const int FirstLevelIndex = 1;
        private const int LevelsToPlay = 4;   // joga 1→2→3→4 antes de encerrar (variedade visual)

        // Showcase: fase 1 de cada um dos 10 mundos (10 fases/mundo → 1, 11, 21, ... 91).
        private const int WorldCount = 10;
        private const int LevelsPerWorld = 10;
        private const float ShowcaseDwellSeconds = 3f;     // ~3 s rodando por mundo
        private const float ShowcaseWatchdogSeconds = 90f; // 10 mundos × ~6 s + folga

        [SerializeField] private int _width = 540;
        [SerializeField] private int _height = 960;
        [SerializeField] private float _intervalSeconds = 2f;
        [SerializeField] private float _watchdogSeconds = 180f;   // 4 fases + folga (era 120 s p/ 1 fase)
        [SerializeField] private float _quitAfterFinishSeconds = 3f;
        [SerializeField] private float _advanceDelaySeconds = 1.2f;   // deixa a vitória renderar antes de avançar

        private static DevScreenshotRig s_active;
        private static LevelConfigSO s_pendingLevel;   // sobrevive ao load da cena Game

        private string _outputDir = DefaultShotsDir;
        private int _shotIndex = 10;        // shot_010_..., shot_020_... — sempre ordenável
        private int _currentLevelIndex = FirstLevelIndex;   // rotula os shots e dirige o avanço de fase
        private int _levelsCleared;         // quantas fases já vencidas nesta sessão
        private bool _firstGateCaptured;
        private bool _finished;
        private bool _showcase;             // modo -showcaseRun (tour dos mundos)
        private int _showcaseWorld = 1;     // mundo atual do tour (rotula os shots W1..W10)
        private float _finishedAt = -1f;
        private readonly List<Canvas> _convertedCanvases = new List<Canvas>(8);

        // ------------------------------------------------------------------ bootstrap

        /// <summary>
        /// O fluxo real começa em Boot→Main, ANTES da cena Game existir: este hook cria o
        /// driver persistente logo após a 1ª cena quando o flag está presente — é ele quem
        /// pula o menu. A cópia colocada na cena Game pelo JuiceFactory vira no-op (guard).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapIfRequested()
        {
            if (!HasAnyFlag() || s_active != null) return;
            var go = new GameObject("[DevScreenshotRig]");
            go.AddComponent<DevScreenshotRig>();
        }

        /// <summary>True se -screenshotRun OU -showcaseRun está presente.</summary>
        private static bool HasAnyFlag()
        {
            return HasArg(Flag) || HasArg(ShowcaseFlag);
        }

        private static bool HasArg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ResolveShotsDir()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i].StartsWith(DirArgPrefix, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(DirArgPrefix.Length).Trim('"');
            return DefaultShotsDir;
        }

        private void Awake()
        {
            if (!HasAnyFlag())
            {
                enabled = false;     // produção: componente presente, efeito zero
                return;
            }
            if (s_active != null && s_active != this)
            {
                enabled = false;     // driver persistente já existe (bootstrap estático)
                return;
            }

            s_active = this;
            // -showcaseRun tem prioridade se ambos vierem; -screenshotRun é o default histórico.
            _showcase = HasArg(ShowcaseFlag);
            if (_showcase) _watchdogSeconds = ShowcaseWatchdogSeconds;
            transform.SetParent(null, false);          // DontDestroyOnLoad exige root
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;        // captura segue com janela em 2º plano
            _outputDir = ResolveShotsDir();
        }

        private void OnEnable()
        {
            if (s_active != this) return;
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            StartCoroutine(DriveRoutine());
        }

        private void OnDisable()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            UnsubscribeGameManager();
            if (s_active == this) s_active = null;
        }

        private void SubscribeGameManager()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.StateEntered -= HandleStateEntered;
            GameManager.Instance.StateEntered += HandleStateEntered;
            GameManager.Instance.LevelStarted -= HandleLevelStarted;
            GameManager.Instance.LevelStarted += HandleLevelStarted;
        }

        private void UnsubscribeGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
            }
        }

        // fonte da verdade do rótulo de fase: qualquer StartLevel atualiza o índice dos shots
        private void HandleLevelStarted(int levelIndex)
        {
            _currentLevelIndex = levelIndex;
        }

        // ------------------------------------------------------------------ roteiro

        private IEnumerator DriveRoutine()
        {
            Directory.CreateDirectory(_outputDir);
            Debug.Log("[DevScreenshotRig] ativo (" + (_showcase ? "showcase" : "screenshot") +
                      ") — salvando em " + _outputDir);

            // 1. espera o composition root (Boot) terminar
            while (GameManager.Instance == null || GameManager.Instance.State == GameState.Boot)
                yield return new WaitForSecondsRealtime(0.25f);
            SubscribeGameManager();

            // Modo showcase: tour pela fase 1 de cada mundo (não joga até vencer) — caminho
            // totalmente separado do -screenshotRun, que segue intacto abaixo.
            if (_showcase)
            {
                yield return ShowcaseRoutine();
                Debug.Log("[DevScreenshotRig] fim do showcase — encerrando o player.");
                Quit();
                yield break;
            }

            // 2. menu (se o fluxo passou por ele) → captura e pula direto para a fase 1
            if (GameManager.Instance.State == GameState.MainMenu)
            {
                yield return new WaitForSecondsRealtime(0.5f);   // menu renderizado
                Capture("menu");
                yield return StartLevelOneRoutine();
            }

            // o load da cena Game troca a instância do GameManager — re-assina no objeto vivo
            // (StateEntered/LevelStarted) para dirigir o avanço de fase e rotular os shots
            SubscribeGameManager();
            EnsureAutoPilot();

            // 3. captura periódica a cada 2 s até o fim (watchdog 180 s — cobre 4 fases)
            float lastPeriodic = Time.realtimeSinceStartup;
            while (true)
            {
                float now = Time.realtimeSinceStartup;
                if (now >= _watchdogSeconds)
                {
                    Capture("timeout");
                    break;
                }
                if (_finished && now - _finishedAt >= _quitAfterFinishSeconds) break;
                if (!_finished && now - lastPeriodic >= _intervalSeconds)
                {
                    lastPeriodic = now;
                    Capture("run");
                }
                yield return null;
            }

            Debug.Log("[DevScreenshotRig] fim do roteiro — encerrando o player.");
            Quit();
        }

        // ------------------------------------------------------------------ showcase (tour dos 10 mundos)

        /// <summary>
        /// Tour VISUAL pelos 10 mundos: carrega a FASE 1 de cada mundo (índices 1, 11, 21, …, 91)
        /// em sequência, deixa rodar ~3 s com AutoPilot e captura 1–2 screenshots por mundo
        /// (shot_NNN_W1_…, shot_NNN_W5_…). Não joga até vencer — só passeia pelos cenários para
        /// eu VER os 10 mundos sem jogar tudo. O 1º mundo passa pelo load da cena Game (vindo do
        /// menu); os demais reusam a cena viva via StartLevel (soft reset). Encerra ao fim.
        /// </summary>
        private IEnumerator ShowcaseRoutine()
        {
            GameSettingsSO settings = GameSettingsSO.Load();
            if (settings == null)
            {
                Debug.LogError("[DevScreenshotRig] catálogo Resources/GameSettings ausente — showcase abortado.");
                yield break;
            }

            // pula o menu, se presente
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu)
                yield return new WaitForSecondsRealtime(0.4f);

            float watchdogStart = Time.realtimeSinceStartup;

            for (int world = 1; world <= WorldCount; world++)
            {
                if (Time.realtimeSinceStartup - watchdogStart >= _watchdogSeconds)
                {
                    Debug.LogWarning("[DevScreenshotRig] watchdog do showcase — encerrando cedo.");
                    break;
                }

                int firstLevelOfWorld = (world - 1) * LevelsPerWorld + 1;   // 1, 11, 21, …, 91
                LevelConfigSO level = settings.GetLevel(firstLevelOfWorld);
                if (level == null)
                {
                    Debug.LogWarning("[DevScreenshotRig] fase " + firstLevelOfWorld +
                                     " (mundo " + world + ") ausente — pulando no showcase.");
                    continue;
                }

                _showcaseWorld = world;
                _currentLevelIndex = firstLevelOfWorld;
                _firstGateCaptured = false;

                // Troca de mundo na cena viva via RestartLevelFromAnyState (1º mundo ainda
                // precisa do load da cena Game, vindo do menu/Boot). O StartLevel cru NÃO serve
                // aqui: ele dispara ChangeState(...→BossScout), ILEGAL a partir de Running/
                // BossFight/ReviveOffer (tabela do GameStateStack) — a transição falhava em
                // silêncio, o LevelManager.BeginRun nunca rodava e TODOS os mundos saíam com a
                // pista/props/atmosfera do W1. RestartLevelFromAnyState zera a pilha de estados
                // (como uma cena nova) e refaz MainMenu→BossScout→Running por transições legais,
                // disparando BeginRun (segmentos/props do mundo) e WorldAtmosphereApplier (céu/
                // fog/sol do mundo) de verdade a cada mundo.
                bool sceneAlreadyLoaded = SceneManager.GetActiveScene().name == GameSceneName
                                          && GameManager.Instance != null;
                if (!sceneAlreadyLoaded)
                {
                    yield return LoadGameSceneWithLevelRoutine(level);
                }
                else
                {
                    GameManager.Instance.RestartLevelFromAnyState(level);
                    yield return null;
                }

                SubscribeGameManager();
                EnsureAutoPilot();

                // espera ENTRAR EM RUNNING antes de capturar: o StartLevel passa ~2 s no cartão
                // BossScout (UIManager.BossScoutSeconds) e só então transita para Running, quando
                // BeginRun monta a pista e a atmosfera do mundo. Capturar antes pegaria o cartão
                // sobre cena vazia. Watchdog curto evita travar se o Running não chegar.
                float runWaitStart = Time.realtimeSinceStartup;
                while ((GameManager.Instance == null || GameManager.Instance.State != GameState.Running) &&
                       Time.realtimeSinceStartup - runWaitStart < 6f)
                    yield return null;

                // deixa a corrida rodar ~3 s JÁ em Running, capturando 2 momentos (entrada da
                // corrida + meio) — ambos com a pista/céu/props corretos do mundo atual.
                float dwellStart = Time.realtimeSinceStartup;
                ScheduleCapture("run", 0.8f);                 // 1º shot: cenário + exército inicial
                bool secondShotTaken = false;
                while (Time.realtimeSinceStartup - dwellStart < ShowcaseDwellSeconds)
                {
                    if (!secondShotTaken && Time.realtimeSinceStartup - dwellStart >= ShowcaseDwellSeconds * 0.6f)
                    {
                        secondShotTaken = true;
                        Capture("run2");                      // 2º shot: exército já maior, mundo cheio
                    }
                    yield return null;
                }
            }
        }

        /// <summary>Carrega a cena Game e inicia o nível dado (mesmo mecanismo do StartLevelOneRoutine).</summary>
        private IEnumerator LoadGameSceneWithLevelRoutine(LevelConfigSO level)
        {
            s_pendingLevel = level;
            SceneManager.sceneLoaded += HandleGameSceneLoaded;
            AsyncOperation load = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone) yield return null;
            yield return null;   // 1 frame para o GameSceneBootstrap registrar os managers
        }

        // mesmo caminho do botão JOGAR (MainMenuController), sem depender da camada UI:
        // resolve a fase 1 no catálogo de Resources e chama StartLevel após o load da Game
        private IEnumerator StartLevelOneRoutine()
        {
            GameSettingsSO settings = GameSettingsSO.Load();
            LevelConfigSO level = settings != null ? settings.GetLevel(1) : null;
            if (level == null)
            {
                Debug.LogError("[DevScreenshotRig] fase 1 ausente no catálogo Resources/GameSettings — abortando.");
                _finished = true;
                _finishedAt = Time.realtimeSinceStartup;
                yield break;
            }

            s_pendingLevel = level;
            SceneManager.sceneLoaded += HandleGameSceneLoaded;
            AsyncOperation load = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone) yield return null;
            yield return null;   // 1 frame para o GameSceneBootstrap registrar os managers
        }

        private static void HandleGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSceneName) return;
            SceneManager.sceneLoaded -= HandleGameSceneLoaded;
            LevelConfigSO level = s_pendingLevel;
            s_pendingLevel = null;
            if (level != null && GameManager.Instance != null)
                GameManager.Instance.StartLevel(level);
        }

        private void EnsureAutoPilot()
        {
            AutoPilot pilot = FindFirstObjectByType<AutoPilot>(FindObjectsInactive.Include);
            if (pilot == null) pilot = gameObject.AddComponent<AutoPilot>();
            pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 12345);
            pilot.Active = true;
        }

        // ------------------------------------------------------------------ momentos-chave

        private void HandleStateEntered(GameState state)
        {
            switch (state)
            {
                case GameState.BossScout:
                    _firstGateCaptured = false;        // nova corrida: 1º portal volta a contar
                    ScheduleCapture("bossscout", 0.6f);
                    break;
                case GameState.BossFight:
                    EnsureAutoPilot();                 // pós-load: garante o piloto na cena viva
                    ScheduleCapture("arena", 0.5f);
                    break;
                case GameState.Victory:
                    Capture("golpe_final");            // slow motion canônico ainda ativo
                    ScheduleCapture("vitoria", 1.2f);
                    break;
                case GameState.Defeat:
                    ScheduleCapture("derrota", 1.0f);
                    break;
            }
        }

        private void HandleGateConsumed(GateResult result)
        {
            if (_firstGateCaptured) return;
            _firstGateCaptured = true;
            ScheduleCapture("portal", 0.15f);
        }

        private void HandleLevelFinished(LevelResult result)
        {
            // Showcase: o tour dirige a sequência de mundos por conta própria (ShowcaseRoutine) —
            // ignora o avanço por vitória/encerramento do screenshot run.
            if (_showcase) return;

            // Vitória e ainda há fases a mostrar: AVANÇA (1→2→3→4) em vez de encerrar — captura
            // o exército transformado das fases curadas. Derrota OU última fase: encerra.
            if (result.won) _levelsCleared++;
            bool moreToPlay = result.won && _levelsCleared < LevelsToPlay;
            if (moreToPlay)
            {
                int nextIndex = result.levelIndex + 1;
                if (isActiveAndEnabled) StartCoroutine(AdvanceToLevelRoutine(nextIndex));
                return;
            }

            _finished = true;
            _finishedAt = Time.realtimeSinceStartup;
        }

        // Avança para a próxima fase pelo MESMO caminho do "próxima fase" (StartLevel, soft reset
        // na mesma cena — doc 12 §2.2). Não depende de UI. Mantém o AutoPilot e segue capturando.
        private IEnumerator AdvanceToLevelRoutine(int nextIndex)
        {
            yield return new WaitForSecondsRealtime(_advanceDelaySeconds);   // deixa a vitória renderar/capturar

            GameSettingsSO settings = GameSettingsSO.Load();
            LevelConfigSO level = settings != null ? settings.GetLevel(nextIndex) : null;
            if (level == null || GameManager.Instance == null)
            {
                Debug.LogWarning("[DevScreenshotRig] fase " + nextIndex + " ausente — encerrando o roteiro.");
                _finished = true;
                _finishedAt = Time.realtimeSinceStartup;
                yield break;
            }

            _currentLevelIndex = nextIndex;     // novos shots já saem rotulados com a fase nova
            _firstGateCaptured = false;         // 1º portal da nova fase volta a contar
            GameManager.Instance.StartLevel(level);
            yield return null;
            EnsureAutoPilot();                  // garante o piloto ativo na corrida recém-iniciada
        }

        private void ScheduleCapture(string tag, float delaySeconds)
        {
            if (isActiveAndEnabled) StartCoroutine(ScheduledCaptureRoutine(tag, delaySeconds));
        }

        private IEnumerator ScheduledCaptureRoutine(string tag, float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            Capture(tag);
        }

        // ------------------------------------------------------------------ captura offscreen

        private void Capture(string tag)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[DevScreenshotRig] sem Camera.main — captura '" + tag + "' pulada.");
                return;
            }

            RenderTexture rt = RenderTexture.GetTemporary(_width, _height, 24);
            Texture2D tex = null;
            try
            {
                // canvases ScreenSpaceOverlay não entram em render de RT: converte para
                // ScreenSpaceCamera só durante a captura (mesmo aspecto 9:16) e restaura
                ConvertOverlayCanvases(cam);
                RenderCameraToTexture(cam, rt);
                RestoreOverlayCanvases();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(_width, _height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, _width, _height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = previous;

                // rótulo no nome: showcase rotula pelo MUNDO (shot_NNN_W5_run.png — me deixa VER
                // os 10 mundos); o screenshot run rotula pela FASE (shot_NNN_L2_portal.png).
                string label = _showcase ? "W" + _showcaseWorld : "L" + _currentLevelIndex;
                string file = string.Format("shot_{0:000}_{1}_{2}.png", _shotIndex, label, tag);
                _shotIndex += 10;
                File.WriteAllBytes(Path.Combine(_outputDir, file), tex.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DevScreenshotRig] captura '" + tag + "' falhou: " + e.Message);
            }
            finally
            {
                if (tex != null) Destroy(tex);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // URP: Camera.Render() direto não é suportado por SRP — o caminho oficial é o
        // RenderRequest (StandardRequest com destination). Fallback para built-in.
        private static void RenderCameraToTexture(Camera cam, RenderTexture rt)
        {
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
                return;
            }

            RenderTexture previousTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previousTarget;
        }

        private void ConvertOverlayCanvases(Camera cam)
        {
            _convertedCanvases.Clear();
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                _convertedCanvases.Add(c);
            }
        }

        private void RestoreOverlayCanvases()
        {
            for (int i = 0; i < _convertedCanvases.Count; i++)
            {
                Canvas c = _convertedCanvases[i];
                if (c == null) continue;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.worldCamera = null;
            }
            _convertedCanvases.Clear();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
