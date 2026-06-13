using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Rei de Gelo (m6_ice_king, fraco FOGO — missão Nota 10, W2-A; PREPARADO: lógica completa,
    /// visual placeholder). Identidade: ESCUDO DE GELO — começa recebendo só 40% do dano
    /// (VulnerabilityMultiplier 0.4); cada weakness hit (fogo) DERRETE 20% do escudo até o
    /// dano voltar a 100%. O tinte azul-gelo enfraquece junto: leitura direta do progresso.
    /// </summary>
    public sealed class IceKingBossBehavior : BossBehavior
    {
        private const float ShieldedVulnerability = 0.4f;
        private const float MeltPerHit = 0.2f;   // 5 vereditos de fraqueza derretem o escudo inteiro
        private static readonly Color IceTint = new Color(0.55f, 0.80f, 1.00f);

        private float _shield01;   // 1 = escudo intacto · 0 = derretido

        public override void OnFightStart(BossContext context)
        {
            _shield01 = 1f;
            if (context.Runtime != null) context.Runtime.VulnerabilityMultiplier = ShieldedVulnerability;
            SetTint(IceTint, 1f);
        }

        public override void OnWeaknessHit(ElementType element)
        {
            if (_shield01 <= 0f) return;   // escudo já derretido: fraqueza segue normal (1.5x do chart)

            _shield01 = Mathf.Max(0f, _shield01 - MeltPerHit);
            // escudo interpola 0.4 → 1.0 conforme derrete — sempre via runtime, nunca o SO
            if (Context.Runtime != null)
                Context.Runtime.VulnerabilityMultiplier = Mathf.Lerp(1f, ShieldedVulnerability, _shield01);
            SetTint(IceTint, _shield01);

            if (_shield01 <= 0f)
            {
                // escudo QUEBROU: momento de virada digno de juice próprio
                PulseScale(0.18f, 0.30f);
                Shake(1.4f, 0.30f);
                if (Context.Vfx != null) Context.Vfx.PlayGateBurst(Context.ViewPosition, IceTint);
            }
        }
    }
}
