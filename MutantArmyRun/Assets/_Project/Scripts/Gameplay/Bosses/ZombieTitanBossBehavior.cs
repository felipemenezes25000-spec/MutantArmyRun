using System.Collections;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Zumbi Titã (m2_final_zombie_titan, fraco FOGO/LUZ — missão Nota 10, W2-A).
    /// Identidade: NUNCA luta sozinho — cada especial invoca uma horda de zumbis
    /// (BossManager.SpawnExtraWave com tropa placeholder); fase 1 = perde o braço, que vira
    /// um grupo rastejante COM DPS na arena (agregado, sem GameObject por inimigo) + pulso
    /// assimétrico de escala; fase 2 = grito que EMPURRA o exército (CrowdManager.KnockbackArmy).
    /// </summary>
    public sealed class ZombieTitanBossBehavior : BossBehavior
    {
        private const int HordeBaseCount = 3;        // zumbis por especial (+2 por fase: pressão crescente)
        private const int CrawlingArmCount = 4;      // "braço" = grupo agregado pequeno mas com mordida
        private const float ScreamKnockbackMeters = 2.5f;

        private bool _armLost;
        private bool _screamed;

        public override void OnFightStart(BossContext context)
        {
            _armLost = false;
            _screamed = false;
        }

        public override void OnSpecialAttackExecute()
        {
            // invoca hordas: o golpe genérico já machucou; a horda mantém a arena viva.
            // Placeholder de tropa = CrowdManager.DefaultUnit (decisão da missão; EnemyConfigSO
            // dedicado fica para o conteúdo da Onda 4).
            int phase = Context.Runtime != null ? Context.Runtime.Phase : 0;
            Context.SpawnExtraWave(Context.PlaceholderEnemy, HordeBaseCount + 2 * phase);
        }

        public override void OnHealthPhaseChanged(float normalizedHp)
        {
            if (!_armLost && normalizedHp <= 0.5f)
            {
                _armLost = true;
                // o braço rastejante: grupo agregado extra que ATACA (entra no TotalArenaDps)
                Context.SpawnExtraWave(Context.PlaceholderEnemy, CrawlingArmCount);
                if (FightActive) StartCoroutine(AsymmetricLurch());
            }

            if (!_screamed && normalizedHp <= 0.25f)
            {
                _screamed = true;
                Context.KnockbackArmy(ScreamKnockbackMeters);   // null-safe no CrowdManager
                Shake(2.2f, 0.45f);
                PulseScale(0.22f, 0.35f);
            }
        }

        // Pulso visual ASSIMÉTRICO: encolhe X e estica Y — silhueta "perdeu um pedaço".
        // Tempo scaled (sincroniza com a luta); StopAllCoroutines do End() cobre o abort, e o
        // SpawnView reseta a escala no reuso do pool (escala 0 → entrada).
        private IEnumerator AsymmetricLurch()
        {
            Transform view = Context.View;
            if (view == null) yield break;
            Vector3 baseScale = view.localScale;
            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (view == null) yield break;
                elapsed += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);   // vai-e-volta suave
                view.localScale = new Vector3(
                    baseScale.x * (1f - 0.22f * k),
                    baseScale.y * (1f + 0.10f * k),
                    baseScale.z);
                yield return null;
            }
            if (view != null) view.localScale = baseScale;
        }
    }
}
