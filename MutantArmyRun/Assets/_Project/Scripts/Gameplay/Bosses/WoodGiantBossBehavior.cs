using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Gigante de Madeira (m1_final_wood_giant, fraco contra FOGO — missão Nota 10, W2-A).
    /// Identidade: madeira PEGA FOGO — cada weakness hit acumula brasa (tinte laranja
    /// progressivo via MPB + burst); fase 1 = batida no chão (especial mais frequente);
    /// fase 2 = desespero em chamas (tinte vermelho + contato +25%); morte = tomba como
    /// árvore em câmera lenta. Multiplicadores SEMPRE no BossRuntime — nunca mutar o SO.
    /// A chuva de moedas da morte é do JuiceController (assina OnBossDied) — aqui só a queda.
    /// </summary>
    public sealed class WoodGiantBossBehavior : BossBehavior
    {
        private const int StacksForFullBlaze = 8;            // ~8 vereditos de fraqueza = brasa total
        private static readonly Color EmberTint = new Color(1.00f, 0.55f, 0.15f);   // laranja-brasa
        private static readonly Color DesperationTint = new Color(1.00f, 0.25f, 0.12f);   // vermelho-fogo

        private int _fireStacks;
        private bool _slammed;       // fase 1 disparada (one-shot por luta)
        private bool _desperate;     // fase 2 disparada
        private Coroutine _fallTween;   // roda no runner do Tween — StopAllCoroutines não o alcança

        public override void OnFightStart(BossContext context)
        {
            // estado por LUTA re-inicializado aqui (componente sobrevive no pool)
            _fireStacks = 0;
            _slammed = false;
            _desperate = false;
            _fallTween = null;
        }

        public override void OnWeaknessHit(ElementType element)
        {
            _fireStacks = Mathf.Min(_fireStacks + 1, StacksForFullBlaze);
            // no desespero o vermelho domina — a brasa não regride o tinte de fase 2
            if (!_desperate) SetTint(EmberTint, _fireStacks / (float)StacksForFullBlaze);
            if (Context.Vfx != null) Context.Vfx.PlayGateBurst(Context.ViewPosition, EmberTint);
        }

        public override void OnHealthPhaseChanged(float normalizedHp)
        {
            // limiares canônicos 0.5/0.25 (contrato §1.14 — "66%/33%" da missão é aproximação)
            if (!_slammed && normalizedHp <= 0.5f)
            {
                _slammed = true;
                // batida no chão: o especial fica mais frequente — multiplicador no runtime
                if (Context.Runtime != null) Context.Runtime.SpecialCooldownMultiplier = 0.7f;
                Shake(1.6f, 0.35f);          // soma ao shake de fase do JuiceController (trauma acumula)
                PulseScale(0.18f, 0.30f);
            }

            if (!_desperate && normalizedHp <= 0.25f)
            {
                _desperate = true;
                // modo desespero: contato efetivo +25% via runtime (NUNCA mutar o SO)
                if (Context.Runtime != null) Context.Runtime.ContactDpsMultiplier = 1.25f;
                SetTint(DesperationTint, 1f);
            }
        }

        public override void OnDeath()
        {
            // tomba ~80° como árvore derrubada. Tween UNSCALED de propósito: o mundo está no
            // slow motion do golpe final (0,3×) — a queda em tempo real sobre o mundo lento
            // lê como cinemática e cabe na janela de ~1,2 s da sequência de morte.
            Transform view = Context.View;
            if (view == null) return;
            Quaternion from = view.rotation;
            Quaternion to = from * Quaternion.AngleAxis(80f, Vector3.right);   // eixo local: cai de frente
            _fallTween = Tween.Float(0f, 1f, 0.9f, Tween.Ease.OutCubic, t =>
            {
                if (view != null) view.rotation = Quaternion.SlerpUnclamped(from, to, t);
            });
        }

        protected override void OnFightEnd()
        {
            // a queda roda no runner do Tween — parar aqui evita girar a view já devolvida ao
            // pool (a rotação é re-setada no próximo SpawnView, mas não giramos objeto inativo)
            Tween.Stop(_fallTween);
            _fallTween = null;
        }
    }
}
