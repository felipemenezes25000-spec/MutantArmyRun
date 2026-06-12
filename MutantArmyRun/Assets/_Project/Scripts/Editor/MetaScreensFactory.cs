using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.UI;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Constrói as 5 telas de META na cena Main (SCR-06 Tropas, SCR-07 Upgrades, SCR-08 Loja,
    /// SCR-09 Mapa, PAINEL DIÁRIO) + a tab bar de navegação, e liga tudo ao MainMenuController
    /// por SerializedProperty. Idempotente: chamado pelo ProjectSetup ao (re)criar a Main —
    /// remove instâncias antigas e reconstrói. As telas nascem ocultas (alpha 0 + inativas);
    /// a grade/lista de cada uma é DATA-DRIVEN em runtime (catálogo só conhecido em play).
    ///
    /// Estilo casual premium: usa o UiSkin (sprites Kenney 9-slice + fonte TMP) quando presente,
    /// degradando para builtin por elemento — nunca cena quebrada. Botões recebem UIButtonPop
    /// (ScalePop no press) como o resto da UI.
    /// </summary>
    public static class MetaScreensFactory
    {
        // Paleta coerente com o ProjectSetup.
        private static readonly Color PanelDim = new Color(0.05f, 0.06f, 0.10f, 0.97f);
        private static readonly Color CardNavy = new Color(0.10f, 0.12f, 0.20f, 0.98f);
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);
        private static readonly Color Amber = new Color(1.00f, 0.75f, 0.15f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color HeaderBar = new Color(0.10f, 0.13f, 0.22f, 0.98f);

        /// <summary>
        /// Cria as telas no canvas da Main e a tab bar; liga ao menu. Chamado pelo ProjectSetup
        /// depois de o MainMenuController já existir com seus campos básicos.
        /// </summary>
        public static void Build(GameObject menuCanvas, MainMenuController menu)
        {
            if (menuCanvas == null || menu == null)
            {
                Debug.LogWarning("MAR Tools: MetaScreensFactory.Build chamado sem canvas/menu — ignorado.");
                return;
            }

            Transform canvas = menuCanvas.transform;
            RemoveExisting(canvas, "MetaScreens");
            RemoveExisting(canvas, "MetaTabBar");

            var screensRoot = new GameObject("MetaScreens", typeof(RectTransform));
            ((RectTransform)screensRoot.transform).SetParent(canvas, false);
            Stretch((RectTransform)screensRoot.transform);

            TroopsScreen troops = BuildTroopsScreen(screensRoot.transform);
            UpgradesScreen upgrades = BuildUpgradesScreen(screensRoot.transform);
            ShopScreen shop = BuildShopScreen(screensRoot.transform);
            MapScreen map = BuildMapScreen(screensRoot.transform);
            DailyScreen daily = BuildDailyScreen(screensRoot.transform);

            // Tab bar (zona quente inferior, doc 09 §4.1): 5 botões.
            Button troopsBtn, upgradesBtn, shopBtn, mapBtn, dailyBtn;
            BuildTabBar(canvas, out troopsBtn, out upgradesBtn, out shopBtn, out mapBtn, out dailyBtn);

            // Liga TUDO ao MainMenuController.
            Wire(menu, "_troopsButton", troopsBtn);
            Wire(menu, "_upgradesButton", upgradesBtn);
            Wire(menu, "_shopButton", shopBtn);
            Wire(menu, "_mapButton", mapBtn);
            Wire(menu, "_dailyButton", dailyBtn);
            Wire(menu, "_troopsScreen", troops);
            Wire(menu, "_upgradesScreen", upgrades);
            Wire(menu, "_shopScreen", shop);
            Wire(menu, "_mapScreen", map);
            Wire(menu, "_dailyScreen", daily);

            Debug.Log("MAR Tools: telas de meta (Tropas/Upgrades/Loja/Mapa/Diário) criadas e ligadas ao menu.");
        }

        // ---------------------------------------------------------------- SCR-06 Tropas

        private static TroopsScreen BuildTroopsScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "TroopsScreen");
            var screen = panel.AddComponent<TroopsScreen>();

            TMP_Text coins, gems;
            Button back = ScreenHeader(panel.transform, "TROPAS", out coins, out gems);

            // Grade rolável de cartas (3 colunas) — GridLayoutGroup.
            RectTransform grid = ScrollGrid(panel.transform, "Grid", new Vector2(0f, 80f),
                new Vector2(-40f, -700f), cellSize: new Vector2(300f, 300f), columns: 3, spacing: 22f);

            // Painel de detalhe (desliza de baixo, thumb zone).
            GameObject detail = Card(panel.transform, "Detail", new Vector2(0.5f, 0f),
                new Vector2(0f, 360f), new Vector2(980f, 640f), CardNavy);
            TMP_Text dTitle = Label(detail.transform, "Title", "—", 44f, new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(900f, 60f), Gold, TextAlignmentOptions.Top);
            TMP_Text dStats = Label(detail.transform, "Stats", "", 32f, new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(900f, 200f), TextSoft, TextAlignmentOptions.Top);
            TMP_Text dShards = Label(detail.transform, "Shards", "", 32f, new Vector2(0.5f, 1f),
                new Vector2(0f, -330f), new Vector2(900f, 50f), Cyan, TextAlignmentOptions.Top);
            TMP_Text evoLabel;
            Button evolve = SkinButton(detail.transform, "Evolve", "EVOLUIR", 40f, UiSkin.ButtonGreen, Green,
                new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(700f, 130f), out evoLabel);

            Wire(screen, "_coinsText", coins);
            Wire(screen, "_gemsText", gems);
            Wire(screen, "_backButton", back);
            Wire(screen, "_gridContent", grid);
            Wire(screen, "_detailPanel", detail);
            Wire(screen, "_detailTitle", dTitle);
            Wire(screen, "_detailStats", dStats);
            Wire(screen, "_detailShards", dShards);
            Wire(screen, "_evolveButton", evolve);
            Wire(screen, "_evolveLabel", evoLabel);

            Deactivate(panel);
            return screen;
        }

        // ---------------------------------------------------------------- SCR-07 Upgrades

        private static UpgradesScreen BuildUpgradesScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "UpgradesScreen");
            var screen = panel.AddComponent<UpgradesScreen>();

            TMP_Text coins, gems;
            Button back = ScreenHeader(panel.transform, "UPGRADES", out coins, out gems);

            RectTransform list = ScrollList(panel.transform, "List", new Vector2(0f, 60f),
                new Vector2(-40f, -260f), spacing: 18f);

            Wire(screen, "_coinsText", coins);
            Wire(screen, "_backButton", back);
            Wire(screen, "_listContent", list);

            Deactivate(panel);
            return screen;
        }

        // ---------------------------------------------------------------- SCR-08 Loja

        private static ShopScreen BuildShopScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "ShopScreen");
            var screen = panel.AddComponent<ShopScreen>();

            TMP_Text coins, gems;
            Button back = ScreenHeader(panel.transform, "LOJA", out coins, out gems);

            // Faixa de resultado de baú (topo, oculta).
            TMP_Text resultBanner = Label(panel.transform, "ResultBanner", "", 30f, new Vector2(0.5f, 1f),
                new Vector2(0f, -210f), new Vector2(980f, 110f), Gold, TextAlignmentOptions.Center);
            resultBanner.gameObject.SetActive(false);

            // Remover anúncios (destaque fixo no topo — placeholder honesto).
            GameObject removeAds = Card(panel.transform, "RemoveAds", new Vector2(0.5f, 1f),
                new Vector2(0f, -360f), new Vector2(980f, 150f), new Color(0.18f, 0.14f, 0.28f, 0.98f));
            TMP_Text removeAdsLabel = Label(removeAds.transform, "Label", "", 34f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(940f, 130f), Color.white, TextAlignmentOptions.Center);

            // Passe de temporada (placeholder).
            GameObject pass = Card(panel.transform, "SeasonPass", new Vector2(0.5f, 1f),
                new Vector2(0f, -530f), new Vector2(980f, 130f), new Color(0.16f, 0.20f, 0.10f, 0.98f));
            TMP_Text passLabel = Label(pass.transform, "Label", "", 32f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(940f, 110f), Gold, TextAlignmentOptions.Center);

            // Baú grátis diário (funcional).
            TMP_Text freeLabel;
            Button freeChest = SkinButton(panel.transform, "FreeChest", "🎁 BAÚ GRÁTIS", 38f,
                UiSkin.ButtonGreen, Green, new Vector2(0.5f, 0f), new Vector2(0f, 540f),
                new Vector2(900f, 150f), out freeLabel);

            // Baú por gemas (funcional).
            TMP_Text gemChestLabel;
            Button gemChest = SkinButton(panel.transform, "GemChest", "BAÚ RARO", 38f,
                UiSkin.ButtonBlue, Cyan, new Vector2(0.5f, 0f), new Vector2(0f, 360f),
                new Vector2(900f, 150f), out gemChestLabel);

            Wire(screen, "_coinsText", coins);
            Wire(screen, "_gemsText", gems);
            Wire(screen, "_backButton", back);
            Wire(screen, "_resultBanner", resultBanner);
            Wire(screen, "_freeChestButton", freeChest);
            Wire(screen, "_freeChestLabel", freeLabel);
            Wire(screen, "_gemChestButton", gemChest);
            Wire(screen, "_gemChestLabel", gemChestLabel);
            Wire(screen, "_removeAdsLabel", removeAdsLabel);
            Wire(screen, "_seasonPassLabel", passLabel);

            // Baús usam o contrato RewardSystem.OpenChest(ChestType) em runtime — sem precisar
            // ligar RewardConfigSO no edit-time (as drop tables vêm do Domain.ChestMath).

            Deactivate(panel);
            return screen;
        }

        // ---------------------------------------------------------------- SCR-09 Mapa

        private static MapScreen BuildMapScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "MapScreen");
            var screen = panel.AddComponent<MapScreen>();

            Button back = ScreenHeaderTitleOnly(panel.transform, "MAPA", out TMP_Text title);

            RectTransform list = ScrollList(panel.transform, "Worlds", new Vector2(0f, 60f),
                new Vector2(-40f, -260f), spacing: 20f);

            Wire(screen, "_titleText", title);
            Wire(screen, "_backButton", back);
            Wire(screen, "_listContent", list);

            Deactivate(panel);
            return screen;
        }

        // ---------------------------------------------------------------- PAINEL DIÁRIO

        private static DailyScreen BuildDailyScreen(Transform parent)
        {
            GameObject panel = Panel(parent, "DailyScreen");
            var screen = panel.AddComponent<DailyScreen>();

            Button back = ScreenHeaderTitleOnly(panel.transform, "DIÁRIO", out TMP_Text title);

            // Calendário de 7 dias (horizontal).
            Label(panel.transform, "LoginTitle", "RECOMPENSA DE LOGIN", 34f, new Vector2(0.5f, 1f),
                new Vector2(0f, -210f), new Vector2(900f, 50f), Gold, TextAlignmentOptions.Center);
            RectTransform calendar = HorizontalStrip(panel.transform, "Calendar",
                new Vector2(0f, -280f), new Vector2(960f, 180f), spacing: 14f);

            TMP_Text claimLabel;
            Button claim = SkinButton(panel.transform, "ClaimLogin", "RECLAMAR", 36f, UiSkin.ButtonGold, Amber,
                new Vector2(0.5f, 1f), new Vector2(0f, -500f), new Vector2(900f, 130f), out claimLabel);

            // Missões.
            Label(panel.transform, "MissionsTitle", "MISSÕES DIÁRIAS", 34f, new Vector2(0.5f, 1f),
                new Vector2(0f, -660f), new Vector2(900f, 50f), Cyan, TextAlignmentOptions.Center);
            // Viewport das missões confinado ABAIXO do título (topo em -720) e acima da tab bar
            // (base em 220) — senão a lista sobrepõe o calendário/RECLAMAR (o ScrollArea-padrão
            // ocuparia todo o miolo). offsetMin/Max explícitos resolvem o empilhamento.
            RectTransform missions = ScrollList(panel.transform, "Missions", new Vector2(0f, 60f),
                new Vector2(-40f, -740f), spacing: 16f,
                offsetMin: new Vector2(20f, 220f), offsetMax: new Vector2(-20f, -720f));

            Wire(screen, "_titleText", title);
            Wire(screen, "_backButton", back);
            Wire(screen, "_calendarContent", calendar);
            Wire(screen, "_claimLoginButton", claim);
            Wire(screen, "_claimLoginLabel", claimLabel);
            Wire(screen, "_missionsContent", missions);

            Deactivate(panel);
            return screen;
        }

        // ---------------------------------------------------------------- Tab bar

        private static void BuildTabBar(Transform canvas, out Button troops, out Button upgrades,
                                        out Button shop, out Button map, out Button daily)
        {
            var bar = new GameObject("MetaTabBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-40f, 170f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            bar.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.10f, 0.92f);
            var hlg = bar.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f; hlg.padding = new RectOffset(14, 14, 14, 14);
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            troops = TabButton(rect, "TROPAS");
            upgrades = TabButton(rect, "UPGRADES");
            shop = TabButton(rect, "LOJA");
            map = TabButton(rect, "MAPA");
            daily = TabButton(rect, "DIÁRIO");
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
            TMP_Text t = Label(go.transform, "Label", label, 28f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(180f, 100f), Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)t.transform);
            return go.GetComponent<Button>();
        }

        // ---------------------------------------------------------------- pedaços comuns de tela

        private static GameObject Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            Image bg = go.GetComponent<Image>();
            Sprite grad = UiSkin.MenuGradient;
            // Fundo 100% opaco: a 0.96 o menu (logo/JOGAR/FASE) vazava por trás. Alpha cheio
            // garante que cada tela de meta cubra o menu por completo (P10 — sem vazamento).
            if (grad != null) { bg.sprite = grad; bg.color = Color.white; }
            else bg.color = new Color(PanelDim.r, PanelDim.g, PanelDim.b, 1f);
            return go;
        }

        /// <summary>Header com voltar + título + chips de carteira (moeda/gema).</summary>
        private static Button ScreenHeader(Transform parent, string title, out TMP_Text coins, out TMP_Text gems)
        {
            HeaderBackground(parent);
            Button back = BackButton(parent);
            // Título alinhado à ESQUERDA, logo após o botão Voltar: centralizado ele colidia com
            // o chip de moedas (que ocupa o centro-direita do header) e ficava escondido atrás.
            TMP_Text t = Label(parent, "Title", title, 46f, new Vector2(0f, 1f),
                new Vector2(190f, -78f), new Vector2(360f, 70f), Gold, TextAlignmentOptions.Left);
            ((RectTransform)t.transform).pivot = new Vector2(0f, 0.5f);
            coins = WalletChip(parent, "CoinsChip", UiSkin.IconCoin, Amber, new Vector2(1f, 1f),
                new Vector2(-360f, -78f), new Vector2(300f, 76f));
            gems = WalletChip(parent, "GemsChip", UiSkin.IconGem, new Color(0.55f, 0.85f, 1f),
                new Vector2(1f, 1f), new Vector2(-40f, -78f), new Vector2(290f, 76f));
            return back;
        }

        private static Button ScreenHeaderTitleOnly(Transform parent, string title, out TMP_Text titleText)
        {
            HeaderBackground(parent);
            Button back = BackButton(parent);
            titleText = Label(parent, "Title", title, 50f, new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(700f, 70f), Gold, TextAlignmentOptions.Center);
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
            go.GetComponent<Image>().color = HeaderBar;
            go.GetComponent<Image>().raycastTarget = false;
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

        // ---------------------------------------------------------------- scroll containers

        // Offsets-padrão do viewport: miolo entre o header (150) e a tab bar (~210).
        private static readonly Vector2 DefaultScrollOffsetMin = new Vector2(20f, 220f);
        private static readonly Vector2 DefaultScrollOffsetMax = new Vector2(-20f, -170f);

        private static RectTransform ScrollList(Transform parent, string name, Vector2 bottomLeftPad,
                                                Vector2 topRightPad, float spacing,
                                                Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            RectTransform content = ScrollArea(parent, name, offsetMin, offsetMax);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _ = bottomLeftPad; _ = topRightPad;
            return content;
        }

        private static RectTransform ScrollGrid(Transform parent, string name, Vector2 bottomLeftPad,
                                                Vector2 topRightPad, Vector2 cellSize, int columns, float spacing)
        {
            RectTransform content = ScrollArea(parent, name);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize; grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.padding = new RectOffset(20, 20, 20, 20);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _ = bottomLeftPad; _ = topRightPad;
            return content;
        }

        /// <summary>
        /// Viewport rolável (ScrollRect + RectMask2D). Por padrão ocupa o miolo entre header e
        /// tab bar; offsetMin/offsetMax explícitos permitem confinar a região (o Diário usa para
        /// pôr a lista de missões ABAIXO do calendário/RECLAMAR, sem sobrepor).
        /// </summary>
        private static RectTransform ScrollArea(Transform parent, string name,
                                                Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            // RectMask2D (recorte por shader, sem stencil) em vez de Mask: o Mask via stencil
            // não renderiza quando a câmera desenha num RenderTexture offscreen (pipeline do
            // -menuShowcase / URP RenderRequest) — o conteúdo era construído mas ficava invisível.
            // O RectMask2D recorta pelo retângulo e funciona offscreen, então grade/lista aparecem.
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
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = vpRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return contentRect;
        }

        private static RectTransform HorizontalStrip(Transform parent, string name, Vector2 anchoredPos,
                                                     Vector2 size, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size; rect.anchoredPosition = anchoredPos;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            return rect;
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

        private static TMP_Text WalletChip(Transform parent, string name, Sprite icon, Color tint,
                                           Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var chipGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            var chipRect = (RectTransform)chipGo.transform;
            chipRect.SetParent(parent, false);
            chipRect.anchorMin = anchor; chipRect.anchorMax = anchor; chipRect.pivot = new Vector2(1f, 1f);
            chipRect.anchoredPosition = pos; chipRect.sizeDelta = size;
            Image chip = chipGo.GetComponent<Image>();
            Sprite skin = UiSkin.BadgeFlat;
            if (skin != null) { chip.sprite = skin; chip.type = Image.Type.Sliced; chip.color = new Color(0.06f, 0.08f, 0.14f, 0.72f); }
            else chip.color = new Color(0f, 0f, 0f, 0.45f);
            chip.raycastTarget = false;

            float iconSize = size.y * 0.64f;
            if (icon != null)
            {
                var ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var icRect = (RectTransform)ic.transform;
                icRect.SetParent(chipRect, false);
                icRect.anchorMin = new Vector2(0f, 0.5f); icRect.anchorMax = new Vector2(0f, 0.5f);
                icRect.pivot = new Vector2(0.5f, 0.5f);
                icRect.anchoredPosition = new Vector2(iconSize * 0.5f + 14f, 0f);
                icRect.sizeDelta = new Vector2(iconSize, iconSize);
                var icImg = ic.GetComponent<Image>();
                icImg.sprite = icon; icImg.color = tint; icImg.preserveAspect = true; icImg.raycastTarget = false;
            }

            TMP_Text value = Label(chipRect, "Value", "0", Mathf.Round(size.y * 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size, Color.white, TextAlignmentOptions.Left);
            var vRect = (RectTransform)value.transform;
            Stretch(vRect);
            vRect.offsetMin = new Vector2(icon != null ? iconSize + 28f : 18f, 4f);
            vRect.offsetMax = new Vector2(-14f, -4f);
            return value;
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
