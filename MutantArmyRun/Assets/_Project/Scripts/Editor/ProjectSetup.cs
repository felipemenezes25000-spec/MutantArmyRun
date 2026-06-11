using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Gameplay;
using MutantArmy.Meta;
using MutantArmy.Services;
using MutantArmy.UI;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Setup Project — cria as 3 cenas canônicas (Boot/Main/Game, doc 12 §2.2)
    /// por código e COSTURA o loop completo: prefina o [Services] com os managers
    /// persistentes na ordem do composition root (doc 12 §3.3) + overlays persistentes
    /// (Boss Scout/Revive) ligados ao UIManager; menu da Main com botão JOGAR real
    /// (MainMenuController); HUD/ResultScreen/GameUIController na cena Game com todos os
    /// campos serializados ligados aos assets do MvpContentFactory. Idempotente: rodar
    /// de novo recria as cenas e re-liga tudo.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string SoRoot = "Assets/_Project/ScriptableObjects";

        // Cores canônicas de UI (doc 09): ciano positivo, âmbar de aviso, cinza neutro.
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);
        private static readonly Color Amber = new Color(1.00f, 0.75f, 0.15f);
        private static readonly Color PanelDark = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        private static readonly Color ButtonGrey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);

        [MenuItem("MAR Tools/Setup Project")]
        public static void SetupProject()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Setup Project não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureTmpEssentials();      // TMP Settings + fonte default — sem isso TMP_Text nasce sem fonte
            EnsureFolder(ScenesFolder);
            EnsureGameSettingsAsset();  // catálogo de fases em Resources (doc 12 §2.1) p/ o botão Jogar

            string bootPath = CreateBootScene();
            string mainPath = CreateMainScene();
            string gamePath = CreateGameScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(bootPath, true),
                new EditorBuildSettingsScene(mainPath, true),
                new EditorBuildSettingsScene(gamePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("MAR Tools: cenas Boot/Main/Game criadas, UI ligada e Build Settings atualizado.");
        }

        // ------------------------------------------------------------------ Boot

        private static string CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera("Main Camera", new Vector3(0f, 1f, -10f), Quaternion.identity);
            CreateEventSystem();

            // ---- [Services]: composition root + managers persistentes (doc 12 §3.3) ----
            // Ordem canônica: Save → RemoteConfig → Analytics → Ads/IAP → Economy →
            // Upgrade → Unit → Reward → Audio → UI (+ GameManager).
            var services = new GameObject("[Services]");
            var bootstrap = services.AddComponent<GameBootstrap>();

            // Providers Null no próprio root: GameBootstrap resolve por
            // GetComponentInChildren<I*Provider> — sem eles o boot loga erro e fica
            // sem Remote Config, ads e analytics (doc 12 §3.3/§7.3).
            services.AddComponent<NullRemoteConfigProvider>();
            services.AddComponent<NullAnalyticsProvider>();
            services.AddComponent<NullAdsProvider>();

            var save = AddManager<SaveSystem>(services, "SaveSystem");
            var remoteConfig = AddManager<RemoteConfigManager>(services, "RemoteConfigManager");
            var analytics = AddManager<AnalyticsManager>(services, "AnalyticsManager");
            var ads = AddManager<AdsManager>(services, "AdsManager");
            var iap = AddManager<IAPManager>(services, "IAPManager");
            var economy = AddManager<EconomySystem>(services, "EconomySystem");
            var upgrade = AddManager<UpgradeSystem>(services, "UpgradeSystem");
            var unit = AddManager<UnitManager>(services, "UnitManager");
            var reward = AddManager<RewardSystem>(services, "RewardSystem");
            var audio = AddManager<AudioManager>(services, "AudioManager");
            var gameManager = AddManager<GameManager>(services, "GameManager");
            var ui = AddManager<UIManager>(services, "UIManager");

            // Catálogos de meta (campos serializados REAIS): sem eles UnitManager e
            // UpgradeSystem nascem vazios e progressão/upgrades viram no-op.
            WireSerializedArray(unit, "_catalog", LoadAllAssets<UnitConfigSO>(SoRoot + "/Units"));
            WireSerializedArray(upgrade, "_trackConfigs", LoadAllAssets<UpgradeConfigSO>(SoRoot + "/Upgrades"));

            // Fontes de áudio do prefab [Services] (música + SFX) — campos do AudioManager.
            AudioSource musicSource = audio.gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            AudioSource sfxSource = audio.gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            WireSerializedField(audio, "_musicSource", musicSource);
            WireSerializedField(audio, "_sfxSource", sfxSource);

            // Canvas persistente da UI com root de safe area para o UIManager (doc 12 §4.13).
            // sortingOrder alto: overlays persistentes (Boss Scout/Revive) ficam SOBRE o
            // HUD da cena Game (canvases separados empatariam em 0).
            GameObject uiCanvas = CreateCanvas("UICanvas", services.transform, sortingOrder: 100);
            var safeRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
            var safeRect = (RectTransform)safeRoot.transform;
            safeRect.SetParent(uiCanvas.transform, false);
            StretchFull(safeRect);
            WireSerializedField(ui, "_root", safeRect);

            // OVL-01 Boss Scout + OVL-05 Revive: persistentes (vivem no canvas do [Services])
            // e ligados aos campos reais do UIManager — sem isso o cartão é pulado com
            // warning e o revive é recusado automaticamente.
            BossScoutOverlay bossScout = BuildBossScoutOverlay(safeRect);
            WireSerializedField(ui, "_bossScout", bossScout);
            ReviveOverlay revive = BuildReviveOverlay(safeRect);
            WireSerializedField(ui, "_reviveOffer", revive);

            // Campos REAIS do GameBootstrap: _gameManager, _saveService e o array
            // _managersInOrder na ordem canônica do composition root (doc 12 §3.3).
            WireSerializedField(bootstrap, "_gameManager", gameManager);
            WireSerializedField(bootstrap, "_saveService", save);
            WireSerializedArray(bootstrap, "_managersInOrder", new Component[]
            {
                remoteConfig, analytics, ads, iap, economy, upgrade, unit, reward, audio, ui
            });

            // Splash: orçamento de boot de 2,5 s sem visual além disto (doc 12 §2.2).
            GameObject splash = CreateCanvas("SplashCanvas", null);
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(splash.transform, false);
            StretchFull(bgRect);
            bg.GetComponent<Image>().color = Color.black;
            CreateTmpLabel(splash.transform, "Title", "MUTANT ARMY RUN", 64f,
                           TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(900f, 200f));

            string path = ScenesFolder + "/Boot.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ Main

        private static string CreateMainScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera("Main Camera", new Vector3(0f, 1f, -10f), Quaternion.identity);
            CreateDirectionalLight();
            CreateEventSystem();

            GameObject canvas = CreateCanvas("MainCanvas", null);
            var menu = canvas.AddComponent<MainMenuController>();

            CreateTmpLabel(canvas.transform, "Title", "MUTANT ARMY RUN", 84f,
                           TextAlignmentOptions.Center, Anchors.TopCenter, new Vector2(0f, -260f), new Vector2(960f, 220f));

            // Topo: moedas (esquerda) e gemas (direita) — preenchidos pelo controller via save/eventos.
            TMP_Text coins = CreateTmpLabel(canvas.transform, "CoinsText", "0", 44f,
                           TextAlignmentOptions.Left, Anchors.TopLeft, new Vector2(60f, -70f), new Vector2(360f, 70f), Amber);
            TMP_Text gems = CreateTmpLabel(canvas.transform, "GemsText", "0", 44f,
                           TextAlignmentOptions.Right, Anchors.TopRight, new Vector2(-60f, -70f), new Vector2(360f, 70f),
                           new Color(0.55f, 0.85f, 1f));

            TMP_Text levelText = CreateTmpLabel(canvas.transform, "LevelText", "FASE 1", 52f,
                           TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 180f), new Vector2(600f, 90f));

            // Botão JOGAR — grande, central (uGUI Button + TMP).
            TMP_Text playLabel;
            Button play = CreateButton(canvas.transform, "PlayButton", "JOGAR", 64f, Cyan,
                                       Anchors.Middle, new Vector2(0f, -40f), new Vector2(540f, 170f), out playLabel);

            WireSerializedField(menu, "_playButton", play);
            WireSerializedField(menu, "_playLabel", playLabel);
            WireSerializedField(menu, "_coinsText", coins);
            WireSerializedField(menu, "_gemsText", gems);
            WireSerializedField(menu, "_levelText", levelText);

            string path = ScenesFolder + "/Main.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ Game

        private static string CreateGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rig de câmera próprio — jamais filho do player (doc 12 §4.12).
            GameObject cameraGo = CreateCamera("Main Camera",
                new Vector3(0f, 9f, -7f), Quaternion.Euler(45f, 0f, 0f));
            cameraGo.AddComponent<CameraRig>();

            CreateDirectionalLight();
            CreateEventSystem();

            // Líder/âncora da multidão (segue o drag lateral, doc 12 §4.2).
            var anchor = new GameObject("CrowdAnchor").AddComponent<CrowdAnchor>();

            // ---- [GameSystems]: managers de gameplay na ordem Level→Crowd→Gate→Boss→Combat (doc 12 §3.3) ----
            var systems = new GameObject("[GameSystems]");
            var sceneBootstrap = systems.AddComponent<GameSceneBootstrap>();
            var level = AddManager<LevelManager>(systems, "LevelManager");
            var crowd = AddManager<CrowdManager>(systems, "CrowdManager");
            // Sem CrowdRenderer a multidão é INVISÍVEL (Submit é no-op sem Instance);
            // sem RiskResolver o portal de Risco vira no-op (doc 12 §4.3/§6.2).
            var crowdRenderer = AddManager<CrowdRenderer>(systems, "CrowdRenderer");
            var gate = AddManager<GateManager>(systems, "GateManager");
            var boss = AddManager<BossManager>(systems, "BossManager");
            var combat = AddManager<CombatSystem>(systems, "CombatSystem");
            var vfx = AddManager<VFXManager>(systems, "VFXManager");
            var risk = AddManager<RiskResolver>(systems, "RiskResolver");

            // Assets de config nos campos serializados REAIS dos managers: sem _defaultUnit
            // o spawn falha com erro e o exército nunca nasce; sem _chart o multiplicador
            // elemental degrada para 1.0.
            var chart = AssetDatabase.LoadAssetAtPath<ElementChartSO>(SoRoot + "/Balance/ElementChart_Default.asset");
            var soldier = AssetDatabase.LoadAssetAtPath<UnitConfigSO>(SoRoot + "/Units/Unit_Soldier.asset");
            if (chart == null || soldier == null)
                Debug.LogWarning("MAR Tools: assets do MvpContentFactory ausentes — rode " +
                                 "MAR Tools/Create MVP Content e re-rode o Setup Project.");
            WireSerializedField(crowd, "_chart", chart);
            WireSerializedField(crowd, "_defaultUnit", soldier);
            WireSerializedField(combat, "_chart", chart);

            // GateManager: pool de autoBalance (os 8 portais canônicos) + prefab do par.
            // O prefab visual (GatePairView) vem do GreyboxFactory — liga se já existir.
            WireSerializedArray(gate, "_autoBalancePool", LoadAllAssets<GateConfigSO>(SoRoot + "/Gates"));
            GatePairView pairPrefab = FindPrefabWithComponent<GatePairView>("Assets/_Project");
            if (pairPrefab != null)
                WireSerializedField(gate, "_pairPrefab", pairPrefab);
            else
                Debug.LogWarning("MAR Tools: nenhum prefab com GatePairView encontrado — fases ficarão " +
                                 "sem portais até rodar o GreyboxFactory e re-rodar o Setup Project.");

            // Campo REAL do GameSceneBootstrap é o array _managersInOrder — a ordem do
            // array É a ordem de init (doc 12 §3.3). CrowdAnchor entra na fila: o Init()
            // dele cria o trigger-proxy do exército.
            WireSerializedArray(sceneBootstrap, "_managersInOrder", new Component[]
            {
                level, crowd, crowdRenderer, gate, boss, combat, vfx, risk, anchor
            });

            // ---- HUD da corrida (doc 09 §4.2): elementos REAIS ligados ao HudController ----
            GameObject hudCanvas = CreateCanvas("HudCanvas", null, sortingOrder: 10);
            var hud = new GameObject("Hud", typeof(RectTransform));
            ((RectTransform)hud.transform).SetParent(hudCanvas.transform, false);
            StretchFull((RectTransform)hud.transform);
            var hudController = hud.AddComponent<HudController>();
            BuildHudElements(hudController, (RectTransform)hud.transform);

            // Feedbacks canônicos (NICE/GREAT/.../BOSS BREAKER) com o TMP ligado.
            var feedback = new GameObject("FeedbackText", typeof(RectTransform));
            ((RectTransform)feedback.transform).SetParent(hudCanvas.transform, false);
            StretchFull((RectTransform)feedback.transform);
            var feedbackController = feedback.AddComponent<FeedbackTextController>();
            TMP_Text feedbackLabel = CreateTmpLabel(feedback.transform, "Label", string.Empty, 88f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 260f), new Vector2(900f, 160f), Amber);
            feedbackLabel.gameObject.SetActive(false);   // o controller ativa por evento
            WireSerializedField(feedbackController, "_label", feedbackLabel);

            // ---- SCR-04/05 ResultScreen + glue (GameUIController) ----
            ResultScreen resultScreen = BuildResultScreen((RectTransform)hudCanvas.transform);
            var gameUi = hudCanvas.AddComponent<GameUIController>();
            WireSerializedField(gameUi, "_resultScreen", resultScreen);
            WireSerializedField(gameUi, "_feedback", feedbackController);

            string path = ScenesFolder + "/Game.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ UI builders

        private static void BuildHudElements(HudController controller, RectTransform root)
        {
            // Barra de progresso da pista (topo, fina) — OnRunProgress.
            Image progressFill = CreateBar(root, "ProgressBar", Anchors.TopCenter,
                new Vector2(0f, -50f), new Vector2(760f, 22f), Green);

            // Contador de tropas: o número mais importante da tela (doc 09 §4.2).
            TMP_Text unitCount = CreateTmpLabel(root, "UnitCountText", "1", 96f,
                TextAlignmentOptions.Center, Anchors.TopCenter, new Vector2(0f, -150f), new Vector2(420f, 130f));

            // Barra de Supply "52/60" com fill e texto sobreposto.
            Image supplyFill = CreateBar(root, "SupplyBar", Anchors.TopCenter,
                new Vector2(0f, -250f), new Vector2(520f, 34f), Cyan);
            TMP_Text supplyText = CreateTmpLabel(supplyFill.transform.parent, "SupplyText", "0/60", 28f,
                TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(520f, 34f));

            // Carteira persistente (topo-esquerda) + delta da corrida (visual DISTINTO).
            TMP_Text coins = CreateTmpLabel(root, "CoinsText", "0", 40f,
                TextAlignmentOptions.Left, Anchors.TopLeft, new Vector2(50f, -60f), new Vector2(320f, 60f), Amber);
            TMP_Text runCoins = CreateTmpLabel(root, "RunCoinsText", "+0", 34f,
                TextAlignmentOptions.Left, Anchors.TopLeft, new Vector2(50f, -120f), new Vector2(320f, 50f),
                new Color(1f, 0.95f, 0.55f));

            // 3 slots de mutação (CANON §3.3) na lateral esquerda.
            var mutationSlots = new TMP_Text[3];
            for (int i = 0; i < mutationSlots.Length; i++)
            {
                mutationSlots[i] = CreateTmpLabel(root, "MutationSlot" + i, string.Empty, 30f,
                    TextAlignmentOptions.Left, Anchors.MiddleLeft, new Vector2(40f, 120f - 70f * i), new Vector2(340f, 54f),
                    new Color(0.75f, 0.55f, 1f));
            }

            WireSerializedField(controller, "_unitCountText", unitCount);
            WireSerializedField(controller, "_supplyText", supplyText);
            WireSerializedField(controller, "_supplyFill", supplyFill);
            WireSerializedField(controller, "_coinsText", coins);
            WireSerializedField(controller, "_runCoinsText", runCoins);
            WireSerializedField(controller, "_progressFill", progressFill);
            WireSerializedArray(controller, "_mutationSlotTexts", mutationSlots);
        }

        private static ResultScreen BuildResultScreen(RectTransform parent)
        {
            GameObject panel = CreatePanel(parent, "ResultScreen", PanelDark);
            panel.AddComponent<CanvasGroup>();              // UIScreen exige CanvasGroup
            var screen = panel.AddComponent<ResultScreen>();

            TMP_Text title = CreateTmpLabel(panel.transform, "Title", "VITÓRIA!", 76f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 440f), new Vector2(800f, 120f));

            // Selo PERFECT (doc 09 §4.4) — GameObject ligado ao campo _perfectBadge.
            TMP_Text perfect = CreateTmpLabel(panel.transform, "PerfectBadge", "PERFECT!", 44f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 340f), new Vector2(500f, 70f), Amber);
            perfect.gameObject.SetActive(false);

            // O número principal é o DELTA, nunca o total da carteira (doc 12 §4.13).
            TMP_Text coinsDelta = CreateTmpLabel(panel.transform, "CoinsDelta", "+0", 100f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 210f), new Vector2(700f, 140f), Amber);
            TMP_Text xpDelta = CreateTmpLabel(panel.transform, "XpDelta", "+0 XP", 44f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 100f), new Vector2(600f, 70f));
            TMP_Text survivors = CreateTmpLabel(panel.transform, "Survivors", "Sobreviventes: 0", 36f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 20f), new Vector2(700f, 60f));
            TMP_Text damage = CreateTmpLabel(panel.transform, "Damage", "Dano causado: 0", 36f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, -40f), new Vector2(700f, 60f));

            TMP_Text doubleLabel;
            Button doubleButton = CreateButton(panel.transform, "DoubleButton", "DOBRAR ×2", 44f, Green,
                Anchors.Middle, new Vector2(0f, -180f), new Vector2(560f, 120f), out doubleLabel);

            Button nextButton = CreateButton(panel.transform, "NextButton", "PRÓXIMA FASE", 52f, Cyan,
                Anchors.Middle, new Vector2(0f, -340f), new Vector2(560f, 140f), out _);

            Button homeButton = CreateButton(panel.transform, "HomeButton", "MENU", 36f, ButtonGrey,
                Anchors.Middle, new Vector2(0f, -480f), new Vector2(360f, 90f), out _);

            WireSerializedField(screen, "_titleText", title);
            WireSerializedField(screen, "_coinsDeltaText", coinsDelta);
            WireSerializedField(screen, "_xpDeltaText", xpDelta);
            WireSerializedField(screen, "_survivorsText", survivors);
            WireSerializedField(screen, "_damageText", damage);
            WireSerializedField(screen, "_perfectBadge", perfect.gameObject);
            WireSerializedField(screen, "_doubleButton", doubleButton);
            WireSerializedField(screen, "_doubleButtonLabel", doubleLabel);
            WireSerializedField(screen, "_nextButton", nextButton);
            WireSerializedField(screen, "_homeButton", homeButton);

            DeactivateUiRoot(panel);   // nasce oculta; UIManager.Push/Show ativa
            return screen;
        }

        private static BossScoutOverlay BuildBossScoutOverlay(RectTransform parent)
        {
            GameObject panel = CreatePanel(parent, "BossScoutOverlay", new Color(0f, 0f, 0f, 0.88f));
            panel.AddComponent<CanvasGroup>();              // UIOverlay exige CanvasGroup
            var overlay = panel.AddComponent<BossScoutOverlay>();

            // Botão fullscreen invisível: qualquer toque pula o cartão (CANON §3.1).
            var skipGo = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var skipRect = (RectTransform)skipGo.transform;
            skipRect.SetParent(panel.transform, false);
            StretchFull(skipRect);
            var skipImage = skipGo.GetComponent<Image>();
            skipImage.color = Color.clear;                  // invisível mas raycastável
            Button skipButton = skipGo.GetComponent<Button>();
            skipButton.transition = Selectable.Transition.None;

            TMP_Text bossName = CreateTmpLabel(panel.transform, "BossName", "BOSS", 72f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 240f), new Vector2(900f, 110f));
            TMP_Text element = CreateTmpLabel(panel.transform, "Element", "ELEMENTO: —", 44f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 110f), new Vector2(900f, 70f));
            // Fraqueza com NOME do elemento, nunca só cor (doc 09 P7).
            TMP_Text weakness = CreateTmpLabel(panel.transform, "Weakness", "FRACO CONTRA —", 56f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 0f), new Vector2(960f, 90f), Amber);
            TMP_Text hint = CreateTmpLabel(panel.transform, "Hint", string.Empty, 36f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, -130f), new Vector2(900f, 70f),
                new Color(0.8f, 0.85f, 0.9f));

            WireSerializedField(overlay, "_bossNameText", bossName);
            WireSerializedField(overlay, "_elementText", element);
            WireSerializedField(overlay, "_weaknessText", weakness);
            WireSerializedField(overlay, "_hintText", hint);
            WireSerializedField(overlay, "_skipButton", skipButton);

            DeactivateUiRoot(panel);
            return overlay;
        }

        private static ReviveOverlay BuildReviveOverlay(RectTransform parent)
        {
            GameObject panel = CreatePanel(parent, "ReviveOverlay", new Color(0f, 0f, 0f, 0.88f));
            panel.AddComponent<CanvasGroup>();
            var overlay = panel.AddComponent<ReviveOverlay>();

            TMP_Text title = CreateTmpLabel(panel.transform, "Title", "EXÉRCITO DERROTADO", 60f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 220f), new Vector2(960f, 100f));
            CreateTmpLabel(panel.transform, "Subtitle", "Reviver com metade do exército?", 38f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 110f), new Vector2(900f, 70f),
                new Color(0.8f, 0.85f, 0.9f));

            TMP_Text reviveLabel;
            Button reviveButton = CreateButton(panel.transform, "ReviveButton", "REVIVER (ANÚNCIO)", 44f, Green,
                Anchors.Middle, new Vector2(0f, -40f), new Vector2(620f, 130f), out reviveLabel);
            Button declineButton = CreateButton(panel.transform, "DeclineButton", "DESISTIR", 38f, ButtonGrey,
                Anchors.Middle, new Vector2(0f, -200f), new Vector2(420f, 100f), out _);

            WireSerializedField(overlay, "_titleText", title);
            WireSerializedField(overlay, "_reviveButton", reviveButton);
            WireSerializedField(overlay, "_reviveLabel", reviveLabel);
            WireSerializedField(overlay, "_declineButton", declineButton);

            DeactivateUiRoot(panel);
            return overlay;
        }

        // ------------------------------------------------------------------ assets de bootstrap

        /// <summary>
        /// Garante o catálogo de fases em Resources/GameSettings.asset sincronizado com os
        /// LevelConfigSO existentes — mesmo sem re-rodar o MvpContentFactory (idempotente).
        /// </summary>
        private static void EnsureGameSettingsAsset()
        {
            LevelConfigSO[] levels = LoadAllAssets<LevelConfigSO>(SoRoot + "/Levels");
            if (levels.Length == 0)
            {
                Debug.LogWarning("MAR Tools: nenhum LevelConfigSO em " + SoRoot + "/Levels — rode " +
                                 "MAR Tools/Create MVP Content antes de jogar (o botão Jogar precisa do catálogo).");
                return;
            }

            EnsureFolder("Assets/_Project/Resources");
            string assetPath = "Assets/_Project/Resources/" + GameSettingsSO.ResourcesName + ".asset";
            var settings = AssetDatabase.LoadAssetAtPath<GameSettingsSO>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettingsSO>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }

            var sorted = new List<LevelConfigSO>(levels);
            sorted.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
            settings.levels = sorted.ToArray();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// Importa os TMP Essential Resources (TMP Settings + LiberationSans SDF) se ainda
        /// não existem — sem eles TODO TMP_Text nasce sem fonte. No Unity 6 o pacote vive
        /// dentro do com.unity.ugui. O import usa PackageImportUtil.ImportSync (síncrono em
        /// batch); se a fonte não aparecer nesta execução, re-rodar o Setup liga as fontes
        /// explicitamente.
        /// </summary>
        private static void EnsureTmpEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;

            string packagePath = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
                packagePath = Path.GetFullPath("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
            {
                Debug.LogWarning("MAR Tools: TMP Essential Resources.unitypackage não encontrado — " +
                                 "importe via Window/TextMeshPro/Import TMP Essential Resources.");
                return;
            }

            PackageImportUtil.ImportSync(packagePath);
            Debug.Log("MAR Tools: TMP Essential Resources importados (TMP Settings + fonte default).");
        }

        // ------------------------------------------------------------------ helpers

        private static T AddManager<T>(GameObject root, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<T>();
        }

        private static GameObject CreateCamera(string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, rotation);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
            go.AddComponent<AudioListener>();
            return go;
        }

        private static void CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            // 1 directional realtime; sem luzes adicionais em gameplay (doc 12 §2.4).
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // Input clássico (activeInputHandler = 0): StandaloneInputModule, nunca InputSystem.
            go.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreateCanvas(string name, Transform parent, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);   // 9:16 (doc 09 §6)
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        /// <summary>Âncoras nomeadas para posicionamento dos elementos de UI.</summary>
        private static class Anchors
        {
            public static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
            public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
            public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
            public static readonly Vector2 TopRight = new Vector2(1f, 1f);
            public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
        }

        private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
        }

        private static TMP_Text CreateTmpLabel(Transform parent, string name, string content, float fontSize,
                                               TextAlignmentOptions alignment, Vector2 anchor,
                                               Vector2 anchoredPos, Vector2 size)
        {
            return CreateTmpLabel(parent, name, content, fontSize, alignment, anchor, anchoredPos, size, Color.white);
        }

        private static TMP_Text CreateTmpLabel(Transform parent, string name, string content, float fontSize,
                                               TextAlignmentOptions alignment, Vector2 anchor,
                                               Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            PlaceRect(rect, anchor, anchoredPos, size);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;

            // Fonte explícita quando os TMP Essentials já existem; sem ela o TMP cai no
            // defaultFontAsset do TMP Settings em runtime.
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) text.font = font;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, float fontSize,
                                           Color color, Vector2 anchor, Vector2 anchoredPos, Vector2 size,
                                           out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            PlaceRect(rect, anchor, anchoredPos, size);

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;

            labelText = CreateTmpLabel(go.transform, "Label", label, fontSize,
                                       TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, size);
            var labelRect = (RectTransform)labelText.transform;
            StretchFull(labelRect);

            return go.GetComponent<Button>();
        }

        /// <summary>Barra com fundo escuro + fill horizontal; retorna o Image do FILL.</summary>
        private static Image CreateBar(Transform parent, string name, Vector2 anchor,
                                       Vector2 anchoredPos, Vector2 size, Color fillColor)
        {
            var bg = new GameObject(name, typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(parent, false);
            PlaceRect(bgRect, anchor, anchoredPos, size);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.45f);
            bgImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(bg.transform, false);
            StretchFull(fillRect);
            var fillImage = fill.GetComponent<Image>();
            // Image.Type.Filled exige sprite — o builtin UISprite serve de máscara branca.
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            StretchFull(rect);
            go.GetComponent<Image>().color = background;
            return go;
        }

        /// <summary>Telas/overlays nascem ocultos: alpha 0 + inativo (Show() ativa e faz fade).</summary>
        private static void DeactivateUiRoot(GameObject panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            panel.SetActive(false);
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T[] LoadAllAssets<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new T[0];
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            var list = new List<T>(guids.Length);
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            // ordem estável (por nome): cenas idempotentes entre execuções
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        private static T FindPrefabWithComponent<T>(string folder) where T : Component
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                T component = go.GetComponent<T>();
                if (component != null) return component;
            }
            return null;
        }

        /// <summary>
        /// Liga um campo [SerializeField] privado por nome via SerializedObject. Campo
        /// inexistente é ERRO (nunca silêncio): wiring contra nome errado foi exatamente
        /// o bug que deixava _gameManager nulo e o boot com NullReferenceException.
        /// </summary>
        private static void WireSerializedField(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    $"MAR Tools: campo serializado '{fieldName}' não existe em {target.GetType().Name} — wiring ignorado, cena ficará quebrada.",
                    target);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Preenche um campo-array [SerializeField] (ex.: _managersInOrder dos bootstraps,
        /// catálogos de SO) via SerializedProperty — a ordem dos elementos É a ordem de
        /// init/typeId. Campo inexistente ou não-array é ERRO explícito, nunca silêncio.
        /// </summary>
        private static void WireSerializedArray(Component target, string fieldName, Object[] values)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError(
                    $"MAR Tools: campo-array serializado '{fieldName}' não existe (ou não é array) em {target.GetType().Name} — wiring ignorado, cena ficará quebrada.",
                    target);
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
