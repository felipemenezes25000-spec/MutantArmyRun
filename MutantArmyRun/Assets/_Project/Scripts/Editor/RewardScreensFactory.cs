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
    /// MAR Tools/Build Reward Screens — augmenta a cena Main com as telas/overlays de RECOMPENSA
    /// (brief F-SeasonPass/Chest/Celebration), no mesmo idioma do SystemScreensFactory/JuiceFactory:
    /// OpenScene → constrói no lugar → liga por SerializedProperty → salva. IDEMPOTENTE (remove a
    /// raiz própria e reconstrói; nunca duplica).
    ///
    /// Cria, no MainCanvas:
    ///  - SeasonPassScreen (UIScreen): trilha de 30 níveis (grátis vs premium), barra de progresso,
    ///    COMPRAR PASSE e COLETAR. Ligada ao MainMenuController (_seasonPassScreen) e aberta pelo
    ///    botão PASSE da Loja (converte o card "SeasonPass" da ShopScreen em Button e liga
    ///    ShopScreen._seasonPassButton).
    ///  - ChestRevealOverlay (UIOverlay): revelação de baú carta-a-carta (assina OnChestOpened).
    ///  - CelebrationOverlay (UIOverlay): desbloqueio de tropa / level-up / resgate de passe.
    ///
    /// Estilo casual premium: fundo OPACO nas telas, scroll com RectMask2D (NUNCA Mask por stencil —
    /// invisível offscreen no -menuShowcase), botões com UIButtonPop, UiSkin com degradação por
    /// elemento. As listas (trilha do passe, cartas do baú) são DATA-DRIVEN em runtime.
    /// </summary>
    public static class RewardScreensFactory
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        // Paleta coerente com MetaScreensFactory/SystemScreensFactory.
        private static readonly Color PanelDim = new Color(0.05f, 0.06f, 0.10f, 1f);
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);
        private static readonly Color Amber = new Color(1.00f, 0.75f, 0.15f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color HeaderBar = new Color(0.10f, 0.13f, 0.22f, 0.98f);
        private static readonly Color CardNavy = new Color(0.10f, 0.12f, 0.20f, 0.98f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.84f);

        [MenuItem("MAR Tools/Build Reward Screens")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Build Reward Screens não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                Debug.Log("MAR Tools: cena Main ausente — rodando ProjectSetup.SetupProject() antes.");
                ProjectSetup.SetupProject();
            }

            AugmentMainScene();

            AssetDatabase.SaveAssets();
            Debug.Log("MAR Tools: telas de recompensa prontas — Passe de Temporada (aberto pela Loja), " +
                      "ChestRevealOverlay (abertura de baú) e CelebrationOverlay (desbloqueio/level-up), " +
                      "tudo ligado ao menu/Loja.");
        }

        private static void AugmentMainScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            var menu = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu == null)
            {
                Debug.LogError("MAR Tools: MainMenuController ausente na cena Main — rode MAR Tools/Setup Project.");
                return;
            }

            GameObject canvas = menu.gameObject;   // MainMenuController vive no MainCanvas
            Transform canvasT = canvas.transform;

            // Raiz própria (idempotente): remove e reconstrói telas + overlays de recompensa.
            RemoveExisting(canvasT, "RewardScreens");
            var root = new GameObject("RewardScreens", typeof(RectTransform));
            ((RectTransform)root.transform).SetParent(canvasT, false);
            Stretch((RectTransform)root.transform);

            SeasonPassScreen pass = BuildSeasonPassScreen(root.transform);
            ChestRevealOverlay chest = BuildChestRevealOverlay(root.transform);
            CelebrationOverlay celebration = BuildCelebrationOverlay(root.transform);

            // Liga a tela de passe ao menu.
            Wire(menu, "_seasonPassScreen", pass);

            // Liga o botão PASSE da Loja: converte o card "SeasonPass" da ShopScreen em Button.
            WireShopSeasonPassButton(canvasT);

            // Overlays existem na cena para os eventos automáticos os encontrarem em runtime
            // (ChestRevealOverlay assina RewardSystem.OnChestOpened; CelebrationOverlay assina
            // UnitManager.OnTroopChanged + EconomySystem.OnPlayerLevelUp) — nada a ligar por campo.
            _ = chest; _ = celebration;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ================================================================== Passe de Temporada (UIScreen)

        private static SeasonPassScreen BuildSeasonPassScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "SeasonPassScreen");
            var screen = panel.AddComponent<SeasonPassScreen>();

            Button back = ScreenHeaderTitleOnly(panel.transform, "PASSE DE TEMPORADA", out TMP_Text title);

            // Barra de progresso do passe (abaixo do header).
            TMP_Text tierText = Label(panel.transform, "Tier", "NÍVEL 1 / 30", 34f, new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(900f, 50f), Gold, TextAlignmentOptions.Center);
            RectTransform xpFill = ProgressBar(panel.transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -250f), new Vector2(900f, 36f), out _);
            TMP_Text xpText = Label(panel.transform, "Xp", "0 / 100 XP", 26f, new Vector2(0.5f, 1f),
                new Vector2(0f, -300f), new Vector2(900f, 40f), TextSoft, TextAlignmentOptions.Center);

            // Cabeçalho das duas trilhas.
            Label(panel.transform, "FreeHdr", "GRÁTIS", 26f, new Vector2(0.5f, 1f),
                new Vector2(-180f, -350f), new Vector2(330f, 36f), Cyan, TextAlignmentOptions.Center);
            Label(panel.transform, "PremHdr", "PREMIUM", 26f, new Vector2(0.5f, 1f),
                new Vector2(180f, -350f), new Vector2(330f, 36f), Gold, TextAlignmentOptions.Center);

            // Lista rolável da trilha (uma linha por nível) — confinada entre o cabeçalho e os botões.
            RectTransform tracks = ScrollList(panel.transform, "Tracks",
                offsetMin: new Vector2(20f, 360f), offsetMax: new Vector2(-20f, -390f), spacing: 14f);

            // Ações no terço inferior: COMPRAR PASSE + COLETAR.
            TMP_Text buyLabel;
            Button buy = SkinButton(panel.transform, "Buy", "COMPRAR PASSE", 38f, UiSkin.ButtonGold, Gold,
                new Vector2(0.5f, 0f), new Vector2(0f, 360f), new Vector2(940f, 140f), out buyLabel);
            TMP_Text claimLabel;
            Button claim = SkinButton(panel.transform, "Claim", "COLETAR RECOMPENSAS", 38f, UiSkin.ButtonGreen, Green,
                new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(940f, 140f), out claimLabel);

            Wire(screen, "_titleText", title);
            Wire(screen, "_backButton", back);
            Wire(screen, "_tierText", tierText);
            Wire(screen, "_xpFill", xpFill);
            Wire(screen, "_xpText", xpText);
            Wire(screen, "_tracksContent", tracks);
            Wire(screen, "_buyButton", buy);
            Wire(screen, "_buyLabel", buyLabel);
            Wire(screen, "_claimButton", claim);
            Wire(screen, "_claimLabel", claimLabel);

            Deactivate(panel);
            return screen;
        }

        // ================================================================== ChestRevealOverlay (UIOverlay)

        private static ChestRevealOverlay BuildChestRevealOverlay(Transform parent)
        {
            // dim fullscreen + CanvasGroup (UIOverlay exige). Sobe SOBRE a Loja.
            var go = new GameObject("ChestRevealOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            go.GetComponent<Image>().color = Dim;
            var overlay = go.AddComponent<ChestRevealOverlay>();

            TMP_Text title = Label(go.transform, "Title", "BAÚ ABERTO!", 64f, new Vector2(0.5f, 1f),
                new Vector2(0f, -220f), new Vector2(900f, 90f), Gold, TextAlignmentOptions.Center);
            TMP_Text currency = Label(go.transform, "Currency", "", 36f, new Vector2(0.5f, 1f),
                new Vector2(0f, -320f), new Vector2(900f, 60f), Amber, TextAlignmentOptions.Center);

            // Faixa horizontal de cartas (RectMask2D; cartas instanciadas em runtime).
            RectTransform cards = ScrollGridHorizontal(go.transform, "Cards",
                offsetMin: new Vector2(40f, 520f), offsetMax: new Vector2(-40f, -420f));

            TMP_Text collectLabel;
            Button collect = SkinButton(go.transform, "Collect", "COLETAR", 44f, UiSkin.ButtonGreen, Green,
                new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(760f, 150f), out collectLabel);

            Wire(overlay, "_titleText", title);
            Wire(overlay, "_currencyText", currency);
            Wire(overlay, "_cardsContent", cards);
            Wire(overlay, "_collectButton", collect);
            Wire(overlay, "_collectLabel", collectLabel);

            // ATIVO mas invisível (alpha 0): o overlay precisa rodar Awake p/ assinar
            // RewardSystem.OnChestOpened ANTES de qualquer baú abrir. Hide() do UIOverlay desativa
            // o GameObject, mas o delegate persiste (não é destruído) — o próximo baú reativa via Show().
            HideVisualOnly(go);
            return overlay;
        }

        // ================================================================== CelebrationOverlay (UIOverlay)

        private static CelebrationOverlay BuildCelebrationOverlay(Transform parent)
        {
            var go = new GameObject("CelebrationOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            go.GetComponent<Image>().color = Dim;
            var overlay = go.AddComponent<CelebrationOverlay>();

            // Root do confete (atrás do cartão) — partículas geradas em código pelo overlay.
            var confettiGo = new GameObject("Confetti", typeof(RectTransform));
            var confettiRect = (RectTransform)confettiGo.transform;
            confettiRect.SetParent(rect, false);
            confettiRect.anchorMin = new Vector2(0.5f, 0.5f); confettiRect.anchorMax = new Vector2(0.5f, 0.5f);
            confettiRect.pivot = new Vector2(0.5f, 0.5f);
            confettiRect.anchoredPosition = Vector2.zero; confettiRect.sizeDelta = new Vector2(1080f, 1080f);

            // Cartão central grande (recebe o ScalePop).
            GameObject card = Card(go.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(900f, 620f), CardNavy);

            TMP_Text kicker = Label(card.transform, "Kicker", "NOVA TROPA", 40f, new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(820f, 60f), Cyan, TextAlignmentOptions.Center);
            TMP_Text title = Label(card.transform, "Title", "", 72f, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(840f, 140f), Color.white, TextAlignmentOptions.Center);
            TMP_Text subtitle = Label(card.transform, "Subtitle", "", 34f, new Vector2(0.5f, 0f),
                new Vector2(0f, 90f), new Vector2(840f, 80f), Gold, TextAlignmentOptions.Center);

            Wire(overlay, "_card", (RectTransform)card.transform);
            Wire(overlay, "_kickerText", kicker);
            Wire(overlay, "_titleText", title);
            Wire(overlay, "_subtitleText", subtitle);
            Wire(overlay, "_confettiRoot", confettiRect);

            // ATIVO mas invisível: precisa rodar Awake p/ assinar OnTroopChanged/OnPlayerLevelUp
            // antes do 1º desbloqueio/level-up. (mesma lógica do ChestRevealOverlay)
            HideVisualOnly(go);
            return overlay;
        }

        // ================================================================== Loja: liga o botão PASSE

        /// <summary>
        /// Converte o card "SeasonPass" da ShopScreen (criado pela MetaScreensFactory como display-only)
        /// em um Button clicável e liga ShopScreen._seasonPassButton. Idempotente: se já houver Button,
        /// reaproveita. Degrada com aviso se a ShopScreen/card não existir (cena não reconstruída).
        /// </summary>
        private static void WireShopSeasonPassButton(Transform canvas)
        {
            var shop = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (shop == null)
            {
                Debug.LogWarning("MAR Tools: ShopScreen ausente — botão PASSE não ligado (rode Setup Project + MetaScreens).");
                return;
            }

            Transform card = FindChildRecursive(shop.transform, "SeasonPass");
            if (card == null)
            {
                Debug.LogWarning("MAR Tools: card 'SeasonPass' não encontrado na ShopScreen — botão PASSE não ligado.");
                return;
            }

            GameObject cardGo = card.gameObject;
            Image img = cardGo.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;   // o card foi criado raycast-off; reativa para clicar
            Button btn = cardGo.GetComponent<Button>();
            if (btn == null) btn = cardGo.AddComponent<Button>();
            if (cardGo.GetComponent<UIButtonPop>() == null) cardGo.AddComponent<UIButtonPop>();

            Wire(shop, "_seasonPassButton", btn);
        }

        // ================================================================== pedaços comuns

        private static GameObject Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            Image bg = go.GetComponent<Image>();
            Sprite grad = UiSkin.MenuGradient;
            if (grad != null) { bg.sprite = grad; bg.color = Color.white; }   // fundo OPACO (P10)
            else bg.color = new Color(PanelDim.r, PanelDim.g, PanelDim.b, 1f);
            return go;
        }

        private static Button ScreenHeaderTitleOnly(Transform parent, string title, out TMP_Text titleText)
        {
            HeaderBackground(parent);
            Button back = BackButton(parent);
            titleText = Label(parent, "Title", title, 48f, new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(800f, 70f), Gold, TextAlignmentOptions.Center);
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

        // ---------------------------------------------------------------- barra de progresso

        /// <summary>Barra de fundo + fill ANCORADO (largura via anchorMax.x), não Image.Type.Filled.</summary>
        private static RectTransform ProgressBar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, out Image fillImg)
        {
            var bgGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(parent, false);
            bgRect.anchorMin = anchor; bgRect.anchorMax = anchor; bgRect.pivot = new Vector2(0.5f, anchor.y);
            bgRect.anchoredPosition = pos; bgRect.sizeDelta = size;
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            bgGo.GetComponent<Image>().raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(bgRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f); fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            fillImg = fillGo.GetComponent<Image>();
            fillImg.color = Gold; fillImg.raycastTarget = false;
            return fillRect;
        }

        // ---------------------------------------------------------------- scroll (RectMask2D — nunca stencil)

        private static RectTransform ScrollList(Transform parent, string name, Vector2 offsetMin,
                                                Vector2 offsetMax, float spacing)
        {
            RectTransform content = ScrollArea(parent, name, offsetMin, offsetMax, horizontal: false);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        private static RectTransform ScrollGridHorizontal(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform content = ScrollArea(parent, name, offsetMin, offsetMax, horizontal: true);
            var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.padding = new RectOffset(20, 20, 20, 20);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        // RectMask2D (recorte por retângulo, funciona offscreen no -menuShowcase) — NUNCA Mask por stencil.
        private static RectTransform ScrollArea(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax, bool horizontal)
        {
            var viewportGo = new GameObject(name + "Viewport", typeof(RectTransform),
                typeof(RectMask2D), typeof(ScrollRect));
            var vpRect = (RectTransform)viewportGo.transform;
            vpRect.SetParent(parent, false);
            vpRect.anchorMin = new Vector2(0f, 0f); vpRect.anchorMax = new Vector2(1f, 1f);
            vpRect.offsetMin = offsetMin; vpRect.offsetMax = offsetMax;

            var contentGo = new GameObject(name + "Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(vpRect, false);
            if (horizontal)
            {
                contentRect.anchorMin = new Vector2(0f, 0f); contentRect.anchorMax = new Vector2(0f, 1f);
                contentRect.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
            }
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = vpRect;
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
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

        /// <summary>
        /// Esconde visualmente (alpha 0, sem raycast) mas MANTÉM o GameObject ATIVO — para overlays
        /// orientados a evento que precisam rodar Awake/OnEnable e assinar o bus antes do 1º gatilho.
        /// </summary>
        private static void HideVisualOnly(GameObject panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            // GameObject permanece ativo de propósito (não SetActive(false)).
        }

        private static void RemoveExisting(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
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
