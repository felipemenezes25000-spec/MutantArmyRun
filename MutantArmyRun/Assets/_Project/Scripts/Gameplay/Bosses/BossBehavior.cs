using System.Collections;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Base do sistema de bosses MODULARES (missão Nota 10, W2-A): cada boss memorável tem um
    /// behavior MonoBehaviour vivendo na VIEW (autorado no prefab OU adicionado pelo registry
    /// no spawn). O BossManager é o ÚNICO chamador dos 7 hooks da missão — o behavior nunca
    /// dirige a luta, só REAGE a ela (HP/fase/dano continuam no BossRuntime/BossManager).
    ///
    /// Hooks virtual com corpo vazio (decisão da missão): behaviors sobrescrevem só o que usam.
    ///
    /// CONTRATO DE POOLING (view é pooled — Release nunca Destroy, §1.5): o componente
    /// SOBREVIVE entre lutas. Begin()/End() são o ciclo de vida chamado pelo BossManager:
    /// End() para coroutines, limpa o tint MPB e zera o contexto — estado por luta DEVE ser
    /// re-inicializado no OnFightStart, nunca em Awake.
    /// </summary>
    public abstract class BossBehavior : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Renderer> _renderers = new List<Renderer>(8);
        private MaterialPropertyBlock _mpb;
        private Color _tintColor = Color.white;
        private float _tintStrength;
        private Coroutine _tintFlash;

        /// <summary>Contexto da luta ATUAL (default fora de luta — checar FightActive antes de usar).</summary>
        public BossContext Context { get; private set; }

        /// <summary>true entre Begin() e End(). Componentes stale no pool ficam inertes (false).</summary>
        public bool FightActive { get; private set; }

        /// <summary>
        /// true quando este componente foi ADICIONADO pelo BossBehaviorRegistry (vs autorado no
        /// prefab). A flag sobrevive no pool e permite ao BossManager distinguir um behavior
        /// stale de outro boss (pool da cápsula fallback é compartilhado) de um autorado.
        /// </summary>
        public bool AddedByRegistry { get; set; }

        /// <summary>Guarda padrão para Update() de behaviors: luta ativa E boss ainda vivo (a
        /// sequência de morte mantém o componente ativo por ~1,2 s com o runtime já zerado).</summary>
        protected bool RuntimeAlive => FightActive && Context.Runtime != null && Context.Runtime.Hp > 0f;

        // ------------------------------------------------------------------
        // Ciclo de vida chamado pelo BossManager (não são hooks da missão)
        // ------------------------------------------------------------------

        /// <summary>Chamado pelo BossManager logo após o SpawnView: arma o contexto e dispara OnFightStart.</summary>
        public void Begin(BossContext context)
        {
            Context = context;
            FightActive = true;
            CacheRenderers();
            _tintStrength = 0f;
            _tintColor = Color.white;
            OnFightStart(context);
        }

        /// <summary>
        /// Chamado pelo BossManager no ReleaseView (único ponto de release): para tudo e limpa o
        /// visual — a view volta ao pool SEM sujeira de MPB/coroutine. Idempotente.
        /// </summary>
        public void End()
        {
            if (!FightActive) return;
            FightActive = false;
            StopAllCoroutines();
            _tintFlash = null;
            _tintStrength = 0f;
            ClearTint();
            OnFightEnd();
            _renderers.Clear();
            Context = default;
        }

        // ------------------------------------------------------------------
        // Os 7 hooks da missão — virtual com corpo vazio; o BossManager chama nos pontos certos
        // ------------------------------------------------------------------

        /// <summary>Luta começou (após SpawnView). RE-INICIALIZAR aqui todo estado por luta (pooling!).</summary>
        public virtual void OnFightStart(BossContext context) { }

        /// <summary>HP cruzou um limiar de fase (0.5/0.25 canônicos — contrato §1.14). normalizedHp = Hp/MaxHp.</summary>
        public virtual void OnHealthPhaseChanged(float normalizedHp) { }

        /// <summary>Telegraph do especial começou (decal genérico já está no chão via VFXManager).</summary>
        public virtual void OnSpecialAttackWarning() { }

        /// <summary>O golpe especial CONECTOU (dano genérico já aplicado pelo BossManager.FireSpecial).</summary>
        public virtual void OnSpecialAttackExecute() { }

        /// <summary>Exército acertando a FRAQUEZA (veredito dominante do CombatSystem, rate-limited ≥0,5 s).</summary>
        public virtual void OnWeaknessHit(ElementType element) { }

        /// <summary>Exército batendo em elemento RESISTIDO/IMUNE (mesmo rate-limit do OnWeaknessHit).</summary>
        public virtual void OnResistedHit(ElementType element) { }

        /// <summary>Boss morreu — chamado ANTES de Current=null (estado final legível) e do RaiseBossDied.</summary>
        public virtual void OnDeath() { }

        /// <summary>Limpeza extra por behavior no fim da luta (tweens externos, transforms próprios).</summary>
        protected virtual void OnFightEnd() { }

        // ------------------------------------------------------------------
        // Helpers de apresentação compartilhados (MPB/escala/shake) — todos null-safe
        // ------------------------------------------------------------------

        /// <summary>
        /// Tint PERSISTENTE da view via MaterialPropertyBlock (nunca instancia material, §6.4).
        /// Reaplicado por frame no LateUpdate enquanto strength &gt; 0 — vence deterministicamente
        /// o flash vermelho de hit do JuiceController (coroutines rodam antes do LateUpdate).
        /// strength 0 desliga e devolve o material intocado.
        /// </summary>
        protected void SetTint(Color color, float strength01)
        {
            _tintColor = color;
            _tintStrength = Mathf.Clamp01(strength01);
            if (_tintStrength <= 0f) ClearTint();
        }

        /// <summary>Tint TEMPORÁRIO (reação leve): sobe a strength dada e decai a zero em seconds.</summary>
        protected void FlashTint(Color color, float strength01, float seconds)
        {
            if (!FightActive) return;
            if (_tintFlash != null) StopCoroutine(_tintFlash);
            _tintFlash = StartCoroutine(FlashTintRoutine(color, Mathf.Clamp01(strength01), Mathf.Max(0.05f, seconds)));
        }

        /// <summary>Punch de escala na raiz da view (anticipação/impacto). No-op sem view.</summary>
        protected void PulseScale(float punch, float seconds)
        {
            if (Context.View != null) Tween.PunchScale(Context.View, punch, seconds);
        }

        /// <summary>Shake por trauma acumulável do Tween (teto 6° anti-vertigem já embutido).</summary>
        protected static void Shake(float amplitude, float seconds)
        {
            Tween.ShakeCamera(amplitude, seconds);
        }

        private IEnumerator FlashTintRoutine(Color color, float strength, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;   // legível mesmo durante slow motion
                float k = 1f - Mathf.Clamp01(elapsed / seconds);
                _tintColor = color;
                _tintStrength = strength * k;
                yield return null;
            }
            _tintStrength = 0f;
            ClearTint();
            _tintFlash = null;
        }

        private void LateUpdate()
        {
            if (!FightActive || _tintStrength <= 0f) return;
            ApplyTintNow();
        }

        private void ApplyTintNow()
        {
            if (_renderers.Count == 0) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Color blended = Color.Lerp(Color.white, _tintColor, _tintStrength);
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, blended);
            _mpb.SetColor(ColorId, blended);
            _mpb.SetColor(EmissionColorId, _tintColor * (0.6f * _tintStrength));
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_mpb);
        }

        // SetPropertyBlock(null) devolve o material INTOCADO — mesmo contrato do JuiceController.
        private void ClearTint()
        {
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(null);
        }

        private void CacheRenderers()
        {
            _renderers.Clear();
            if (Context.View == null) return;
            Context.View.GetComponentsInChildren(true, _renderers);
            for (int i = _renderers.Count - 1; i >= 0; i--)
                if (_renderers[i] is ParticleSystemRenderer) _renderers.RemoveAt(i);
        }

        // Defensivo: se a view for desativada por fora do fluxo (release sem End por algum
        // caminho novo), o tint não pode ficar gravado no renderer pooled.
        private void OnDisable()
        {
            if (_tintStrength > 0f)
            {
                _tintStrength = 0f;
                ClearTint();
            }
        }
    }
}
