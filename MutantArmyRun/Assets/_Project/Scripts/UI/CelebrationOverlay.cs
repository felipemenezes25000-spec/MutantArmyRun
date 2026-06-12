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
    /// OVL — celebração reutilizável (brief F-Celebration). Dois gatilhos automáticos + um manual:
    ///  (a) DESBLOQUEIO DE TROPA — assina UnitManager.OnTroopChanged e detecta a transição
    ///      bloqueada→desbloqueada (carta grande "NOVA TROPA: &lt;nome&gt;");
    ///  (b) LEVEL-UP — assina EconomySystem.OnPlayerLevelUp (exposto agora, disparado em
    ///      CheckPlayerLevelUp) e mostra "NÍVEL N!";
    ///  (c) manual — ShowSeasonReward, chamado pela SeasonPassScreen ao coletar.
    ///
    /// Confete (partículas de UI baratas, geradas em código) + Tween.ScalePop no cartão, AUTO-DISMISS
    /// curto (não bloqueia o AutoPilot — fade do UIOverlay em unscaled time, fecha sozinho). Funciona
    /// offscreen (sem Mask stencil). Enfileira gatilhos: dois desbloqueios seguidos não se atropelam.
    /// </summary>
    public class CelebrationOverlay : UIOverlay
    {
        /// <summary>Instância ativa — a SeasonPassScreen dispara ShowSeasonReward sem ref serializada.</summary>
        public static CelebrationOverlay Instance { get; private set; }

        [Header("Cartão")]
        [SerializeField] private RectTransform _card;           // recebe o ScalePop
        [SerializeField] private TMP_Text _kickerText;          // "NOVA TROPA" / "SUBIU DE NÍVEL"
        [SerializeField] private TMP_Text _titleText;           // nome da tropa / "NÍVEL 5!"
        [SerializeField] private TMP_Text _subtitleText;        // raridade / recompensa
        [SerializeField] private RectTransform _confettiRoot;   // pai das partículas de confete

        [Header("Tempo")]
        [SerializeField] private float _holdSeconds = 1.6f;     // curto: não trava o AutoPilot

        private readonly Dictionary<string, bool> _unlockState = new Dictionary<string, bool>();
        private readonly Queue<Pending> _queue = new Queue<Pending>();
        private readonly List<GameObject> _confetti = new List<GameObject>();
        private bool _playing;
        private bool _subscribed;
        private Coroutine _holdRoutine;

        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);

        // Cores de confete vivas (geração em código — sem asset de partícula).
        private static readonly Color[] ConfettiColors =
        {
            new Color(1.00f, 0.80f, 0.28f), new Color(0.28f, 0.58f, 0.98f),
            new Color(0.66f, 0.36f, 0.92f), new Color(0.25f, 0.80f, 0.35f),
            new Color(1.00f, 0.45f, 0.55f)
        };

        private struct Pending
        {
            public string kicker;
            public string title;
            public string subtitle;
            public Color accent;
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
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
            if (_subscribed) return;
            if (UnitManager.Instance != null)
            {
                SeedUnlockState();
                UnitManager.Instance.OnTroopChanged += HandleTroopChanged;
            }
            if (EconomySystem.Instance != null)
                EconomySystem.Instance.OnPlayerLevelUp += HandlePlayerLevelUp;
            // Só marca inscrito se ao menos um manager estava pronto; senão tenta de novo no próximo OnEnable.
            _subscribed = UnitManager.Instance != null || EconomySystem.Instance != null;
        }

        private void Unsubscribe()
        {
            if (UnitManager.Instance != null) UnitManager.Instance.OnTroopChanged -= HandleTroopChanged;
            if (EconomySystem.Instance != null) EconomySystem.Instance.OnPlayerLevelUp -= HandlePlayerLevelUp;
            _subscribed = false;
        }

        /// <summary>Foto inicial de quem já está desbloqueado — só transições futuras celebram.</summary>
        private void SeedUnlockState()
        {
            _unlockState.Clear();
            UnitManager um = UnitManager.Instance;
            if (um == null) return;
            for (int i = 0; i < um.CatalogSize; i++)
            {
                UnitConfigSO cfg = um.GetConfig(i);
                if (cfg == null || string.IsNullOrEmpty(cfg.unitId)) continue;
                _unlockState[cfg.unitId] = um.IsUnlocked(cfg.unitId);
            }
        }

        private void HandleTroopChanged(string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || UnitManager.Instance == null) return;
            bool nowUnlocked = UnitManager.Instance.IsUnlocked(unitId);
            bool wasUnlocked = _unlockState.TryGetValue(unitId, out bool prev) && prev;
            _unlockState[unitId] = nowUnlocked;

            if (nowUnlocked && !wasUnlocked)
            {
                UnitConfigSO cfg = UnitManager.Instance.GetConfig(unitId);
                Rarity rarity = cfg != null ? cfg.rarity : Rarity.Common;
                Enqueue(new Pending
                {
                    kicker = "NOVA TROPA",
                    title = cfg != null ? MetaText.UnitName(cfg) : MetaText.Humanize(unitId),
                    subtitle = MetaText.RarityName(rarity),
                    accent = MetaText.RarityFrame(rarity)
                });
            }
        }

        private void HandlePlayerLevelUp(int newLevel)
        {
            Enqueue(new Pending
            {
                kicker = "SUBIU DE NÍVEL",
                title = "NÍVEL " + newLevel + "!",
                subtitle = "Novas recompensas liberadas",
                accent = Gold
            });
        }

        /// <summary>Celebração manual do resgate do Passe de Temporada (chamada pela SeasonPassScreen).</summary>
        public void ShowSeasonReward(long coins, int gems, int shards, int skins)
        {
            var sb = new System.Text.StringBuilder(48);
            if (coins > 0) sb.Append("+").Append(coins).Append(" moedas  ");
            if (gems > 0) sb.Append("+").Append(gems).Append(" gemas  ");
            if (shards > 0) sb.Append("+").Append(shards).Append(" frag  ");
            if (skins > 0) sb.Append("SKIN  ");
            Enqueue(new Pending
            {
                kicker = "PASSE DE TEMPORADA",
                title = "RECOMPENSAS!",
                subtitle = sb.Length > 0 ? sb.ToString().TrimEnd() : "Coletado!",
                accent = Gold
            });
        }

        private void Enqueue(Pending p)
        {
            _queue.Enqueue(p);
            if (!_playing) PlayNext();
        }

        private void PlayNext()
        {
            if (_queue.Count == 0) { _playing = false; return; }
            _playing = true;
            Pending p = _queue.Dequeue();

            if (_kickerText != null) { _kickerText.text = p.kicker; _kickerText.color = p.accent; }
            if (_titleText != null) _titleText.text = p.title;
            if (_subtitleText != null) _subtitleText.text = p.subtitle;

            Show(() =>
            {
                if (_card != null) Tween.ScalePop(_card, 0.45f);
                SpawnConfetti(p.accent);
                if (_holdRoutine != null) StopCoroutine(_holdRoutine);
                _holdRoutine = StartCoroutine(HoldRoutine());
            });
        }

        private IEnumerator HoldRoutine()
        {
            // unscaled: a celebração fecha no tempo certo mesmo com o jogo congelado; não bloqueia o AutoPilot.
            yield return new WaitForSecondsRealtime(_holdSeconds);
            _holdRoutine = null;
            Hide(() =>
            {
                ClearConfetti();
                PlayNext();   // encadeia o próximo da fila (desbloqueios em sequência)
            });
        }

        // ---------------------------------------------------------------- confete (gerado em código)

        private void SpawnConfetti(Color accent)
        {
            RectTransform root = _confettiRoot != null ? _confettiRoot : (RectTransform)transform;
            ClearConfetti();
            const int count = 24;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(root, false);
                rect.sizeDelta = new Vector2(Random.Range(14f, 26f), Random.Range(14f, 26f));
                rect.anchoredPosition = new Vector2(Random.Range(-360f, 360f), Random.Range(120f, 460f));
                var img = go.GetComponent<Image>();
                img.color = i % 3 == 0 ? accent : ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                img.raycastTarget = false;
                _confetti.Add(go);

                // Queda + leve deriva lateral, em unscaled time. Some no fim do hold (ClearConfetti).
                Vector3 to = rect.position + new Vector3(Random.Range(-1.4f, 1.4f), -Random.Range(3.5f, 6.5f), 0f);
                Tween.MoveTo(rect, to, _holdSeconds * 0.9f, Tween.Ease.OutCubic);
                Tween.ScalePop(rect, 0.3f);
            }
        }

        private void ClearConfetti()
        {
            for (int i = 0; i < _confetti.Count; i++)
                if (_confetti[i] != null) Destroy(_confetti[i]);
            _confetti.Clear();
        }
    }
}
