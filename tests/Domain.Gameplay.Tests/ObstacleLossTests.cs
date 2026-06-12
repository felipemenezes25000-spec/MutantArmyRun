using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // Doc 12 §4.11: obstáculos/armadilhas da pista removem uma FRAÇÃO das unidades da faixa de
    // impacto; esquiva (Corredor/Ninja com DodgeTraps) e a trilha de meta ObstacleResist
    // (RunStartBonuses.obstacleLossFactor) reduzem a perda, multiplicando sobre a perda-base.
    public class ObstacleLossTests
    {
        [Fact]
        public void Neutro_SemEsquivaSemMeta_RetornaPerdaBase()
        {
            // dodgeRatio 0, lossFactor 1 (None) → perda = base
            float loss = CombatAbilities.ObstacleLossFraction(0.25f, 0f, 1f);
            Assert.Equal(0.25f, loss, 4);
        }

        [Fact]
        public void Esquiva_Total_ReduzAteOTeto()
        {
            // exército 100% esquivo: perde DodgeTrapsMaxReduction (60%) a menos → 0.25 × 0.4 = 0.10
            float loss = CombatAbilities.ObstacleLossFraction(0.25f, 1f, 1f);
            Assert.Equal(0.25f * (1f - CombatAbilities.DodgeTrapsMaxReduction), loss, 4);
        }

        [Fact]
        public void Esquiva_Parcial_ReduzLinear()
        {
            // metade do exército esquiva: redução pela metade do teto (30%) → 0.25 × 0.7 = 0.175
            float loss = CombatAbilities.ObstacleLossFraction(0.25f, 0.5f, 1f);
            Assert.Equal(0.175f, loss, 4);
        }

        [Fact]
        public void Meta_ObstacleResist_ReduzMultiplicativo()
        {
            // lossFactor 0.5 (ObstacleResist −50%) sobre a perda-base → 0.125
            float loss = CombatAbilities.ObstacleLossFraction(0.25f, 0f, 0.5f);
            Assert.Equal(0.125f, loss, 4);
        }

        [Fact]
        public void EsquivaEMeta_Combinam()
        {
            // 50% esquivo (×0.7) × meta 0.5 → 0.25 × 0.7 × 0.5 = 0.0875
            float loss = CombatAbilities.ObstacleLossFraction(0.25f, 0.5f, 0.5f);
            Assert.Equal(0.0875f, loss, 4);
        }

        [Fact]
        public void PerdaBaseZero_RetornaZero()
        {
            Assert.Equal(0f, CombatAbilities.ObstacleLossFraction(0f, 0f, 1f), 6);
        }

        [Fact]
        public void DodgeRatio_ForaDeFaixa_ClampadoEm01()
        {
            // dodgeRatio negativo e >1 são clampados (robustez): nenhum amplifica a perda
            float neg = CombatAbilities.ObstacleLossFraction(0.25f, -1f, 1f);
            float big = CombatAbilities.ObstacleLossFraction(0.25f, 5f, 1f);
            Assert.Equal(0.25f, neg, 4);                                          // como dodgeRatio 0
            Assert.Equal(0.25f * (1f - CombatAbilities.DodgeTrapsMaxReduction), big, 4);  // como dodgeRatio 1
        }

        [Fact]
        public void LossFactor_Negativo_NaoAumentaAPerda()
        {
            // meta nunca aumenta a perda além do dado: lossFactor < 0 é clampado a 0
            float loss = CombatAbilities.ObstacleLossFraction(0.25f, 0f, -2f);
            Assert.Equal(0f, loss, 6);
        }

        [Fact]
        public void Resultado_NuncaUltrapassa01()
        {
            // perda-base absurda é clampada em 1 (segurança do contrato)
            float loss = CombatAbilities.ObstacleLossFraction(5f, 0f, 1f);
            Assert.Equal(1f, loss, 6);
        }
    }
}
