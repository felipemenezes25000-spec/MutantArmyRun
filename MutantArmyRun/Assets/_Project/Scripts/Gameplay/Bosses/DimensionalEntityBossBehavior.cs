using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Entidade Dimensional (m10_dimensional_entity, fraqueza ROTATIVA — missão Nota 10, W2-A;
    /// PREPARADO: lógica completa, visual placeholder). A rotação de fraqueza por fase JÁ é
    /// do loop genérico (BossConfigSO.rotatingWeakness + BossRuntime.RotateWeakness) — aqui
    /// entra só a identidade do especial "PORTAL NEGATIVO": além do dano, suga 10% do exército
    /// e converte em moedas; 15% de chance de a Entidade CAIR NO PRÓPRIO PORTAL (10% do MaxHp
    /// de dano + feedback de fraqueza). RNG derivado da seed (contrato §1.6) — determinístico.
    /// </summary>
    public sealed class DimensionalEntityBossBehavior : BossBehavior
    {
        private const float ArmyConvertFraction = 0.10f;
        private const double SelfPortalChance = 0.15;
        private const float SelfPortalDamageFraction = 0.10f;
        // moedas por unidade sugada = mesmo rate default do overflow de Supply (RcKeys
        // supply_overflow_coin_rate = 2) — o portal "paga" o que rouba, surpresa justa
        private const int CoinsPerConvertedUnit = 2;
        private static readonly Color VoidTint = new Color(0.55f, 0.30f, 0.90f);

        private System.Random _rng;

        public override void OnFightStart(BossContext context)
        {
            // primo/offset próprios: não compete com pista (seed), risco (×486187739+1),
            // inimigos (×92821+7) nem com o roll de raro do BossManager (×48611+3)
            _rng = context.CreateDerivedRng(48611, 7);
        }

        public override void OnSpecialAttackExecute()
        {
            ConvertArmySlice();

            // 15% de chance de cair no próprio portal — SEMPRE consome 1 draw (ordem de
            // consumo do RNG é contrato de determinismo, mesmo padrão do RareBossMath.Roll)
            bool selfHit = _rng != null && _rng.NextDouble() < SelfPortalChance;
            if (!selfHit) return;

            BossRuntime runtime = Context.Runtime;
            BossManager boss = Context.Boss;
            if (runtime == null || boss == null) return;

            float selfDamage = runtime.MaxHp * SelfPortalDamageFraction;
            ElementType weakness = runtime.ActiveWeakness;
            Vector3 at = Context.ViewPosition;
            boss.ApplyDamage(selfDamage);   // funil oficial: fases/morte continuam corretas

            // feedback de FRAQUEZA: o jogador entende que o portal puniu o próprio boss.
            // One-shot fora do rate-limit do CombatSystem — evento raro, não spam.
            GameEvents.RaiseBossElementalHit(new BossElementalHit(
                weakness, ElementRelation.Weakness, selfDamage, at));
            FlashTint(VoidTint, 0.8f, 0.35f);
            Shake(1.5f, 0.30f);
        }

        // Portal negativo: converte 10% do exército em moedas pela API de remoção EXISTENTE
        // (ReconcileTo — funil único, despawn burst e CrowdChanged inclusos) + RaiseSupplyOverflow
        // para o crédito de moedas (a Meta já assina). Piso de 1 unidade preservado.
        private void ConvertArmySlice()
        {
            CrowdManager crowd = Context.Crowd;
            if (crowd == null || crowd.Count <= 1) return;

            int toConvert = Mathf.Min(Mathf.RoundToInt(crowd.Count * ArmyConvertFraction), crowd.Count - 1);
            if (toConvert <= 0) return;

            crowd.ReconcileTo(crowd.Count - toConvert, null);
            GameEvents.RaiseSupplyOverflow(new SupplyOverflow(toConvert, toConvert * CoinsPerConvertedUnit));
        }
    }
}
