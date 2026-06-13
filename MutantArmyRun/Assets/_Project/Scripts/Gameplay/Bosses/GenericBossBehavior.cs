using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Behavior padrão para bosses SEM entrada no BossBehaviorRegistry (missão Nota 10):
    /// reações LEVES e universais — pulso de escala ao trocar de fase e tinte rápido em
    /// weakness hit. Garante que nenhum dos 20 bosses fique 100% inerte mesmo antes de
    /// ganhar behavior próprio. Zero estado entre lutas (seguro no pool por construção).
    /// </summary>
    public sealed class GenericBossBehavior : BossBehavior
    {
        // dourado-quente: lê como "está funcionando!" sem competir com o flash vermelho de hit
        private static readonly Color WeaknessFlash = new Color(1f, 0.85f, 0.30f);

        public override void OnHealthPhaseChanged(float normalizedHp)
        {
            PulseScale(0.15f, 0.30f);   // fase nova: o corpo "acusa" a virada junto do shake do JuiceController
        }

        public override void OnWeaknessHit(ElementType element)
        {
            FlashTint(WeaknessFlash, 0.55f, 0.30f);   // tinte rápido: o plano do Scout está pagando
        }
    }
}
