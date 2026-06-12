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
    /// telas de vitória/derrota) em Build\shots com nomes ordenáveis (shot_010_menu.png…).
    /// Application.Quit() 3 s após o fim da fase ou no watchdog de 120 s.
    /// Sem o argumento, o componente é 100% inerte (pode viver na cena Game de produção).
    /// </summary>
    public class DevScreenshotRig : MonoBehaviour
    {
        private const string Flag = "-screenshotRun";
        private const string DirArgPrefix = "-shotsDir=";
        private const string DefaultShotsDir = @"C:\Users\Felipe\Downloads\jogo test\Build\shots";
        private const string GameSceneName = "Game";

        [SerializeField] private int _width = 540;
        [SerializeField] private int _height = 960;
        [SerializeField] private float _intervalSeconds = 2f;
        [SerializeField] private float _watchdogSeconds = 120f;
        [SerializeField] private float _quitAfterFinishSeconds = 3f;

        private static DevScreenshotRig s_active;
        private static LevelConfigSO s_pendingLevel;   // sobrevive ao load da cena Game

        private string _outputDir = DefaultShotsDir;
        private int _shotIndex = 10;        // shot_010_..., shot_020_... — sempre ordenável
        private bool _firstGateCaptured;
        private bool _finished;
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
            if (!HasFlag() || s_active != null) return;
            var go = new GameObject("[DevScreenshotRig]");
            go.AddComponent<DevScreenshotRig>();
        }

        private static bool HasFlag()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase)) return true;
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
            if (!HasFlag())
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
        }

        private void UnsubscribeGameManager()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateEntered -= HandleStateEntered;
        }

        // ------------------------------------------------------------------ roteiro

        private IEnumerator DriveRoutine()
        {
            Directory.CreateDirectory(_outputDir);
            Debug.Log("[DevScreenshotRig] ativo — salvando em " + _outputDir);

            // 1. espera o composition root (Boot) terminar
            while (GameManager.Instance == null || GameManager.Instance.State == GameState.Boot)
                yield return new WaitForSecondsRealtime(0.25f);
            SubscribeGameManager();

            // 2. menu (se o fluxo passou por ele) → captura e pula direto para a fase 1
            if (GameManager.Instance.State == GameState.MainMenu)
            {
                yield return new WaitForSecondsRealtime(0.5f);   // menu renderizado
                Capture("menu");
                yield return StartLevelOneRoutine();
            }

            EnsureAutoPilot();

            // 3. captura periódica a cada 2 s até o fim (watchdog 120 s)
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
            _finished = true;
            _finishedAt = Time.realtimeSinceStartup;
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

                string file = string.Format("shot_{0:000}_{1}.png", _shotIndex, tag);
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
