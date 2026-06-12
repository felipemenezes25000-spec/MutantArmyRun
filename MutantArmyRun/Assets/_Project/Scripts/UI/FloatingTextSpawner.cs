using System.Collections;
using System.Collections.Generic;
using MutantArmy.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MutantArmy.UI
{
    /// <summary>
    /// Moedas voadoras + texto flutuante do estouro de Supply (CANON §3.2 — fanfarra,
    /// NUNCA punição): no OnSupplyOverflow, sprites dourados sobem em ARCO da região do
    /// exército (base da tela) até o contador de moedas do HUD, com stagger, e um "+N"
    /// dourado pula no centro. Tudo por evento do bus (zero polling, doc 12 §3.2),
    /// tempo UNSCALED (vivo durante slow motion) e 100% pooled — nunca Destroy (§6.4).
    /// </summary>
    public class FloatingTextSpawner : MonoBehaviour
    {
        [Header("Wiring (MAR Tools/Build Juice)")]
        [SerializeField] private RectTransform _layer;        // camada full-stretch no HudCanvas
        [SerializeField] private RectTransform _coinTarget;   // contador de moedas do HUD
        [SerializeField] private Sprite _coinSprite;          // disco branco (Kenney) tintado de dourado

        [Header("Tuning")]
        [SerializeField] private int _maxCoins = 12;
        [SerializeField] private float _coinSize = 44f;
        [SerializeField] private float _flightSeconds = 0.7f;
        [SerializeField] private float _spawnStagger = 0.045f;
        [SerializeField] private Color _coinColor = new Color(1f, 0.84f, 0.25f);

        private readonly List<RectTransform> _coinPool = new List<RectTransform>(16);
        private TMP_Text _floatingLabel;

        private void OnEnable()
        {
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
        }

        private void OnDisable()
        {
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;   // bus estático: sempre limpar (§3.2)
            StopAllCoroutines();
            for (int i = 0; i < _coinPool.Count; i++)
                if (_coinPool[i] != null) _coinPool[i].gameObject.SetActive(false);
            if (_floatingLabel != null) _floatingLabel.gameObject.SetActive(false);
        }

        private void HandleSupplyOverflow(SupplyOverflow overflow)
        {
            if (!isActiveAndEnabled || _layer == null) return;
            int coins = Mathf.Clamp(overflow.unitsConverted, 4, _maxCoins);
            StartCoroutine(BurstRoutine(coins, overflow.coinsGranted));
        }

        private IEnumerator BurstRoutine(int coinCount, int coinsGranted)
        {
            ShowFloatingText("+" + coinsGranted);
            for (int i = 0; i < coinCount; i++)
            {
                StartCoroutine(FlyCoinRoutine(GetCoin()));
                yield return new WaitForSecondsRealtime(_spawnStagger);
            }
        }

        // ------------------------------------------------------------------ moeda

        private RectTransform GetCoin()
        {
            for (int i = 0; i < _coinPool.Count; i++)
            {
                if (_coinPool[i] != null && !_coinPool[i].gameObject.activeSelf)
                {
                    _coinPool[i].gameObject.SetActive(true);
                    return _coinPool[i];
                }
            }

            var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_layer, false);
            rect.sizeDelta = new Vector2(_coinSize, _coinSize);
            var image = go.GetComponent<Image>();
            image.sprite = _coinSprite;           // null → quad colorido: ainda lê como moeda
            image.color = _coinColor;
            image.raycastTarget = false;
            _coinPool.Add(rect);
            return rect;
        }

        private IEnumerator FlyCoinRoutine(RectTransform coin)
        {
            // origem: faixa inferior central (onde o exército vive na tela 9:16);
            // destino: contador de moedas do HUD (fallback: canto superior esquerdo)
            Rect layerRect = _layer.rect;
            Vector2 start = new Vector2(
                Random.Range(-layerRect.width * 0.18f, layerRect.width * 0.18f),
                Random.Range(-layerRect.height * 0.38f, -layerRect.height * 0.25f));

            Vector2 end = _coinTarget != null
                ? (Vector2)_layer.InverseTransformPoint(_coinTarget.position)
                : new Vector2(-layerRect.width * 0.36f, layerRect.height * 0.44f);

            // arco: bézier quadrática com controle acima e ao lado do ponto médio
            Vector2 mid = (start + end) * 0.5f;
            Vector2 control = mid + new Vector2(Random.Range(-120f, 120f), Random.Range(140f, 260f));

            coin.anchorMin = coin.anchorMax = new Vector2(0.5f, 0.5f);
            coin.anchoredPosition = start;
            coin.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < _flightSeconds)
            {
                if (coin == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / _flightSeconds);

                float pop = Tween.Evaluate(Tween.Ease.OutBack, Mathf.Clamp01(k * 4f));   // nasce em pop
                float shrink = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((k - 0.7f) / 0.3f));  // encolhe ao chegar
                coin.localScale = Vector3.one * (pop * shrink);

                float e = Tween.Evaluate(Tween.Ease.OutCubic, k);
                Vector2 a = Vector2.LerpUnclamped(start, control, e);
                Vector2 b = Vector2.LerpUnclamped(control, end, e);
                coin.anchoredPosition = Vector2.LerpUnclamped(a, b, e);
                yield return null;
            }

            coin.gameObject.SetActive(false);
            if (_coinTarget != null) Tween.PunchScale(_coinTarget, 0.2f, 0.18f);   // o contador "recebe"
        }

        // ------------------------------------------------------------------ texto flutuante

        /// <summary>Texto dourado de uma linha que pula no centro e some — reutilizável por outros glues.</summary>
        public void ShowFloatingText(string text)
        {
            if (_layer == null) return;
            if (_floatingLabel == null)
            {
                var go = new GameObject("FloatingText", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_layer, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(700f, 120f);
                _floatingLabel = go.AddComponent<TextMeshProUGUI>();
                _floatingLabel.alignment = TextAlignmentOptions.Center;
                _floatingLabel.fontSize = 64f;
                _floatingLabel.color = _coinColor;
                _floatingLabel.raycastTarget = false;
            }
            _floatingLabel.text = text;
            _floatingLabel.gameObject.SetActive(true);
            StartCoroutine(FloatingTextRoutine((RectTransform)_floatingLabel.transform));
        }

        private IEnumerator FloatingTextRoutine(RectTransform rect)
        {
            const float seconds = 0.9f;
            Vector2 from = new Vector2(0f, -40f);
            Vector2 to = new Vector2(0f, 140f);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (rect == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / seconds);
                rect.anchoredPosition = Vector2.Lerp(from, to, Tween.Evaluate(Tween.Ease.OutCubic, k));
                rect.localScale = Vector3.one * Tween.Evaluate(Tween.Ease.OutBack, Mathf.Clamp01(k * 3f));
                _floatingLabel.alpha = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                yield return null;
            }
            if (rect != null) rect.gameObject.SetActive(false);
        }
    }
}
