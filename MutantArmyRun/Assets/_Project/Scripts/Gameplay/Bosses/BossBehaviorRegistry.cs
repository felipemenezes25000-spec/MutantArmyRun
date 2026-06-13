using System;
using System.Collections.Generic;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Mapeamento bossId → tipo de BossBehavior (missão Nota 10, W2-A). Registry estático em
    /// vez de campo no BossConfigSO de propósito: o SO mora no Core, que NÃO enxerga Gameplay
    /// (fronteira de asmdef §1.2) — um campo de behavior lá criaria ciclo. O id do .asset
    /// (gerado pelo MvpContentFactory) é a chave natural.
    /// Boss sem entrada ganha GenericBossBehavior (reações leves) — todo boss reage a algo.
    /// </summary>
    public static class BossBehaviorRegistry
    {
        // ids canônicos dos .asset (Editor/MvpContentFactory.ConfigureBoss) — mudar lá exige mudar aqui.
        private static readonly Dictionary<string, Type> ByBossId = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { "m1_final_wood_giant", typeof(WoodGiantBossBehavior) },
            { "m2_final_zombie_titan", typeof(ZombieTitanBossBehavior) },
            { "m3_final_scorpion_mech", typeof(ScorpionMechBossBehavior) },
            { "m5_lava_dragon", typeof(LavaDragonBossBehavior) },
            { "m6_ice_king", typeof(IceKingBossBehavior) },
            { "m10_dimensional_entity", typeof(DimensionalEntityBossBehavior) },
        };

        /// <summary>Tipo do behavior do boss; GenericBossBehavior quando o id não tem entrada própria.</summary>
        public static Type Resolve(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId) && ByBossId.TryGetValue(bossId, out Type type)) return type;
            return typeof(GenericBossBehavior);
        }
    }
}
