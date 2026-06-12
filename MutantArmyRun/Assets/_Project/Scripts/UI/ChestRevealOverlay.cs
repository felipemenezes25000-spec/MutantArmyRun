using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;

namespace MutantArmy.UI
{
    /// <summary>
    /// OVL — abertura de baú satisfatória (brief F-Chest). Assina RewardSystem.OnChestOpened (que já
    /// carrega o ChestResult creditado) e revela as recompensas UMA A UMA: cada carta entra com
    /// Tween.ScalePop e um brilho por RARIDADE (moldura colorida + glow), moedas/gemas no topo,
    /// fragmentos por tropa. Botão COLETAR fecha o overlay.
    ///
    /// Funciona OFFSCREEN: scroll com RectMask2D, sem Mask stencil (o stencil não renderiza quando a
    /// câmera desenha num RenderTexture — pipeline do -menuShowcase). Fade do UIOverlay em unscaled
    /// time, então o reveal roda mesmo com o jogo congelado e não trava o AutoPilot (auto-dismiss
    /// só por COLETAR; o reveal é rápido).
    /// </summary>
    public class ChestRevealOverlay : UIOverlay
    {
        /// <summary>Instância ativa (a ShopScreen/factory dispara o reveal via OnChestOpened, sem ref direta).</summary>
        public static ChestRevealOverlay Instance { get; private set; }

        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _currencyText;        // "+250 moedas  +5 gemas"

        [Header("Cartas reveladas (runtime)")]
        [SerializeField] private RectTransform _cardsContent;   // GridLayoutGroup/Horizontal; uma carta por tropa

        [Header("Ação")]
        [SerializeField] private Button _collectButton;
        [SerializeField] private TMP_Text _collectLabel;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Coroutine _revealRoutine;
        private bool _subscribed;

        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            if (_collectButton != null) _collectButton.onClick.AddListener(OnCollect);
            Subscribe();
        }

        private void OnEnable() { Subscribe(); }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Subscribe()
        {
            if (_subscribed || RewardSystem.Instance == null) return;
            RewardSystem.Instance.OnChestOpened += HandleChestOpened;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || RewardSystem.Instance == null) { _subscribed = false; return; }
            RewardSystem.Instance.OnChestOpened -= HandleChestOpened;
            _subscribed = false;
        }

        /// <summary>
        /// Disparado pelo funil de baús (RewardSystem.OpenChest). Mostra o overlay e revela o conteúdo.
        /// Funciona para baú grátis/gema/embutido — toda abertura passa por OnChestOpened.
        /// </summary>
        private void HandleChestOpened(ChestResult result)
        {
            Play(result);
        }

        /// <summary>Abre o overlay e revela o ChestResult dado, carta por carta.</summary>
        public void Play(ChestResult result)
        {
            ClearCards();
            if (_titleText != null) _titleText.text = "BAÚ ABERTO!";
            if (_currencyText != null) _currencyText.text = CurrencyLine(result);
            if (_collectButton != null) _collectButton.interactable = true;
            if (_collectLabel != null) _collectLabel.text = "COLETAR";

            Show();   // fade de entrada do UIOverlay

            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = StartCoroutine(RevealRoutine(result));
        }

        private IEnumerator RevealRoutine(ChestResult result)
        {
            // Pequena espera para o fade de entrada começar, depois revela cada carta com um respiro.
            yield return new WaitForSecondsRealtime(0.12f);

            int n = result.unitIds != null ? result.unitIds.Length : 0;
            for (int i = 0; i < n; i++)
            {
                int shards = result.shards != null && i < result.shards.Length ? result.shards[i] : 0;
                GameObject card = BuildCard(result.unitIds[i], shards);
                if (card != null)
                {
                    _spawned.Add(card);
                    // Brilho/escala: a carta nasce de escala 0 e estoura (OutBack) — reveal satisfatório.
                    card.transform.localScale = Vector3.zero;
                    Tween.ScalePop(card.transform, 0.40f);
                    yield return new WaitForSecondsRealtime(0.22f);
                }
            }

            // Baú só de moeda/gema (sem cartas): garante que a faixa de moeda já comunica o ganho.
            _revealRoutine = null;
        }

        private GameObject BuildCard(string unitId, int shards)
        {
            if (_cardsContent == null) return null;

            UnitConfigSO cfg = UnitManager.Instance != null ? UnitManager.Instance.GetConfig(unitId) : null;
            Rarity rarity = cfg != null ? cfg.rarity : Rarity.Common;
            Color frame = MetaText.RarityFrame(rarity);

            var go = new GameObject("Card_" + unitId, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_cardsContent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 260f; le.preferredWidth = 260f; le.minHeight = 340f; le.preferredHeight = 340f;
            var bg = go.GetComponent<Image>();
            bg.color = MetaText.RarityCardBg(rarity); bg.raycastTarget = false;

            // Moldura de raridade (borda colorida) — brilho que comunica o valor sem depender só da cor.
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            var frameRect = (RectTransform)frameGo.transform;
            frameRect.SetParent(rect, false);
            frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = new Vector2(-6f, -6f); frameRect.offsetMax = new Vector2(6f, 6f);
            var frameImg = frameGo.GetComponent<Image>();
            frameImg.color = new Color(frame.r, frame.g, frame.b, 0.85f); frameImg.raycastTarget = false;
            frameGo.transform.SetAsFirstSibling();   // atrás da carta: vira borda

            Label(rect, "Rarity", MetaText.RarityName(rarity), 24f, new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(240f, 34f), frame, TextAlignmentOptions.Center);
            Label(rect, "Name", cfg != null ? MetaText.UnitName(cfg) : MetaText.Humanize(unitId), 28f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(240f, 90f), Color.white,
                TextAlignmentOptions.Center);
            Label(rect, "Shards", "+" + shards + " fragmentos", 26f, new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(240f, 40f), Gold, TextAlignmentOptions.Center);
            return go;
        }

        private static string CurrencyLine(ChestResult result)
        {
            var sb = new System.Text.StringBuilder(48);
            if (result.coins > 0) sb.Append("+").Append(result.coins).Append(" moedas");
            if (result.gems > 0)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("+").Append(result.gems).Append(" gemas");
            }
            if (sb.Length == 0) sb.Append("Fragmentos para as Tropas!");
            return sb.ToString();
        }

        private void OnCollect()
        {
            if (_revealRoutine != null) { StopCoroutine(_revealRoutine); _revealRoutine = null; }
            Hide(ClearCards);
        }

        private void ClearCards()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
        }

        private TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                               Vector2 pos, Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = content; label.fontSize = size; label.alignment = align; label.color = color;
            label.raycastTarget = false;
            return label;
        }
    }
}
