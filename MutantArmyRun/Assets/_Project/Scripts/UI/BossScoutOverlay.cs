using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// OVL-01 — Boss Scout card (CANON §3.1, doc 09 §5.1): cartão de ~2 s mostrando o
    /// boss da fase, seu elemento e sua fraqueza ("FRACO CONTRA FOGO"). Auto-dismiss;
    /// qualquer toque pula. Fraqueza com NOME do elemento, nunca só cor (doc 09 P7).
    /// </summary>
    public class BossScoutOverlay : UIOverlay
    {
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private TMP_Text _elementText;
        [SerializeField] private TMP_Text _weaknessText;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private Image _portrait;        // arte estática (scoutCardArt) OU emblema da silhueta (fallback B)
        [SerializeField] private RawImage _portraitRaw;  // preview 3D do boss (Opção A): RT do BossPortraitRenderer
        [SerializeField] private TMP_Text _portraitGlyph;// letra/glifo da silhueta no fallback (sobre o emblema tintado)
        [SerializeField] private Image _elementIcon;     // orbe tintado pela cor do elemento do boss
        [SerializeField] private Image _weaknessIcon;    // orbe tintado pela cor da FRAQUEZA (reforça o texto)
        [SerializeField] private Image _timerFill;       // anel radial: esvazia junto com o auto-dismiss
        [SerializeField] private Button _skipButton;     // botão fullscreen: qualquer toque pula
        [SerializeField] private RectTransform _card;    // cartão central: pop de entrada (escala) — opcional

        // P2-UILAYOUT: posição/tamanho-alvo do retrato no MIOLO vazio do card (entre o nome,
        // que pode ter 2 linhas, e a faixa de fraqueza). Aplicado em runtime sobre o _portrait
        // wirado pela factory — corrige o overlap em que a caixa do retrato cobria a 2ª linha
        // do nome ("GOLEM DE / PEDRA"). Idempotente; só recoloca, nunca cria conteúdo.
        private static readonly Vector2 PortraitAnchoredPos = new Vector2(0f, 130f);
        private static readonly Vector2 PortraitSize = new Vector2(300f, 300f);

        private Coroutine _autoHide;
        private Action _onDone;
        private bool _finished;

        private BossPortraitRenderer _portraitRenderer;  // rig de preview 3D (lazy; só existe se há prefab)
        private bool _usingLivePortrait;                 // true quando o preview 3D está ativo nesta exibição

        protected override void Awake()
        {
            base.Awake();
            if (_skipButton != null) _skipButton.onClick.AddListener(Finish);
        }

        /// <summary>Mostra o cartão por <paramref name="seconds"/>; onDone após fechar (1× garantido).</summary>
        public void Play(BossConfigSO boss, float seconds, Action onDone)
        {
            _onDone = onDone;
            _finished = false;
            Bind(boss);
            // Pop de entrada do cartão JUNTO com o fade (overlap = mais punch). Se a factory não
            // wirou _card, anima o próprio overlay. Tudo em unscaled time (sobrevive a slow-motion).
            Transform popTarget = _card != null ? (Transform)_card : transform;
            Core.Tween.ScalePop(popTarget, 0.32f);
            Show(() =>
            {
                if (_autoHide != null) StopCoroutine(_autoHide);
                _autoHide = StartCoroutine(AutoHideRoutine(seconds));
            });
        }

        /// <summary>
        /// Pulo PROGRAMÁTICO do cartão (AutoPilot/PlayMode em batchmode): mesmo caminho do
        /// toque — fecha 1× e dispara o onDone (que leva BossScout→Running). Necessário
        /// porque o auto-dismiss roda em tempo UNSCALED (Time.timeScale não acelera o cartão).
        /// </summary>
        public void Skip()
        {
            Finish();
        }

        /// <summary>Preenche o cartão a partir do dado — a UI nunca inventa conteúdo.</summary>
        public void Bind(BossConfigSO boss)
        {
            if (boss == null) return;
            if (_bossNameText != null) _bossNameText.text = DisplayName(boss);
            // Boss sem elemento (todos do MVP): a linha "ELEMENTO: SEM ELEMENTO" não informa nada —
            // some com ela e deixa o cartão falar só da FRAQUEZA, que é a ação que importa (doc 09 §5.1).
            if (_elementText != null)
            {
                bool hasElement = boss.element != ElementType.None;
                _elementText.gameObject.SetActive(hasElement);
                if (hasElement) _elementText.text = "ELEMENTO: " + ElementNamePt(boss.element);
            }
            if (_weaknessText != null) _weaknessText.text = WeaknessLine(boss);
            if (_hintText != null) _hintText.text = HintLine(boss);
            BindPortrait(boss);
            if (_elementIcon != null)
            {
                // orbe do elemento acompanha a linha de elemento: some junto quando o boss é neutro
                bool hasElement = boss.element != ElementType.None;
                _elementIcon.color = ElementColorPt(boss.element);
                _elementIcon.enabled = hasElement && _elementIcon.sprite != null;
            }
            if (_weaknessIcon != null)
            {
                bool hasWeakness = boss.weaknesses != null && boss.weaknesses.Length > 0;
                if (hasWeakness) _weaknessIcon.color = ElementColorPt(boss.weaknesses[0]);
                _weaknessIcon.enabled = hasWeakness && _weaknessIcon.sprite != null;
            }
            if (_timerFill != null) _timerFill.fillAmount = 1f;
        }

        // ------------------------------------------------------------------ retrato (Opção A 3D / Opção B silhueta)

        /// <summary>
        /// Preenche o ESPAÇO VAZIO do card com o boss. Ordem de preferência:
        /// (A) preview 3D do prefab num RawImage (boss girando = "vivo"); senão
        /// (B) silhueta — emblema tingido pela cor do elemento + glifo (1ª letra do nome);
        /// (C) arte estática scoutCardArt no _portrait, se existir. Tudo null-safe e idempotente.
        /// </summary>
        private void BindPortrait(BossConfigSO boss)
        {
            EnsureFallbackWidgets();
            LayoutPortrait();      // P2-UILAYOUT: garante o retrato no miolo, ABAIXO do nome (sem overlap)
            _usingLivePortrait = false;

            // (A) Preview 3D — só se há prefab e o RawImage existe (wired ou construído em código).
            if (_portraitRaw != null && boss.prefab != null)
            {
                if (_portraitRenderer == null)
                    _portraitRenderer = gameObject.AddComponent<BossPortraitRenderer>();

                Color tint = boss.element != ElementType.None
                    ? ElementColorPt(boss.element)
                    : ElementColorPt(boss.weaknesses != null && boss.weaknesses.Length > 0 ? boss.weaknesses[0] : ElementType.None);

                if (_portraitRenderer.Show(boss, tint))
                {
                    _portraitRaw.texture = _portraitRenderer.Texture;
                    _portraitRaw.color = Color.white;
                    _portraitRaw.enabled = true;
                    _usingLivePortrait = true;
                }
            }

            // (B/C) Sem preview 3D: para o rig (caso um boss anterior tenha usado preview) e
            // cai na silhueta tintada (preferida a vazio) OU na arte estática.
            if (!_usingLivePortrait)
            {
                if (_portraitRenderer != null) _portraitRenderer.Stop();
                if (_portraitRaw != null) { _portraitRaw.enabled = false; _portraitRaw.texture = null; }
                BindStaticPortrait(boss);
            }
            else if (_portrait != null)
            {
                _portrait.enabled = false;     // preview 3D cobre o vazio: esconde o emblema/arte
                if (_portraitGlyph != null) _portraitGlyph.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Fallback B/C: arte estática (scoutCardArt) se houver; senão emblema-silhueta — círculo
        /// tingido pela cor do elemento (ou da fraqueza, se neutro) + a 1ª letra do nome. Melhor
        /// que um buraco vazio (briefing P-SCOUT, doc 09 §5.1).
        /// </summary>
        private void BindStaticPortrait(BossConfigSO boss)
        {
            if (_portrait == null) return;

            bool hasArt = boss.scoutCardArt != null;
            ElementType emblemElement = boss.element != ElementType.None
                ? boss.element
                : (boss.weaknesses != null && boss.weaknesses.Length > 0 ? boss.weaknesses[0] : ElementType.None);

            if (hasArt)
            {
                _portrait.sprite = boss.scoutCardArt;
                _portrait.color = Color.white;
                if (_portraitGlyph != null) _portraitGlyph.gameObject.SetActive(false);
            }
            else
            {
                // Emblema: mantém o sprite do orbe (GlowSoft, se wired) ou só a cor; sempre visível.
                Color tint = ElementColorPt(emblemElement);
                _portrait.color = new Color(tint.r, tint.g, tint.b, 0.85f);
                if (_portraitGlyph != null)
                {
                    _portraitGlyph.gameObject.SetActive(true);
                    _portraitGlyph.text = GlyphFor(boss);
                }
            }
            _portrait.enabled = true;
        }

        /// <summary>Primeira letra do nome amigável do boss para a silhueta (ex.: "Golem" → "G").</summary>
        private static string GlyphFor(BossConfigSO boss)
        {
            string name = DisplayName(boss);
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
        }

        /// <summary>
        /// Constrói EM CÓDIGO o RawImage do preview 3D e o glifo da silhueta se a factory ainda
        /// não os wirou (degradação graciosa — o recurso funciona sem regerar a cena). Parenteia
        /// na MESMA âncora do _portrait existente; posição/tamanho são definidos depois pelo
        /// LayoutPortrait (miolo do card, abaixo do nome). Idempotente: só cria uma vez.
        /// </summary>
        private void EnsureFallbackWidgets()
        {
            if (_portrait == null) return;
            RectTransform anchor = _portrait.rectTransform;

            if (_portraitRaw == null)
            {
                var go = new GameObject("PortraitLive", typeof(RectTransform), typeof(RawImage));
                var rt = (RectTransform)go.transform;
                rt.SetParent(anchor.parent, false);
                rt.anchorMin = anchor.anchorMin;
                rt.anchorMax = anchor.anchorMax;
                rt.pivot = anchor.pivot;
                rt.SetSiblingIndex(anchor.GetSiblingIndex() + 1);   // por cima do emblema
                _portraitRaw = go.GetComponent<RawImage>();
                _portraitRaw.raycastTarget = false;
                _portraitRaw.enabled = false;
            }

            if (_portraitGlyph == null)
            {
                var go = new GameObject("PortraitGlyph", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(anchor, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                _portraitGlyph = go.AddComponent<TextMeshProUGUI>();
                _portraitGlyph.alignment = TextAlignmentOptions.Center;
                _portraitGlyph.fontSize = 220f;
                _portraitGlyph.fontStyle = FontStyles.Bold;
                _portraitGlyph.color = new Color(1f, 1f, 1f, 0.92f);
                _portraitGlyph.raycastTarget = false;
                _portraitGlyph.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// P2-UILAYOUT — recoloca o retrato (e seus widgets) no MIOLO vazio do card, abaixo do
        /// nome do boss. CORRIGE o overlap visto no screenshot: a factory ancorava o _portrait
        /// em y≈170 com 360×360, cuja borda superior (≈350) cobria a 2ª linha de nomes longos
        /// ("GOLEM DE / PEDRA"). Reduz para 300×300 centrado em y≈130 (topo ≈280, base ≈-20) —
        /// sobra para 2 linhas de nome acima (linha 2 termina ≈318) e não toca a linha do elemento
        /// (y≈-80) nem a faixa de fraqueza. Roda em runtime sobre o que a factory wirou (não exige
        /// regerar a cena) e é idempotente. O glifo é filho do _portrait e herda esse rect — só
        /// precisa zerar o offset local. PENDÊNCIA p/ a factory (W4-B): ancorar o "Portrait"
        /// direto em (0,130) tamanho 300×300 deixa este ajuste redundante.
        /// </summary>
        private void LayoutPortrait()
        {
            if (_portrait != null)
            {
                RectTransform pr = _portrait.rectTransform;
                pr.anchoredPosition = PortraitAnchoredPos;
                pr.sizeDelta = PortraitSize;
            }

            if (_portraitRaw != null)
            {
                RectTransform lr = _portraitRaw.rectTransform;
                lr.anchoredPosition = PortraitAnchoredPos;
                lr.sizeDelta = PortraitSize;
            }

            // O glifo é filho do _portrait (centralizado): basta casar o tamanho do pai e zerar
            // o offset para acompanhar a nova caixa.
            if (_portraitGlyph != null)
            {
                RectTransform gr = _portraitGlyph.rectTransform;
                gr.anchoredPosition = Vector2.zero;
                gr.sizeDelta = PortraitSize;
            }
        }

        public static string ElementNamePt(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "FOGO";
                case ElementType.Ice: return "GELO";
                case ElementType.Lightning: return "RAIO";
                case ElementType.Poison: return "VENENO";
                case ElementType.Light: return "LUZ";
                case ElementType.Shadow: return "SOMBRA";
                case ElementType.Metal: return "METAL";
                case ElementType.Alien: return "ALIEN";
                default: return "SEM ELEMENTO";
            }
        }

        /// <summary>
        /// Cor canônica por elemento (doc 09 P7: a COR reforça, o NOME informa — nunca
        /// só cor). Tons vivos para tintar orbes/ícones sobre o cartão escuro.
        /// </summary>
        public static Color ElementColorPt(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1.00f, 0.45f, 0.15f);
                case ElementType.Ice: return new Color(0.45f, 0.85f, 1.00f);
                case ElementType.Lightning: return new Color(1.00f, 0.92f, 0.25f);
                case ElementType.Poison: return new Color(0.55f, 0.90f, 0.25f);
                case ElementType.Light: return new Color(1.00f, 0.97f, 0.75f);
                case ElementType.Shadow: return new Color(0.58f, 0.40f, 0.88f);
                case ElementType.Metal: return new Color(0.75f, 0.78f, 0.85f);
                case ElementType.Alien: return new Color(0.40f, 1.00f, 0.65f);
                default: return new Color(0.70f, 0.70f, 0.75f);
            }
        }

        // ------------------------------------------------------------------ polish (vida na UI)

        /// <summary>
        /// Destaque pulsante da fraqueza ao terminar a entrada. NÃO toca no fluxo
        /// (Play/Skip/onDone) nem no tempo do auto-dismiss — puro juice, tudo em unscaled
        /// time (sobrevive a timeScale 0 / slow-motion; PlayMode roda a 8-10×).
        /// </summary>
        protected override void OnShown()
        {
            base.OnShown();
            // Fraqueza é a AÇÃO que importa: pulsa a FAIXA inteira (orbe + texto juntos) para
            // puxar o olho sem poluir — UIPulse é o pulso senoidal padrão dos CTAs (±4%, 1.6 s,
            // unscaled, compõe sem stomp de escala). Se o orbe não estiver dentro da faixa, pulsa
            // o próprio orbe como fallback.
            Transform weaknessRow = _weaknessIcon != null ? _weaknessIcon.transform.parent : null;
            if (weaknessRow != null && weaknessRow.GetComponent<Image>() != null)
                EnablePulse(weaknessRow.GetComponent<Image>());
            else
                EnablePulse(_weaknessIcon);
        }

        private static void EnablePulse(Component target)
        {
            if (target == null) return;
            var pulse = target.GetComponent<UIPulse>();
            if (pulse == null) pulse = target.gameObject.AddComponent<UIPulse>();
            pulse.enabled = true;
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            // Desliga o preview 3D ao fechar: câmera off + instância destruída (sem vazar,
            // sem render desperdiçado fora do cartão). O rig (RT/câmera/luz) é reusado no
            // próximo boss; só morre de vez no OnDestroy.
            if (_portraitRenderer != null) _portraitRenderer.Stop();
        }

        private void OnDestroy()
        {
            // Garantia extra: se o overlay morrer com o preview ativo, para o rig (o
            // BossPortraitRenderer faz a limpeza pesada no próprio OnDestroy).
            if (_portraitRenderer != null) _portraitRenderer.Stop();
        }

        private IEnumerator AutoHideRoutine(float seconds)
        {
            // unscaled: o cartão fecha no tempo certo mesmo se o jogo congelar timeScale.
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                // Timer circular esvaziando: comunica o auto-dismiss sem texto (doc 09 §5.1).
                if (_timerFill != null && seconds > 0f)
                    _timerFill.fillAmount = Mathf.Clamp01(1f - t / seconds);
                yield return null;
            }
            Finish();
        }

        private void Finish()
        {
            if (_finished) return;     // toque + auto-hide no mesmo frame: fecha 1× só
            _finished = true;
            if (_autoHide != null)
            {
                StopCoroutine(_autoHide);
                _autoHide = null;
            }
            // Para o preview 3D imediatamente (o Hide leva 150 ms; já desligamos a câmera).
            if (_portraitRenderer != null) _portraitRenderer.Stop();
            Action done = _onDone;
            _onDone = null;
            Hide(() =>
            {
                if (done != null) done();
            });
        }

        private static string DisplayName(BossConfigSO boss)
        {
            // 1) nome amigável PT-BR gravado pelo MvpContentFactory (caminho normal).
            if (!string.IsNullOrEmpty(boss.displayName))
                return boss.displayName.ToUpperInvariant();

            // 2) fallback p/ assets antigos: humaniza a key/id (ex.: "m1_golem_stone_name"
            // → "M1 GOLEM STONE") em vez de mostrar a chave crua de localização.
            string raw = string.IsNullOrEmpty(boss.displayNameKey) ? boss.bossId : boss.displayNameKey;
            return string.IsNullOrEmpty(raw) ? "BOSS" : Humanize(raw);
        }

        /// <summary>
        /// Converte uma key de loc ("m1_golem_stone_name") num rótulo legível ("M1 GOLEM STONE"):
        /// tira o sufixo "_name", troca "_" por espaço e maiúsculas. Só é usado como rede de
        /// segurança — o caminho normal usa BossConfigSO.displayName.
        /// </summary>
        private static string Humanize(string raw)
        {
            string trimmed = raw;
            if (trimmed.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "_name".Length);
            return trimmed.Replace('_', ' ').Trim().ToUpperInvariant();
        }

        private static string WeaknessLine(BossConfigSO boss)
        {
            if (boss.weaknesses == null || boss.weaknesses.Length == 0)
                return "SEM FRAQUEZA ELEMENTAL";

            var sb = new StringBuilder("FRACO CONTRA ");
            for (int i = 0; i < boss.weaknesses.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(ElementNamePt(boss.weaknesses[i]));
            }
            return sb.ToString();
        }

        private static string HintLine(BossConfigSO boss)
        {
            if (boss.weaknesses == null || boss.weaknesses.Length == 0)
                return "Capriche na quantidade e no Supply!";
            return "Priorize portais de " + ElementNamePt(boss.weaknesses[0]) + "!";
        }
    }
}
