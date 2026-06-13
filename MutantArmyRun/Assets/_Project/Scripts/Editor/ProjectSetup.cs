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
    /// A UI usa o SKIN premium do UiSkinFactory (sprites Kenney 9-slice + fonte TMP com
    /// outline + gradiente gerado, doc 01 §6): se um asset do skin faltar, cada elemento
    /// degrada individualmente para o builtin — nunca erro, nunca cena quebrada.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string SoRoot = "Assets/_Project/ScriptableObjects";

        // Cores canônicas de UI (doc 09 + doc 01 §6.5): ciano positivo, âmbar de aviso,
        // verde = ganho, vermelho = perda, dourado = moedas/CTA premium.
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);
        private static readonly Color Amber = new Color(1.00f, 0.75f, 0.15f);
        private static readonly Color PanelDark = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        private static readonly Color ButtonGrey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color GoldDeep = new Color(1.00f, 0.58f, 0.12f);
        private static readonly Color CardNavy = new Color(0.10f, 0.12f, 0.20f, 0.97f);
        private static readonly Color ChipBg = new Color(0.06f, 0.08f, 0.14f, 0.72f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);

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
            UiSkinFactory.BuildAll();   // skin premium (sprites Kenney + fonte + gradiente) ANTES das cenas
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
            Debug.Log("MAR Tools: cenas Boot/Main/Game criadas, UI premium ligada e Build Settings atualizado.");
        }

        // ------------------------------------------------------------------ Boot

        private static string CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera("Main Camera", new Vector3(0f, 1f, -10f), Quaternion.identity);
            CreateEventSystem();

            // ---- [Services]: composition root + managers persistentes (doc 12 §3.3) ----
            // Ordem canônica: Save → BossCollection → RemoteConfig → Analytics → Ads/IAP →
            // Economy → Upgrade → Unit → Reward → Audio → UI (+ GameManager).
            var services = new GameObject("[Services]");
            var bootstrap = services.AddComponent<GameBootstrap>();

            // Providers Null no próprio root: GameBootstrap resolve por
            // GetComponentInChildren<I*Provider> — sem eles o boot loga erro e fica
            // sem Remote Config, ads e analytics (doc 12 §3.3/§7.3).
            services.AddComponent<NullRemoteConfigProvider>();
            services.AddComponent<NullAnalyticsProvider>();
            services.AddComponent<NullAdsProvider>();

            var save = AddManager<SaveSystem>(services, "SaveSystem");
            // Álbum de bosses (missão Nota 10): 1º do array _managersInOrder = init logo APÓS
            // o SaveSystem (que é campo dedicado e roda antes do array) — os recordes leem
            // SaveData.bossCollection e nada mais, então nenhum outro manager o precede.
            var bossCollection = AddManager<BossCollectionSystem>(services, "BossCollectionSystem");
            var remoteConfig = AddManager<RemoteConfigManager>(services, "RemoteConfigManager");
            var analytics = AddManager<AnalyticsManager>(services, "AnalyticsManager");
            var ads = AddManager<AdsManager>(services, "AdsManager");
            var iap = AddManager<IAPManager>(services, "IAPManager");
            var economy = AddManager<EconomySystem>(services, "EconomySystem");
            var upgrade = AddManager<UpgradeSystem>(services, "UpgradeSystem");
            var unit = AddManager<UnitManager>(services, "UnitManager");
            var reward = AddManager<RewardSystem>(services, "RewardSystem");
            var mission = AddManager<MissionSystem>(services, "MissionSystem");
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
                bossCollection, remoteConfig, analytics, ads, iap, economy, upgrade, unit, reward, mission, audio, ui
            });

            // Splash: orçamento de boot de 2,5 s — gradiente do skin + logo (doc 12 §2.2).
            GameObject splash = CreateCanvas("SplashCanvas", null);
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(splash.transform, false);
            StretchFull(bgRect);
            var bgImage = bg.GetComponent<Image>();
            Sprite splashGradient = UiSkin.MenuGradient;
            if (splashGradient != null)
            {
                bgImage.sprite = splashGradient;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = Color.black;
            }
            TMP_Text splashTitle = CreateTmpLabel(splash.transform, "Title", "MUTANT ARMY\nRUN", 84f,
                           TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(900f, 360f));
            ApplyTitleStyle(splashTitle, 0f);

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

            // Fundo: gradiente vertical vibrante gerado em código (doc 01 §6.2) +
            // dois glows suaves de profundidade. Tudo raycast-off.
            Sprite gradient = UiSkin.MenuGradient;
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(canvas.transform, false);
            StretchFull(bgRect);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.raycastTarget = false;
            if (gradient != null) { bgImg.sprite = gradient; bgImg.color = Color.white; }
            else bgImg.color = new Color(0.23f, 0.14f, 0.45f);

            Sprite glow = UiSkin.GlowSoft;
            if (glow != null)
            {
                CreateImage(canvas.transform, "GlowTop", glow, new Color(1f, 1f, 1f, 0.10f),
                            Anchors.TopCenter, new Vector2(-320f, -260f), new Vector2(900f, 900f), Image.Type.Simple);
                CreateImage(canvas.transform, "GlowBottom", glow, new Color(1f, 0.55f, 0.85f, 0.08f),
                            Anchors.BottomCenter, new Vector2(380f, 220f), new Vector2(1100f, 1100f), Image.Type.Simple);
            }

            // Topo: painéis de moeda (esq.) e gema (dir.) com ícones — preenchidos pelo controller.
            TMP_Text coins = CreateWalletChip(canvas.transform, "CoinsChip", UiSkin.IconCoin, Color.white,
                            Anchors.TopLeft, new Vector2(205f, -90f), new Vector2(330f, 84f), Amber);
            TMP_Text gems = CreateWalletChip(canvas.transform, "GemsChip", UiSkin.IconGem, new Color(0.55f, 0.85f, 1f),
                            Anchors.TopRight, new Vector2(-205f, -90f), new Vector2(330f, 84f), new Color(0.55f, 0.85f, 1f));

            // Logo tipográfico: grande, outline (material da fonte), gradiente dourado e
            // leve rotação — identidade sem depender de arte bitmap.
            TMP_Text title = CreateTmpLabel(canvas.transform, "Title", "MUTANT ARMY\nRUN", 108f,
                           TextAlignmentOptions.Center, Anchors.TopCenter, new Vector2(0f, -420f), new Vector2(980f, 380f));
            ApplyTitleStyle(title, -4f);

            // Badge da fase atual.
            Image levelBadge = CreateImage(canvas.transform, "LevelBadge",
                            UiSkin.BadgeFlat, UiSkin.BadgeFlat != null ? new Color(0.08f, 0.10f, 0.20f, 0.78f) : PanelDark,
                            Anchors.Middle, new Vector2(0f, 130f), new Vector2(380f, 96f));
            TMP_Text levelText = CreateTmpLabel(levelBadge.transform, "LevelText", "FASE 1", 50f,
                           TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(380f, 96f));

            // Botão JOGAR — gigante, dourado, na thumb zone (terço inferior, doc 09 §6),
            // com pulso sutil em loop e ScalePop no press (UIButtonPop via CreateButton).
            TMP_Text playLabel;
            Button play = CreateButton(canvas.transform, "PlayButton", "JOGAR", 80f,
                                       UiSkin.ButtonGold, Amber,
                                       Anchors.Middle, new Vector2(0f, -330f), new Vector2(660f, 190f), out playLabel);
            play.gameObject.AddComponent<UIPulse>();
            if (AddIcon(play.transform, "PlayIcon", UiSkin.IconPlay, new Vector2(-220f, 0f), 64f) != null)
                ((RectTransform)playLabel.transform).anchoredPosition = new Vector2(28f, 0f);

            WireSerializedField(menu, "_playButton", play);
            WireSerializedField(menu, "_playLabel", playLabel);
            WireSerializedField(menu, "_coinsText", coins);
            WireSerializedField(menu, "_gemsText", gems);
            WireSerializedField(menu, "_levelText", levelText);

            // Telas de meta (SCR-06/07/08/09 + Diário) + tab bar de navegação, ligadas ao menu
            // (brief F4): construídas e costuradas em separado pela MetaScreensFactory.
            MetaScreensFactory.Build(canvas, menu);

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

            // ---- [GameSystems]: managers de gameplay na ordem Level→Enemies→Crowd→Gate→Boss→Combat→Combo (doc 12 §3.3) ----
            var systems = new GameObject("[GameSystems]");
            var sceneBootstrap = systems.AddComponent<GameSceneBootstrap>();
            var level = AddManager<LevelManager>(systems, "LevelManager");
            // Inimigos de pista (missão Nota 10): APÓS Level e ANTES de Combat — ordem do
            // composition root é CONTRATO (§3.3). O LevelManager chama SpawnFromLevel no
            // BeginRun (null-safe), mas o Init precisa ter rodado antes da 1ª corrida.
            var enemies = AddManager<TrackEnemyManager>(systems, "TrackEnemyManager");
            var crowd = AddManager<CrowdManager>(systems, "CrowdManager");
            // Sem CrowdRenderer a multidão é INVISÍVEL (Submit é no-op sem Instance);
            // sem RiskResolver o portal de Risco vira no-op (doc 12 §4.3/§6.2).
            var crowdRenderer = AddManager<CrowdRenderer>(systems, "CrowdRenderer");
            var gate = AddManager<GateManager>(systems, "GateManager");
            var boss = AddManager<BossManager>(systems, "BossManager");
            var combat = AddManager<CombatSystem>(systems, "CombatSystem");
            // Combos (missão Nota 10): APÓS Combat — na morte do boss ele fotografa
            // CrowdManager/BossManager/CombatSystem (contrato W2-C §5).
            var combo = AddManager<ComboSystem>(systems, "ComboSystem");
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
                level, enemies, crowd, crowdRenderer, gate, boss, combat, combo, vfx, risk, anchor
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

            // ---- HUD do boss (missão Nota 10): barra de HP + nome + fraqueza + aviso ----
            BuildBossHud((RectTransform)hudCanvas.transform);

            // ---- SCR-04/05 ResultScreen + glue (GameUIController) ----
            ResultScreen resultScreen = BuildResultScreen((RectTransform)hudCanvas.transform);
            var gameUi = hudCanvas.AddComponent<GameUIController>();
            WireSerializedField(gameUi, "_resultScreen", resultScreen);
            WireSerializedField(gameUi, "_feedback", feedbackController);

            // Ordem de irmãos é ordem de render no mesmo canvas: FeedbackText por ÚLTIMO —
            // os popups de combo pós-vitória aparecem SOBRE a ResultScreen (missão Nota 10).
            feedback.transform.SetAsLastSibling();

            string path = ScenesFolder + "/Game.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ UI builders

        private static void BuildHudElements(HudController controller, RectTransform root)
        {
            // Barra de progresso da pista (zona segura do topo, doc 01 §6.4) com o ícone
            // do boss na ponta — o jogador vê o quão perto está da arena.
            Image progressFill = CreateBar(root, "ProgressBar", Anchors.TopCenter,
                new Vector2(0f, -60f), new Vector2(720f, 36f), Green);
            Transform progressBg = progressFill.transform.parent;
            Sprite glow = UiSkin.GlowSoft;
            if (glow != null)
            {
                Image marker = CreateImage(progressBg, "BossMarker", glow, new Color(1f, 0.28f, 0.20f, 0.95f),
                    Anchors.MiddleRight, new Vector2(16f, 0f), new Vector2(96f, 96f), Image.Type.Simple);
                AddIcon(marker.transform, "Exclamation", UiSkin.IconExclamation, Vector2.zero, 44f);
            }

            // Contador de tropas em BADGE grande central-superior: o número mais
            // importante da tela (doc 09 §4.2); pop ao mudar vem do HudController.
            Image armyBadge = CreateImage(root, "ArmyBadge",
                UiSkin.BadgeRound, UiSkin.BadgeRound != null ? Color.white : new Color(0.13f, 0.30f, 0.60f, 0.90f),
                Anchors.TopCenter, new Vector2(0f, -175f), new Vector2(330f, 150f));
            TMP_Text unitCount = CreateTmpLabel(armyBadge.transform, "UnitCountText", "1", 96f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 2f), new Vector2(330f, 150f));

            // Barra de Supply "52/60" com fill e texto sobreposto.
            Image supplyFill = CreateBar(root, "SupplyBar", Anchors.TopCenter,
                new Vector2(0f, -290f), new Vector2(520f, 40f), Cyan);
            TMP_Text supplyText = CreateTmpLabel(supplyFill.transform.parent, "SupplyText", "0/60", 28f,
                TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(520f, 40f));

            // Carteira persistente (topo-esquerda, chip com ícone) + delta da corrida
            // (visual DISTINTO — doc 12 §4.13).
            TMP_Text coins = CreateWalletChip(root, "CoinsChip", UiSkin.IconCoin, Color.white,
                Anchors.TopLeft, new Vector2(185f, -70f), new Vector2(300f, 76f), Amber);
            TMP_Text runCoins = CreateTmpLabel(root, "RunCoinsText", "+0", 36f,
                TextAlignmentOptions.Left, Anchors.TopLeft, new Vector2(195f, -140f), new Vector2(320f, 50f),
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

        /// <summary>
        /// HUD do boss (missão Nota 10): GameObject "BossHud" full-stretch sob o HudCanvas com
        /// o BossHudController e a hierarquia polida — barra 700×46 topo-centro com fundo
        /// escuro e fill ANCORADO (_hpFillRect: anchorMax.x = HP normalizado, mesma técnica do
        /// fallback do controller), nome do boss acima, fraqueza ativa abaixo e o aviso de
        /// especial central com CanvasGroup próprio (pisca sem mexer no resto). O GO fica
        /// SEMPRE ativo (os handlers de evento vivem nele); mostrar/esconder é alpha do
        /// _rootGroup — contrato do BossHudController.
        /// </summary>
        private static void BuildBossHud(RectTransform hudCanvas)
        {
            var go = new GameObject("BossHud", typeof(RectTransform));
            var root = (RectTransform)go.transform;
            root.SetParent(hudCanvas, false);
            StretchFull(root);

            var controller = go.AddComponent<BossHudController>();

            var rootGroup = go.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;                   // nasce invisível; StateEntered(BossFight) faz o fade
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;       // AutoPilot/jogador tocam por baixo intactos

            // Nome do boss acima da barra ("[RARO]" e a cor roxa entram pelo controller).
            TMP_Text bossName = CreateTmpLabel(root, "BossName", string.Empty, 40f,
                TextAlignmentOptions.Center, Anchors.TopCenter, new Vector2(0f, -322f), new Vector2(760f, 44f));

            // Barra 700×46 topo-centro, abaixo do bloco do HudController (Supply termina em -310).
            Image barBg = CreateImage(root, "HpBarBg",
                UiSkin.BarBackground,
                UiSkin.BarBackground != null ? new Color(0.10f, 0.11f, 0.16f, 0.95f)
                                             : new Color(0.08f, 0.09f, 0.13f, 0.85f),
                Anchors.TopCenter, new Vector2(0f, -372f), new Vector2(700f, 46f));

            // Fill ancorado: o controller escreve anchorMax.x; a COR por fase
            // (verde→amarelo→vermelho) entra por _hpFillImage (type != Filled → caminho do rect).
            var fillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(barBg.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.35f, 0.90f, 0.40f);   // verde da fase 0 (controller re-tinta)
            fillImage.raycastTarget = false;

            // Marcadores dos limiares de fase 0.5/0.25 (CONTRACT §1 item 14), sobre o fill.
            CreateBossHudMarker((RectTransform)barBg.transform, "Marker50", 0.5f);
            CreateBossHudMarker((RectTransform)barBg.transform, "Marker25", 0.25f);

            // Fraqueza ativa abaixo da barra — texto E cor vêm do controller (nome informa,
            // cor reforça, doc 09 P7).
            TMP_Text weakness = CreateTmpLabel(root, "WeaknessTag", string.Empty, 28f,
                TextAlignmentOptions.Center, Anchors.TopCenter, new Vector2(0f, -428f), new Vector2(760f, 34f));

            // Aviso do especial: central e piscante (CanvasGroup próprio, animado pelo controller
            // durante toda a janela do telegraph — unscaled, convive com slow-mo).
            TMP_Text warning = CreateTmpLabel(root, "SpecialWarning", "!! ATAQUE ESPECIAL !!", 44f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 470f), new Vector2(860f, 56f),
                new Color(1.00f, 0.35f, 0.20f));
            var warningGroup = warning.gameObject.AddComponent<CanvasGroup>();
            warningGroup.alpha = 0f;
            warningGroup.interactable = false;
            warningGroup.blocksRaycasts = false;

            WireSerializedField(controller, "_rootGroup", rootGroup);
            WireSerializedField(controller, "_bossNameText", bossName);
            WireSerializedField(controller, "_weaknessText", weakness);
            WireSerializedField(controller, "_hpFillImage", fillImage);
            WireSerializedField(controller, "_hpFillRect", fillRect);
            WireSerializedField(controller, "_warningText", warning);
            WireSerializedField(controller, "_warningGroup", warningGroup);
        }

        /// <summary>Risco vertical nos limiares de fase da barra do boss (0.5/0.25).</summary>
        private static void CreateBossHudMarker(RectTransform bar, string name, float normalizedX)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(bar, false);
            rect.anchorMin = new Vector2(normalizedX, 0f);
            rect.anchorMax = new Vector2(normalizedX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(3f, -8f);   // 4 px de respiro vertical dentro do trilho
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.65f);
            img.raycastTarget = false;
        }

        private static ResultScreen BuildResultScreen(RectTransform parent)
        {
            GameObject panel = CreatePanel(parent, "ResultScreen", new Color(0f, 0f, 0f, 0.85f));
            panel.AddComponent<CanvasGroup>();              // UIScreen exige CanvasGroup
            var screen = panel.AddComponent<ResultScreen>();

            // Cartão central escuro sobre o dim — tudo do resultado vive nele.
            Image card = CreateImage(panel.transform, "Card",
                UiSkin.BadgeFlat, UiSkin.BadgeFlat != null ? CardNavy : PanelDark,
                Anchors.Middle, new Vector2(0f, -10f), new Vector2(920f, 1320f));

            // Header colorido: o ResultScreen pinta verde/vermelho no Bind (doc 01 §6.5).
            Image headerBg = CreateImage(card.transform, "Header",
                UiSkin.BadgeFlat, new Color(0.22f, 0.72f, 0.35f),
                Anchors.Middle, new Vector2(0f, 580f), new Vector2(880f, 150f));
            TMP_Text title = CreateTmpLabel(headerBg.transform, "Title", "VITÓRIA!", 76f,
                TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(880f, 150f));

            // Espaço reservado do confete (o agente de juice ancora o burst aqui).
            var confetti = new GameObject("ConfettiAnchor", typeof(RectTransform));
            var confettiRect = (RectTransform)confetti.transform;
            confettiRect.SetParent(card.transform, false);
            PlaceRect(confettiRect, Anchors.Middle, new Vector2(0f, 350f), new Vector2(800f, 400f));

            // Selo PERFECT (doc 09 §4.4) — container com estrela + texto, ligado ao
            // campo _perfectBadge (GameObject).
            var perfectGo = new GameObject("PerfectBadge", typeof(RectTransform));
            var perfectRect = (RectTransform)perfectGo.transform;
            perfectRect.SetParent(card.transform, false);
            PlaceRect(perfectRect, Anchors.Middle, new Vector2(0f, 455f), new Vector2(520f, 80f));
            AddIcon(perfectRect, "Star", UiSkin.IconStar, new Vector2(-190f, 0f), 60f);
            CreateTmpLabel(perfectRect, "Label", "PERFECT!", 46f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(20f, 0f), new Vector2(420f, 76f), Amber);
            perfectGo.SetActive(false);

            // Dica da derrota (missão Nota 10): MESMO slot do selo PERFECT — nunca coexistem
            // (PERFECT é só vitória, motivo é só derrota; o Bind controla os dois).
            TMP_Text reason = CreateTmpLabel(card.transform, "ReasonText", string.Empty, 34f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 450f), new Vector2(820f, 110f), TextSoft);
            reason.gameObject.SetActive(false);

            // O número principal é o DELTA, nunca o total da carteira (doc 12 §4.13) —
            // ENORME, dourado, com a moeda do lado.
            bool hasCoinIcon = AddIcon(card.transform, "CoinIcon", UiSkin.IconCoin, new Vector2(-230f, 320f), 96f) != null;
            TMP_Text coinsDelta = CreateTmpLabel(card.transform, "CoinsDelta", "+0", 112f,
                TextAlignmentOptions.Center, Anchors.Middle,
                new Vector2(hasCoinIcon ? 40f : 0f, 320f), new Vector2(560f, 150f), Amber);

            TMP_Text xpDelta = CreateTmpLabel(card.transform, "XpDelta", "+0 XP", 46f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 195f), new Vector2(600f, 70f));
            TMP_Text survivors = CreateTmpLabel(card.transform, "Survivors", "Sobreviventes: 0", 36f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 110f), new Vector2(700f, 60f), TextSoft);
            TMP_Text damage = CreateTmpLabel(card.transform, "Damage", "Dano causado: 0", 36f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 50f), new Vector2(700f, 60f), TextSoft);

            // DOBRAR (verde, com ícone de vídeo do rewarded — CANON §11).
            TMP_Text doubleLabel;
            Button doubleButton = CreateButton(card.transform, "DoubleButton", "DOBRAR x2", 44f,
                UiSkin.ButtonGreen, Green,
                Anchors.Middle, new Vector2(0f, -105f), new Vector2(640f, 140f), out doubleLabel);
            if (AddIcon(doubleButton.transform, "VideoIcon", UiSkin.IconVideo, new Vector2(-245f, 0f), 58f) != null)
                ((RectTransform)doubleLabel.transform).anchoredPosition = new Vector2(30f, 0f);

            // PRÓXIMA FASE: o CTA principal — dourado, o maior do cartão. O rótulo é wireado
            // (_nextButtonLabel): a tela troca para "TENTAR DE NOVO" na derrota.
            TMP_Text nextLabel;
            Button nextButton = CreateButton(card.transform, "NextButton", "PRÓXIMA FASE", 56f,
                UiSkin.ButtonGold, Amber,
                Anchors.Middle, new Vector2(0f, -295f), new Vector2(660f, 170f), out nextLabel);

            TMP_Text homeLabel;
            Button homeButton = CreateButton(card.transform, "HomeButton", "MENU", 38f,
                UiSkin.ButtonGrey, ButtonGrey,
                Anchors.Middle, new Vector2(0f, -490f), new Vector2(400f, 100f), out homeLabel);
            if (AddIcon(homeButton.transform, "HomeIcon", UiSkin.IconHome, new Vector2(-140f, 0f), 44f) != null)
                ((RectTransform)homeLabel.transform).anchoredPosition = new Vector2(22f, 0f);

            // ---- Extras da missão Nota 10 (a ResultScreen segue passiva: BindCombos/
            // BindNextBoss/SetRareBossDefeated preenchem; aqui só nasce a hierarquia) ----

            // Pilha de combos ACIMA do cartão (faixa livre y 650..960): grade 2×3 — o mesmo
            // layout do fallback da tela, agora via GridLayoutGroup (as linhas nascem em
            // runtime com LayoutElement compatível: célula 450×62, 6 ComboKind no máximo).
            var combosGo = new GameObject("CombosContent", typeof(RectTransform));
            var combosRect = (RectTransform)combosGo.transform;
            combosRect.SetParent(panel.transform, false);
            PlaceRect(combosRect, Anchors.Middle, new Vector2(0f, 878f), new Vector2(940f, 230f));
            combosRect.pivot = new Vector2(0.5f, 1f);   // cresce para baixo a partir do topo da faixa
            var combosGrid = combosGo.AddComponent<GridLayoutGroup>();
            combosGrid.cellSize = new Vector2(450f, 62f);
            combosGrid.spacing = new Vector2(20f, 8f);
            combosGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            combosGrid.constraintCount = 2;
            combosGrid.childAlignment = TextAnchor.UpperCenter;

            // Badge "BOSS RARO DERROTADO! x3" acima da pilha de combos (RareBossMath ×3).
            Image rareBg = CreateImage(panel.transform, "RareBossBadge",
                UiSkin.BadgeFlat, Gold,
                Anchors.Middle, new Vector2(0f, 925f), new Vector2(640f, 62f));
            CreateTmpLabel(rareBg.transform, "Label", "BOSS RARO DERROTADO! x3", 34f,
                TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(640f, 62f),
                new Color(0.25f, 0.13f, 0.02f));
            rareBg.gameObject.AddComponent<CanvasGroup>();   // reveal em stagger usa o group
            rareBg.gameObject.SetActive(false);

            // Teaser do PRÓXIMO boss ABAIXO do cartão (faixa livre y -670..-960): silhueta
            // (scoutCardArt em tinte escuro, aplicado pelo BindNextBoss) + nome/fraqueza.
            Image teaserBg = CreateImage(panel.transform, "NextBossTeaser",
                UiSkin.BadgeFlat, UiSkin.BadgeFlat != null ? CardNavy : ChipBg,
                Anchors.Middle, new Vector2(0f, -805f), new Vector2(920f, 180f));
            teaserBg.gameObject.AddComponent<CanvasGroup>();

            var silGo = new GameObject("Silhouette", typeof(RectTransform), typeof(Image));
            var silRect = (RectTransform)silGo.transform;
            silRect.SetParent(teaserBg.transform, false);
            PlaceRect(silRect, Anchors.Middle, new Vector2(-340f, 0f), new Vector2(150f, 150f));
            var silhouette = silGo.GetComponent<Image>();
            silhouette.preserveAspect = true;
            silhouette.raycastTarget = false;
            silhouette.enabled = false;     // só liga quando o BindNextBoss tiver arte

            TMP_Text nextBossText = CreateTmpLabel(teaserBg.transform, "Text", string.Empty, 40f,
                TextAlignmentOptions.Left, Anchors.Middle, new Vector2(70f, 0f), new Vector2(700f, 170f));
            teaserBg.gameObject.SetActive(false);

            WireSerializedField(screen, "_headerBg", headerBg);
            WireSerializedField(screen, "_titleText", title);
            WireSerializedField(screen, "_coinsDeltaText", coinsDelta);
            WireSerializedField(screen, "_xpDeltaText", xpDelta);
            WireSerializedField(screen, "_survivorsText", survivors);
            WireSerializedField(screen, "_damageText", damage);
            WireSerializedField(screen, "_perfectBadge", perfectGo);
            WireSerializedField(screen, "_doubleButton", doubleButton);
            WireSerializedField(screen, "_doubleButtonLabel", doubleLabel);
            WireSerializedField(screen, "_nextButton", nextButton);
            WireSerializedField(screen, "_homeButton", homeButton);

            // Campos da missão Nota 10 (nomes EXATOS da ResultScreen — typo = LogError).
            WireSerializedField(screen, "_reasonText", reason);
            WireSerializedField(screen, "_combosContent", combosRect);
            WireSerializedField(screen, "_rareBadge", rareBg.gameObject);
            WireSerializedField(screen, "_nextBossPanel", teaserBg.gameObject);
            WireSerializedField(screen, "_nextBossSilhouette", silhouette);
            WireSerializedField(screen, "_nextBossText", nextBossText);
            WireSerializedField(screen, "_nextButtonLabel", nextLabel);

            DeactivateUiRoot(panel);   // nasce oculta; UIManager.Push/Show ativa
            return screen;
        }

        private static BossScoutOverlay BuildBossScoutOverlay(RectTransform parent)
        {
            GameObject panel = CreatePanel(parent, "BossScoutOverlay", new Color(0f, 0f, 0f, 0.85f));
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

            // Cartão central com MOLDURA (panel fantasy do skin; doc 09 §5.1).
            Image card = CreateImage(panel.transform, "Card",
                UiSkin.PanelFrame, UiSkin.PanelFrame != null ? Color.white : PanelDark,
                Anchors.Middle, new Vector2(0f, 40f), new Vector2(940f, 1180f));

            CreateTmpLabel(card.transform, "Header", "BOSS À FRENTE!", 40f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 500f), new Vector2(820f, 60f), Gold);
            TMP_Text bossName = CreateTmpLabel(card.transform, "BossName", "BOSS", 76f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 400f), new Vector2(820f, 110f));

            // Retrato (scoutCardArt) — desabilitado até o Bind achar arte.
            Image portrait = CreateImage(card.transform, "Portrait", null, Color.white,
                Anchors.Middle, new Vector2(0f, 170f), new Vector2(360f, 360f), Image.Type.Simple);
            portrait.enabled = false;

            // Linha do elemento: orbe tintado pela cor do elemento (Bind) + nome.
            Image elementIcon = AddIcon(card.transform, "ElementIcon", UiSkin.GlowSoft, new Vector2(-250f, -80f), 110f);
            TMP_Text element = CreateTmpLabel(card.transform, "Element", "ELEMENTO: -", 44f,
                TextAlignmentOptions.Left, Anchors.Middle, new Vector2(70f, -80f), new Vector2(560f, 80f));

            // Fraqueza DESTACADA: faixa dourada com texto escuro + orbe da cor da
            // fraqueza — nome do elemento, nunca só cor (doc 09 P7).
            Image weaknessStrip = CreateImage(card.transform, "WeaknessStrip",
                UiSkin.BadgeFlat, new Color(1f, 0.78f, 0.18f),
                Anchors.Middle, new Vector2(0f, -225f), new Vector2(840f, 120f));
            Image weaknessIcon = AddIcon(weaknessStrip.transform, "WeaknessIcon", UiSkin.GlowSoft, new Vector2(-340f, 0f), 80f);
            TMP_Text weakness = CreateTmpLabel(weaknessStrip.transform, "Weakness", "FRACO CONTRA -", 50f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(30f, 0f), new Vector2(700f, 110f),
                new Color(0.25f, 0.13f, 0.02f));

            TMP_Text hint = CreateTmpLabel(card.transform, "Hint", string.Empty, 36f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, -365f), new Vector2(860f, 70f), TextSoft);

            // Timer circular do auto-dismiss (~2 s) — esvazia via _timerFill.
            Image timerFill = CreateRingTimer(card.transform, "Timer", new Vector2(0f, -490f), 120f, Amber);

            WireSerializedField(overlay, "_bossNameText", bossName);
            WireSerializedField(overlay, "_elementText", element);
            WireSerializedField(overlay, "_weaknessText", weakness);
            WireSerializedField(overlay, "_hintText", hint);
            WireSerializedField(overlay, "_portrait", portrait);
            WireSerializedField(overlay, "_elementIcon", elementIcon);
            WireSerializedField(overlay, "_weaknessIcon", weaknessIcon);
            WireSerializedField(overlay, "_timerFill", timerFill);
            WireSerializedField(overlay, "_skipButton", skipButton);

            DeactivateUiRoot(panel);
            return overlay;
        }

        private static ReviveOverlay BuildReviveOverlay(RectTransform parent)
        {
            // Dim avermelhado: urgência sem gore (doc 09 §5.5).
            GameObject panel = CreatePanel(parent, "ReviveOverlay", new Color(0.20f, 0f, 0.02f, 0.90f));
            panel.AddComponent<CanvasGroup>();
            var overlay = panel.AddComponent<ReviveOverlay>();

            Image card = CreateImage(panel.transform, "Card",
                UiSkin.BadgeFlat, UiSkin.BadgeFlat != null ? new Color(0.20f, 0.08f, 0.11f, 0.97f) : PanelDark,
                Anchors.Middle, Vector2.zero, new Vector2(880f, 1000f));

            Image headerBg = CreateImage(card.transform, "Header",
                UiSkin.BadgeFlat, new Color(0.85f, 0.25f, 0.28f),
                Anchors.Middle, new Vector2(0f, 420f), new Vector2(840f, 140f));
            TMP_Text title = CreateTmpLabel(headerBg.transform, "Title", "EXÉRCITO DERROTADO", 56f,
                TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, new Vector2(840f, 140f));

            CreateTmpLabel(card.transform, "Subtitle", "Reviver com metade do exército?", 38f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 300f), new Vector2(820f, 70f), TextSoft);

            // Countdown URGENTE: anel esvaziando + segundos gigantes no centro.
            Image timerFill = CreateRingTimer(card.transform, "CountdownRing", new Vector2(0f, 90f), 260f,
                new Color(1f, 0.42f, 0.20f));
            TMP_Text countdown = CreateTmpLabel(card.transform, "CountdownText", "5", 120f,
                TextAlignmentOptions.Center, Anchors.Middle, new Vector2(0f, 90f), new Vector2(260f, 260f), Gold);

            TMP_Text reviveLabel;
            Button reviveButton = CreateButton(card.transform, "ReviveButton", "REVIVER (ANÚNCIO)", 44f,
                UiSkin.ButtonGreen, Green,
                Anchors.Middle, new Vector2(0f, -205f), new Vector2(660f, 150f), out reviveLabel);
            if (AddIcon(reviveButton.transform, "VideoIcon", UiSkin.IconVideo, new Vector2(-255f, 0f), 60f) != null)
                ((RectTransform)reviveLabel.transform).anchoredPosition = new Vector2(32f, 0f);

            Button declineButton = CreateButton(card.transform, "DeclineButton", "DESISTIR", 38f,
                UiSkin.ButtonGrey, ButtonGrey,
                Anchors.Middle, new Vector2(0f, -385f), new Vector2(420f, 100f), out _);

            WireSerializedField(overlay, "_titleText", title);
            WireSerializedField(overlay, "_reviveButton", reviveButton);
            WireSerializedField(overlay, "_reviveLabel", reviveLabel);
            WireSerializedField(overlay, "_declineButton", declineButton);
            WireSerializedField(overlay, "_countdownText", countdown);
            WireSerializedField(overlay, "_timerFill", timerFill);

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
            public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
            public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
            public static readonly Vector2 TopRight = new Vector2(1f, 1f);
            public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
            public static readonly Vector2 MiddleRight = new Vector2(1f, 0.5f);
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
            ApplyFont(text);
            return text;
        }

        /// <summary>
        /// Fonte do skin (Kenney Future SDF com outline no material default); fallback
        /// LiberationSans BOLD + material outline do UiSkinFactory — o jogo nunca fica
        /// sem fonte nem sem contorno legível (doc 01 §6.4).
        /// </summary>
        private static void ApplyFont(TMP_Text text)
        {
            TMP_FontAsset skinFont = UiSkin.FontAsset;
            if (skinFont != null)
            {
                text.font = skinFont;
                return;
            }

            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (liberation == null) return;   // TMP Essentials ausentes: TMP usa o default
            text.font = liberation;
            text.fontStyle = FontStyles.Bold;
            Material outline = UiSkin.FallbackOutlineMaterial;
            if (outline != null) text.fontSharedMaterial = outline;
        }

        /// <summary>Estilo do logo: gradiente dourado + leve rotação (doc 09 §4.1).</summary>
        private static void ApplyTitleStyle(TMP_Text title, float rotationZ)
        {
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Gold, Gold, GoldDeep, GoldDeep);
            title.lineSpacing = -20f;
            if (!Mathf.Approximately(rotationZ, 0f))
                title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        }

        /// <summary>
        /// Image utilitária do skin: sprite 9-slice (ou builtin quando o skin falta),
        /// raycast OFF — só botões e o skip do Boss Scout bloqueiam toque.
        /// </summary>
        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color,
                                         Vector2 anchor, Vector2 anchoredPos, Vector2 size,
                                         Image.Type type = Image.Type.Sliced)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            PlaceRect(rect, anchor, anchoredPos, size);

            var image = go.GetComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = type;
                if (type == Image.Type.Simple) image.preserveAspect = true;
            }
            else
            {
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                image.type = Image.Type.Sliced;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Ícone simples (preserveAspect); retorna null se o sprite não existe no skin.</summary>
        private static Image AddIcon(Transform parent, string name, Sprite sprite, Vector2 anchoredPos, float size)
        {
            if (sprite == null) return null;
            return CreateImage(parent, name, sprite, Color.white, Anchors.Middle, anchoredPos,
                               new Vector2(size, size), Image.Type.Simple);
        }

        /// <summary>
        /// Chip de carteira (moeda/gema): badge escuro translúcido + ícone + valor.
        /// Retorna o TMP do VALOR (é ele que os controllers escrevem).
        /// </summary>
        private static TMP_Text CreateWalletChip(Transform parent, string name, Sprite icon, Color iconTint,
                                                 Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color textColor)
        {
            Image chip = CreateImage(parent, name,
                UiSkin.BadgeFlat, UiSkin.BadgeFlat != null ? ChipBg : new Color(0f, 0f, 0f, 0.45f),
                anchor, anchoredPos, size);

            float iconSize = size.y * 0.66f;
            Image iconImage = null;
            if (icon != null)
            {
                iconImage = CreateImage(chip.transform, "Icon", icon, iconTint,
                    Anchors.MiddleLeft, new Vector2(iconSize * 0.5f + 14f, 0f),
                    new Vector2(iconSize, iconSize), Image.Type.Simple);
            }

            TMP_Text value = CreateTmpLabel(chip.transform, "Value", "0", Mathf.Round(size.y * 0.52f),
                TextAlignmentOptions.Left, Anchors.Middle, Vector2.zero, size, textColor);
            var valueRect = (RectTransform)value.transform;
            StretchFull(valueRect);
            valueRect.offsetMin = new Vector2(iconImage != null ? iconSize + 28f : 18f, 4f);
            valueRect.offsetMax = new Vector2(-14f, -4f);
            return value;
        }

        private static Button CreateButton(Transform parent, string name, string label, float fontSize,
                                           Sprite skin, Color fallbackColor, Vector2 anchor,
                                           Vector2 anchoredPos, Vector2 size, out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            PlaceRect(rect, anchor, anchoredPos, size);

            var image = go.GetComponent<Image>();
            if (skin != null)
            {
                image.sprite = skin;
                image.color = Color.white;          // os sprites Kenney já vêm coloridos
            }
            else
            {
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                image.color = fallbackColor;
            }
            image.type = Image.Type.Sliced;

            // Microinteração canônica: ScalePop no press em TODO botão (doc 09 §6).
            go.AddComponent<UIButtonPop>();

            labelText = CreateTmpLabel(go.transform, "Label", label, fontSize,
                                       TextAlignmentOptions.Center, Anchors.Middle, Vector2.zero, size);
            var labelRect = (RectTransform)labelText.transform;
            StretchFull(labelRect);

            return go.GetComponent<Button>();
        }

        /// <summary>
        /// Barra com fundo 9-slice do skin + fill horizontal embutido (6 px de respiro);
        /// retorna o Image do FILL (Image.Type.Filled, cor própria — o HudController
        /// re-tinta o Supply para âmbar em ≥80%).
        /// </summary>
        private static Image CreateBar(Transform parent, string name, Vector2 anchor,
                                       Vector2 anchoredPos, Vector2 size, Color fillColor)
        {
            Sprite barBg = UiSkin.BarBackground;
            var bg = new GameObject(name, typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(parent, false);
            PlaceRect(bgRect, anchor, anchoredPos, size);
            var bgImage = bg.GetComponent<Image>();
            if (barBg != null)
            {
                bgImage.sprite = barBg;
                bgImage.type = Image.Type.Sliced;
                bgImage.color = new Color(0.55f, 0.58f, 0.66f, 0.95f);   // trilho neutro
            }
            else
            {
                bgImage.color = new Color(0f, 0f, 0f, 0.45f);
            }
            bgImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(bg.transform, false);
            StretchFull(fillRect);
            fillRect.offsetMin = new Vector2(6f, 6f);
            fillRect.offsetMax = new Vector2(-6f, -6f);
            var fillImage = fill.GetComponent<Image>();
            // Image.Type.Filled exige sprite — o builtin UISprite serve de máscara branca
            // (sprite colorido aqui brigaria com o re-tint do HudController).
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        /// <summary>
        /// Timer circular: trilho apagado + anel radial (Radial360, origem no topo,
        /// anti-horário) que o código de runtime esvazia via fillAmount. Sem o sprite
        /// de anel no skin, degrada para um disco radial translúcido (builtin).
        /// </summary>
        private static Image CreateRingTimer(Transform parent, string name, Vector2 anchoredPos,
                                             float size, Color fillColor)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(parent, false);
            PlaceRect(holderRect, Anchors.Middle, anchoredPos, new Vector2(size, size));

            Sprite ring = UiSkin.Ring;
            if (ring != null)
            {
                CreateImage(holder.transform, "Track", ring, new Color(1f, 1f, 1f, 0.18f),
                            Anchors.Middle, Vector2.zero, new Vector2(size, size), Image.Type.Simple);
            }

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(holder.transform, false);
            PlaceRect(fillRect, Anchors.Middle, Vector2.zero, new Vector2(size, size));
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = ring != null
                ? ring
                : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false;
            fillImage.fillAmount = 1f;
            fillImage.color = ring != null ? fillColor : new Color(fillColor.r, fillColor.g, fillColor.b, 0.35f);
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
