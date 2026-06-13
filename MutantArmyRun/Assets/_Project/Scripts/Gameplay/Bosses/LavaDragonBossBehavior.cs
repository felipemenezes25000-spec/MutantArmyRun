using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Dragão de Lava (m5_lava_dragon, fraco GELO — missão Nota 10, W2-A; PREPARADO: lógica
    /// completa, visual placeholder). Identidade: VOA em ciclos — 4 s no ar recebendo só 25%
    /// do dano (VulnerabilityMultiplier 0.25); GELO DERRUBA: weakness hit de Ice durante o
    /// voo pousa o dragão na hora e o deixa vulnerável (×2.0) por 2 s. Ensina o contra-pick
    /// do mundo de lava sem nenhum número novo no SO.
    /// </summary>
    public sealed class LavaDragonBossBehavior : BossBehavior
    {
        private const float GroundSeconds = 8f;
        private const float AirSeconds = 4f;
        private const float AirVulnerability = 0.25f;
        private const float ForcedLandVulnerability = 2f;
        private const float ForcedLandSeconds = 2f;
        private const float FlightHeightMeters = 3f;   // placeholder visual: a view sobe/da view desce

        private bool _airborne;
        private float _cycleTimer;
        private float _forcedLandTimer;
        private Vector3 _groundPosition;
        private bool _hasGroundPosition;

        public override void OnFightStart(BossContext context)
        {
            _airborne = false;
            _cycleTimer = GroundSeconds;
            _forcedLandTimer = 0f;
            _hasGroundPosition = false;
            if (context.Runtime != null) context.Runtime.VulnerabilityMultiplier = 1f;
        }

        public override void OnWeaknessHit(ElementType element)
        {
            // gelo derruba: só o ELEMENTO certo durante o VOO dispara o pouso forçado
            if (_airborne && element == ElementType.Ice) Land(forced: true);
        }

        private void Update()
        {
            if (!RuntimeAlive) return;   // sequência de morte/fim de luta: dragão para de ciclar
            float dt = Time.deltaTime;

            // janela de pouso forçado (vulnerável ×2) corre antes do ciclo normal retomar
            if (_forcedLandTimer > 0f)
            {
                _forcedLandTimer -= dt;
                if (_forcedLandTimer <= 0f && Context.Runtime != null)
                    Context.Runtime.VulnerabilityMultiplier = 1f;
            }
            else
            {
                _cycleTimer -= dt;
                if (_cycleTimer <= 0f)
                {
                    if (_airborne) Land(forced: false);
                    else TakeOff();
                }
            }

            TickFlightVisual(dt);
        }

        private void TakeOff()
        {
            _airborne = true;
            _cycleTimer = AirSeconds;
            if (Context.Runtime != null) Context.Runtime.VulnerabilityMultiplier = AirVulnerability;
            PulseScale(0.12f, 0.25f);
        }

        private void Land(bool forced)
        {
            _airborne = false;
            _cycleTimer = GroundSeconds;
            if (forced)
            {
                // derrubado pelo gelo: pousa JÁ e fica escancarado por 2 s
                _forcedLandTimer = ForcedLandSeconds;
                if (Context.Runtime != null) Context.Runtime.VulnerabilityMultiplier = ForcedLandVulnerability;
                Shake(1.8f, 0.35f);
                PulseScale(0.20f, 0.30f);
            }
            else if (Context.Runtime != null)
            {
                Context.Runtime.VulnerabilityMultiplier = 1f;
            }
        }

        // Placeholder visual do voo: a raiz da view sobe FlightHeightMeters com damping
        // exponencial (framerate-independente). O BossManager não move a view após o spawn,
        // então este behavior é o único dono da posição durante a luta.
        private void TickFlightVisual(float dt)
        {
            Transform view = Context.View;
            if (view == null) return;
            if (!_hasGroundPosition)
            {
                _groundPosition = view.position;
                _hasGroundPosition = true;
            }
            Vector3 target = _groundPosition + (_airborne ? Vector3.up * FlightHeightMeters : Vector3.zero);
            view.position = Vector3.Lerp(view.position, target, 1f - Mathf.Exp(-6f * dt));
        }

        protected override void OnFightEnd()
        {
            // devolve a view ao chão antes do release — o pool reusa a instância como ela ficou
            if (_hasGroundPosition && Context.View != null) Context.View.position = _groundPosition;
            _hasGroundPosition = false;
        }
    }
}
