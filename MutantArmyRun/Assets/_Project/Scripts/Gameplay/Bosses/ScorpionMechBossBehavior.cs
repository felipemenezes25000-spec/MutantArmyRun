using System.Collections;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Robô Escorpião (m3_final_scorpion_mech, fraco RAIO — missão Nota 10, W2-A).
    /// Identidade: ciclo de LASER com janela de NÚCLEO EXPOSTO — após cada especial, o núcleo
    /// abre por 3 s e o boss recebe dano DOBRADO (BossRuntime.VulnerabilityMultiplier 2.0,
    /// aplicado no funil BossManager.ApplyDamage). Risco/recompensa legível: aguente o laser,
    /// puna na abertura. Fase 1 = lança drones (SpawnExtraWave); fase 2 = laser ×1.5
    /// (SpecialDamageMultiplier no runtime). Marca BossManager.UsedLaserThisFight para o
    /// fail reason HitByLaser (FailReasonResolver).
    /// </summary>
    public sealed class ScorpionMechBossBehavior : BossBehavior
    {
        private const float CoreWindowSeconds = 3f;
        private const float CoreVulnerability = 2f;
        private const int DroneCount = 5;
        private static readonly Color CoreTint = new Color(0.40f, 0.90f, 1.00f);   // ciano-elétrico: núcleo aberto

        private bool _dronesLaunched;
        private bool _overcharged;
        private Coroutine _coreWindow;

        public override void OnFightStart(BossContext context)
        {
            _dronesLaunched = false;
            _overcharged = false;
            _coreWindow = null;
        }

        public override void OnSpecialAttackWarning()
        {
            PulseScale(0.12f, 0.25f);   // anticipação mecânica: o corpo arma junto do decal (VFXManager)
        }

        public override void OnSpecialAttackExecute()
        {
            // o laser conectou: registra para o fail reason HitByLaser (contrato §5)
            if (Context.Boss != null) Context.Boss.UsedLaserThisFight = true;

            // APÓS o golpe abre a janela de núcleo exposto — reinicia se já estava aberta
            if (_coreWindow != null) StopCoroutine(_coreWindow);
            if (FightActive) _coreWindow = StartCoroutine(ExposedCoreWindow());
        }

        public override void OnHealthPhaseChanged(float normalizedHp)
        {
            if (!_dronesLaunched && normalizedHp <= 0.5f)
            {
                _dronesLaunched = true;
                // drones = grupo agregado (placeholder de tropa até existir EnemyConfigSO dedicado)
                Context.SpawnExtraWave(Context.PlaceholderEnemy, DroneCount);
                PulseScale(0.15f, 0.30f);
            }

            if (!_overcharged && normalizedHp <= 0.25f)
            {
                _overcharged = true;
                // laser mais forte: ×1.5 efetivo via multiplicador no runtime (NUNCA mutar o SO)
                if (Context.Runtime != null) Context.Runtime.SpecialDamageMultiplier = 1.5f;
            }
        }

        // Janela do núcleo: ×2 de dano recebido por 3 s (tempo SCALED — o slow motion alonga a
        // janela junto do resto da luta, leitura justa). End() para a coroutine; o runtime morre
        // com a luta, então o multiplicador nunca vaza para a próxima.
        private IEnumerator ExposedCoreWindow()
        {
            BossRuntime runtime = Context.Runtime;
            if (runtime == null) yield break;

            runtime.VulnerabilityMultiplier = CoreVulnerability;
            SetTint(CoreTint, 0.8f);

            float elapsed = 0f;
            while (elapsed < CoreWindowSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            runtime.VulnerabilityMultiplier = 1f;
            SetTint(Color.white, 0f);
            _coreWindow = null;
        }

        protected override void OnFightEnd()
        {
            _coreWindow = null;   // StopAllCoroutines do End() já parou a janela
        }
    }
}
