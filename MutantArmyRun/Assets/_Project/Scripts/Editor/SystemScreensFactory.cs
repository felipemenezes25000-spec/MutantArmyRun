using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MutantArmy.UI;
using Object = UnityEngine.Object;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build System Screens — augmenta as cenas Main e Game com as TELAS-SISTEMA que
    /// faltavam (igual ao JuiceFactory: OpenScene → constrói no lugar → salva). Idempotente.
    ///
    /// MAIN (menu): cria SettingsScreen e EventsScreen como telas de meta (push via UIManager) +
    ///   o botão de ENGRENAGEM no canto superior (→ Settings) e o botão EVENTOS na tab bar
    ///   inferior (→ Events), ligando tudo ao MainMenuController por SerializedProperty.
    /// GAME (HUD): cria o PauseOverlay no HudCanvas + o botão de PAUSE no canto do HUD, ligando
    ///   ao GameUIController (botão pause → overlay; ações do overlay → reiniciar/menu/settings).
    ///
    /// Estilo casual premium das telas existentes: UiSkin (Kenney 9-slice + fonte TMP), fundo
    /// OPACO das telas, RectMask2D nos scrolls (NUNCA Mask por stencil), botões com UIButtonPop,
    /// telas nascem ocultas (alpha 0 + inativas) e populam o conteúdo data-driven em runtime.
    /// Cada asset do skin degrada individualmente para builtin — nunca cena quebrada.
    /// </summary>
    public static class SystemScreensFactory
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // Paleta coerente com MetaScreensFactory/ProjectSetup.
        private static readonly Color PanelDim = new Color(0.05f, 0.06f, 0.10f, 1f);
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);
        private static readonly Color Amber = new Color(1.00f, 0.75f, 0.15f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color HeaderBar = new Color(0.10f, 0.13f, 0.22f, 0.98f);
        private static readonly Color CardNavy = new Color(0.10f, 0.12f, 0.20f, 0.98f);

        [MenuItem("MAR Tools/Build System Screens")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Build System Screens não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.Log("MAR Tools: cenas Main/Game ausentes — rodando ProjectSetup.SetupProject() antes.");
                ProjectSetup.SetupProject();
            }

            AugmentMainScene();
            AugmentGameScene();

            AssetDatabase.SaveAssets();
            Debug.Log("MAR Tools: telas-sistema prontas — Settings/Events no menu (engrenagem + Eventos) " +
                      "e PauseOverlay no HUD da cena Game (botão de pause), tudo ligado aos controllers.");
        }

        // ================================================================== MAIN: Settings + Events

        private static void AugmentMainScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            var menu = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu == null)
            {
                Debug.LogError("MAR Tools: MainMenuController ausente na cena Main — rode MAR Tools/Setup Project.");
                return;
            }

            GameObject canvas = menu.gameObject;   // o MainMenuController vive no MainCanvas
            Transform canvasT = canvas.transform;

            // raiz própria das telas-sistema (idempotente: remove e reconstrói)
            RemoveExisting(canvasT, "SystemScreens");
            var screensRoot = new GameObject("SystemScreens", typeof(RectTransform));
            ((RectTransform)screensRoot.transform).SetParent(canvasT, false);
            Stretch((RectTransform)screensRoot.transform);

            SettingsScreen settings = BuildSettingsScreen(screensRoot.transform);
            EventsScreen events = BuildEventsScreen(screensRoot.transform);

            // Engrenagem no canto superior-direito (acima do header do menu).
            RemoveExisting(canvasT, "SettingsGear");
            Button gear = BuildGearButton(canvasT);

            // Botão EVENTOS adicionado à tab bar inferior (se existir); senão um botão flutuante.
            Button eventsBtn = AddEventsTab(canvasT);

            // Liga ao MainMenuController.
            Wire(menu, "_settingsButton", gear);
            Wire(menu, "_eventsButton", eventsBtn);
            Wire(menu, "_settingsScreen", settings);
            Wire(menu, "_eventsScreen", events);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static SettingsScreen BuildSettingsScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "SettingsScreen");
            var screen = panel.AddComponent<SettingsScreen>();

            Button back = ScreenHeaderTitleOnly(panel.transform, "CONFIGURAÇÕES", out TMP_Text title);

            // Lista de toggles (Som/Música/Vibração) — confinada abaixo do header.
            RectTransform toggles = ScrollList(panel.transform, "Toggles",
                offsetMin: new Vector2(40f, 760f), offsetMax: new Vector2(-40f, -200f), spacing: 18f);

            // Botões de ação (empilhados no terço inferior).
            TMP_Text restoreLabel;
            Button restore = SkinButton(panel.transform, "Restore", "RESTAURAR COMPRAS", 36f,
                UiSkin.ButtonBlue, Cyan, new Vector2(0.5f, 0f), new Vector2(0f, 560f),
                new Vector2(900f, 130f), out restoreLabel);
            Button privacy = SkinButton(panel.transform, "Privacy", "POLÍTICA DE PRIVACIDADE", 34f,
                UiSkin.ButtonGrey, Grey, new Vector2(0.5f, 0f), new Vector2(0f, 410f),
                new Vector2(900f, 120f), out _);
            Button credits = SkinButton(panel.transform, "Credits", "CRÉDITOS", 36f,
                UiSkin.ButtonGrey, Grey, new Vector2(0.5f, 0f), new Vector2(0f, 270f),
                new Vector2(900f, 120f), out _);

            TMP_Text version = Label(panel.transform, "Version", "Versão —", 28f, new Vector2(0.5f, 0f),
                new Vector2(0f, 180f), new Vector2(900f, 50f), TextSoft, TextAlignmentOptions.Center);

            // Painel de créditos/privacidade (cobre o miolo, fecha por botão). Nasce oculto.
            GameObject creditsPanel = Card(panel.transform, "CreditsPanel", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(960f, 1200f), CardNavy);
            TMP_Text creditsText = Label(creditsPanel.transform, "Text", "", 30f, new Vector2(0.5f, 1f),
                new Vector2(0f, -50f), new Vector2(880f, 980f), Color.white, TextAlignmentOptions.Top);
            ((RectTransform)creditsText.transform).pivot = new Vector2(0.5f, 1f);
            Button creditsClose = SkinButton(creditsPanel.transform, "Close", "FECHAR", 36f,
                UiSkin.ButtonGold, Amber, new Vector2(0.5f, 0f), new Vector2(0f, 50f),
                new Vector2(500f, 120f), out _);
            creditsPanel.SetActive(false);

            Wire(screen, "_titleText", title);
            Wire(screen, "_backButton", back);
            Wire(screen, "_togglesContent", toggles);
            Wire(screen, "_restoreButton", restore);
            Wire(screen, "_restoreLabel", restoreLabel);
            Wire(screen, "_privacyButton", privacy);
            Wire(screen, "_creditsButton", credits);
            Wire(screen, "_creditsPanel", creditsPanel);
            Wire(screen, "_creditsText", creditsText);
            Wire(screen, "_creditsCloseButton", creditsClose);
            Wire(screen, "_versionText", version);

            Deactivate(panel);
            return screen;
        }

        private static EventsScreen BuildEventsScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "EventsScreen");
            var screen = panel.AddComponent<EventsScreen>();

            Button back = ScreenHeaderTitleOnly(panel.transform, "EVENTOS", out TMP_Text title);

            // Dois cards de evento no topo (o conteúdo é preenchido pela tela em runtime).
            RectTransform daily = EventCardRect(panel.transform, "DailyCard", new Vector2(0f, -210f));
            RectTransform weekly = EventCardRect(panel.transform, "WeeklyCard", new Vector2(0f, -470f));

            // Título do ranking + nota honesta (local).
            Label(panel.transform, "RankTitle", "RANKING", 36f, new Vector2(0.5f, 1f),
                new Vector2(0f, -740f), new Vector2(900f, 50f), Gold, TextAlignmentOptions.Center);
            TMP_Text note = Label(panel.transform, "RankNote", "", 26f, new Vector2(0.5f, 1f),
                new Vector2(0f, -796f), new Vector2(900f, 40f), TextSoft, TextAlignmentOptions.Center);

            // Lista rolável do ranking, confinada abaixo dos cards e acima da tab bar.
            RectTransform ranking = ScrollList(panel.transform, "Ranking",
                offsetMin: new Vector2(20f, 220f), offsetMax: new Vector2(-20f, -840f), spacing: 12f);

            Wire(screen, "_titleText", title);
            Wire(screen, "_backButton", back);
            Wire(screen, "_dailyCard", daily);
            Wire(screen, "_weeklyCard", weekly);
            Wire(screen, "_rankingContent", ranking);
            Wire(screen, "_rankingNote", note);

            Deactivate(panel);
            return screen;
        }

        // card vazio (RectTransform + Image) — a EventsScreen pinta e popula em runtime
        private static RectTransform EventCardRect(Transform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(980f, 230f);
            Image img = go.GetComponent<Image>();
            Sprite skin = UiSkin.BadgeFlat;
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = CardNavy; }
            else img.color = CardNavy;
            img.raycastTarget = false;
            return rect;
        }

        // engrenagem (⚙) no canto superior-direito do menu
        private static Button BuildGearButton(Transform parent)
        {
            var go = new GameObject("SettingsGear", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 1f); rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -200f);
            rect.sizeDelta = new Vector2(120f, 120f);
            Image img = go.GetComponent<Image>();
            Sprite skin = UiSkin.ButtonGrey;
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = Grey;
            go.AddComponent<UIButtonPop>();
            TMP_Text gearLabel = Label(go.transform, "Label", "⚙", 64f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(120f, 120f), Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)gearLabel.transform);
            return go.GetComponent<Button>();
        }

        // adiciona um botão EVENTOS à MetaTabBar (se houver); senão, botão flutuante acima da barra
        private static Button AddEventsTab(Transform canvas)
        {
            Transform bar = canvas.Find("MetaTabBar");
            if (bar != null)
            {
                RemoveExisting(bar, "Tab_EVENTOS");
                return TabButton(bar, "EVENTOS");
            }

            // fallback: botão flutuante no canto inferior-esquerdo (acima da tab bar inexistente)
            RemoveExisting(canvas, "EventsButton");
            TMP_Text label;
            Button btn = SkinButton(canvas, "EventsButton", "EVENTOS", 32f, UiSkin.ButtonBlue, Cyan,
                new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(440f, 120f), out label);
            return btn;
        }

        // ================================================================== GAME: PauseOverlay + botão

        private static void AugmentGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var gameUi = Object.FindFirstObjectByType<GameUIController>(FindObjectsInactive.Include);
            if (gameUi == null)
            {
                Debug.LogError("MAR Tools: GameUIController ausente na cena Game — rode MAR Tools/Setup Project.");
                return;
            }

            GameObject hudCanvas = FindInScene(scene, "HudCanvas");
            if (hudCanvas == null)
            {
                Debug.LogError("MAR Tools: HudCanvas ausente na cena Game — rode MAR Tools/Setup Project.");
                return;
            }

            // Botão de PAUSE no canto superior-direito do HUD (some fora da corrida via controller).
            RemoveExisting(hudCanvas.transform, "PauseButton");
            Button pauseButton = BuildPauseButton(hudCanvas.transform);

            // PauseOverlay sobre o HUD (idempotente).
            RemoveExisting(hudCanvas.transform, "PauseOverlay");
            PauseOverlay overlay = BuildPauseOverlay(hudCanvas.transform);

            Wire(gameUi, "_pauseButton", pauseButton);
            Wire(gameUi, "_pauseOverlay", overlay);
            // _settingsScreen fica nulo na cena Game (sem Settings aqui) — o controller degrada.

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Button BuildPauseButton(Transform hudCanvas)
        {
            var go = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(hudCanvas, false);
            rect.anchorMin = new Vector2(1f, 1f); rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -30f);
            rect.sizeDelta = new Vector2(110f, 110f);
            Image img = go.GetComponent<Image>();
            Sprite skin = UiSkin.ButtonGrey;
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = Grey;
            go.AddComponent<UIButtonPop>();
            TMP_Text label = Label(go.transform, "Label", "II", 48f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(110f, 110f), Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            return go.GetComponent<Button>();
        }

        private static PauseOverlay BuildPauseOverlay(Transform hudCanvas)
        {
            // dim fullscreen opaco-translúcido + CanvasGroup (UIOverlay exige)
            var panel = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(hudCanvas, false);
            Stretch(rect);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
            var overlay = panel.AddComponent<PauseOverlay>();

            // cartão central com os botões
            GameObject card = Card(panel.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(820f, 980f), new Color(0.10f, 0.12f, 0.20f, 0.99f));

            TMP_Text title = Label(card.transform, "Title", "PAUSADO", 70f, new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(760f, 100f), Gold, TextAlignmentOptions.Center);

            Button resume = SkinButton(card.transform, "Resume", "CONTINUAR", 48f, UiSkin.ButtonGreen, Green,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(680f, 150f), out _);
            Button restart = SkinButton(card.transform, "Restart", "REINICIAR FASE", 44f, UiSkin.ButtonBlue, Cyan,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(680f, 150f), out _);
            Button settings = SkinButton(card.transform, "Settings", "CONFIGURAÇÕES", 44f, UiSkin.ButtonGrey, Grey,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(680f, 150f), out _);
            Button menu = SkinButton(card.transform, "Menu", "MENU", 44f, UiSkin.ButtonGrey, Grey,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(680f, 150f), out _);

            Wire(overlay, "_titleText", title);
            Wire(overlay, "_resumeButton", resume);
            Wire(overlay, "_restartButton", restart);
            Wire(overlay, "_settingsButton", settings);
            Wire(overlay, "_menuButton", menu);

            Deactivate(panel);
            return overlay;
        }

        // ================================================================== pedaços comuns de UI

        private static GameObject Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            Image bg = go.GetComponent<Image>();
            Sprite grad = UiSkin.MenuGradient;
            // fundo 100% OPACO (P10): a tela cobre o menu por completo, sem vazamento
            if (grad != null) { bg.sprite = grad; bg.color = Color.white; }
            else bg.color = new Color(PanelDim.r, PanelDim.g, PanelDim.b, 1f);
            return go;
        }

        private static Button ScreenHeaderTitleOnly(Transform parent, string title, out TMP_Text titleText)
        {
            HeaderBackground(parent);
            Button back = BackButton(parent);
            titleText = Label(parent, "Title", title, 50f, new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(760f, 70f), Gold, TextAlignmentOptions.Center);
            return back;
        }

        private static void HeaderBackground(Transform parent)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f); rect.sizeDelta = new Vector2(0f, 150f);
            rect.anchoredPosition = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.color = HeaderBar; img.raycastTarget = false;
        }

        private static Button BackButton(Transform parent)
        {
            var go = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f); rect.sizeDelta = new Vector2(140f, 110f);
            rect.anchoredPosition = new Vector2(24f, -22f);
            Image img = go.GetComponent<Image>();
            Sprite skin = UiSkin.ButtonGrey;
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = Grey;
            go.AddComponent<UIButtonPop>();
            TMP_Text t = Label(go.transform, "Label", "◄", 52f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(140f, 110f), Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)t.transform);
            return go.GetComponent<Button>();
        }

        private static Button TabButton(Transform parent, string label)
        {
            var go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            Sprite skin = UiSkin.ButtonGrey;
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.18f, 0.22f, 0.30f);
            go.AddComponent<UIButtonPop>();
            TMP_Text t = Label(go.transform, "Label", label, 26f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(180f, 100f), Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)t.transform);
            return go.GetComponent<Button>();
        }

        // ---------------------------------------------------------------- scroll (RectMask2D, nunca Mask por stencil)

        private static readonly Vector2 DefaultScrollOffsetMin = new Vector2(20f, 220f);
        private static readonly Vector2 DefaultScrollOffsetMax = new Vector2(-20f, -170f);

        private static RectTransform ScrollList(Transform parent, string name, Vector2 offsetMin,
                                                Vector2 offsetMax, float spacing)
        {
            RectTransform content = ScrollArea(parent, name, offsetMin, offsetMax);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        // RectMask2D (recorte por retângulo, funciona offscreen no -menuShowcase) — NUNCA Mask
        // por stencil, que não renderiza quando a câmera desenha num RenderTexture offscreen.
        private static RectTransform ScrollArea(Transform parent, string name, Vector2? offsetMin, Vector2? offsetMax)
        {
            var viewportGo = new GameObject(name + "Viewport", typeof(RectTransform),
                typeof(RectMask2D), typeof(ScrollRect));
            var vpRect = (RectTransform)viewportGo.transform;
            vpRect.SetParent(parent, false);
            vpRect.anchorMin = new Vector2(0f, 0f); vpRect.anchorMax = new Vector2(1f, 1f);
            vpRect.offsetMin = offsetMin ?? DefaultScrollOffsetMin;
            vpRect.offsetMax = offsetMax ?? DefaultScrollOffsetMax;

            var contentGo = new GameObject(name + "Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(vpRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = vpRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return contentRect;
        }

        // ---------------------------------------------------------------- elementos

        private static GameObject Card(Transform parent, string name, Vector2 anchor, Vector2 pos,
                                       Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = pos; rect.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            Sprite skin = UiSkin.BadgeFlat;
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = color; }
            else img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private static TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                                      Vector2 pos, Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            ApplyFont(t);
            return t;
        }

        private static Button SkinButton(Transform parent, string name, string label, float size, Sprite skin,
                                         Color fallback, Vector2 anchor, Vector2 pos, Vector2 sizeDelta,
                                         out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            Image img = go.GetComponent<Image>();
            if (skin != null) { img.sprite = skin; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = fallback;
            go.AddComponent<UIButtonPop>();
            labelText = Label(go.transform, "Label", label, size, new Vector2(0.5f, 0.5f),
                Vector2.zero, sizeDelta, Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)labelText.transform);
            return go.GetComponent<Button>();
        }

        // ---------------------------------------------------------------- skin / util

        private static void ApplyFont(TMP_Text text)
        {
            TMP_FontAsset skinFont = UiSkin.FontAsset;
            if (skinFont != null) { text.font = skinFont; return; }
            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (liberation == null) return;
            text.font = liberation;
            text.fontStyle = FontStyles.Bold;
            Material outline = UiSkin.FallbackOutlineMaterial;
            if (outline != null) text.fontSharedMaterial = outline;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private static void Deactivate(GameObject panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            panel.SetActive(false);
        }

        private static void RemoveExisting(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                Transform found = FindChildRecursive(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name) return child;
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Wire(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"MAR Tools: campo '{fieldName}' não existe em {target.GetType().Name} — wiring ignorado.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
