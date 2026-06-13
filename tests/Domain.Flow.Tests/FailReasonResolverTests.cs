using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    // Missão Nota 10 (CONTRACT §2): motivo RICO de derrota com cadeia de prioridade fixa
    // WrongElement > HitByLaser > HitByLava > TooManyLossesOnTrack > ArmyTooSmall >
    // BossResistedDamage > LowUpgradePower. Em derrota NUNCA resolve None.
    public class FailReasonResolverTests
    {
        // Derrota "limpa": nenhum gatilho específico — cai no fallback LowUpgradePower.
        // Exército saudável (peak 40, chegou com 20, poucas perdas) e dano >= 50% do HP.
        private static FailReasonResolver.DefeatContext DerrotaGenerica() => new FailReasonResolver.DefeatContext
        {
            armySizeAtBossStart = 20,
            unitsLostOnTrack = 5,
            armyPeak = 40,
            damageDealtToBoss = 600f,
            bossMaxHp = 1000f,
            bossUsedLaser = false,
            armyHadResistedElement = false,
            diedOnTrack = false,
            diedToHazard = false
        };

        // ---------------------------------------------------------------- Cada razão isolada

        [Fact]
        public void ElementoResistido_ResolveWrongElement()
        {
            var c = DerrotaGenerica();
            c.armyHadResistedElement = true;
            Assert.Equal(FailReason.WrongElement, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void LaserConectouNaArena_ResolveHitByLaser()
        {
            var c = DerrotaGenerica();
            c.bossUsedLaser = true;
            Assert.Equal(FailReason.HitByLaser, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void LaserMasMorreuNaPista_NaoEhHitByLaser()
        {
            // Quem zerou na pista nunca viu o especial — o laser não pode ser "a causa".
            var c = DerrotaGenerica();
            c.bossUsedLaser = true;
            c.diedOnTrack = true;
            Assert.NotEqual(FailReason.HitByLaser, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void GolpeFatalDeHazard_ResolveHitByLava()
        {
            var c = DerrotaGenerica();
            c.diedToHazard = true;
            Assert.Equal(FailReason.HitByLava, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void MetadeDoPicoPerdidaNaPista_ResolveTooManyLosses()
        {
            var c = DerrotaGenerica();
            c.armyPeak = 40;
            c.unitsLostOnTrack = 20;   // exatamente peak/2 — fronteira inclusiva (>=)
            Assert.Equal(FailReason.TooManyLossesOnTrack, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void PerdasAbaixoDaMetade_NaoEhTooManyLosses()
        {
            var c = DerrotaGenerica();
            c.armyPeak = 40;
            c.unitsLostOnTrack = 19;
            Assert.NotEqual(FailReason.TooManyLossesOnTrack, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void PicoMenorQue10_NuncaAcusaTooManyLosses()
        {
            // corrida que nunca cresceu: perder "metade" de 4 unidades não é a lição certa
            var c = DerrotaGenerica();
            c.armyPeak = 4;
            c.unitsLostOnTrack = 4;       // >= peak/2, mas o piso de pico (>= 10) desarma a acusação
            c.armySizeAtBossStart = 20;   // gatilhos seguintes (ArmyTooSmall) também desarmados
            Assert.NotEqual(FailReason.TooManyLossesOnTrack, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void ChegouComMenosDe10_ResolveArmyTooSmall()
        {
            var c = DerrotaGenerica();
            c.armySizeAtBossStart = 9;
            c.unitsLostOnTrack = 5;       // abaixo de peak/2 (40/2=20) — não rouba a prioridade
            Assert.Equal(FailReason.ArmyTooSmall, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void ChegouComExatamente10_NaoEhArmyTooSmall()
        {
            var c = DerrotaGenerica();
            c.armySizeAtBossStart = 10;   // fronteira: < 10 estrito
            Assert.NotEqual(FailReason.ArmyTooSmall, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void DanoAbaixoDaMetadeDoHp_ResolveBossResistedDamage()
        {
            var c = DerrotaGenerica();
            c.damageDealtToBoss = 499f;
            c.bossMaxHp = 1000f;
            Assert.Equal(FailReason.BossResistedDamage, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void DanoNaMetadeExata_NaoEhBossResistedDamage()
        {
            var c = DerrotaGenerica();
            c.damageDealtToBoss = 500f;   // fronteira: < 0.5×HP estrito
            c.bossMaxHp = 1000f;
            Assert.Equal(FailReason.LowUpgradePower, FailReasonResolver.Resolve(c));
        }

        // ---------------------------------------------------------------- Prioridade da cadeia

        [Fact]
        public void TudoAoMesmoTempo_WrongElementVence()
        {
            var c = new FailReasonResolver.DefeatContext
            {
                armySizeAtBossStart = 3,
                unitsLostOnTrack = 30,
                armyPeak = 40,
                damageDealtToBoss = 10f,
                bossMaxHp = 1000f,
                bossUsedLaser = true,
                armyHadResistedElement = true,
                diedOnTrack = false,
                diedToHazard = true
            };
            Assert.Equal(FailReason.WrongElement, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void SemWrongElement_LaserVenceLava()
        {
            var c = DerrotaGenerica();
            c.bossUsedLaser = true;
            c.diedToHazard = true;
            Assert.Equal(FailReason.HitByLaser, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void LaserAnuladoPorMorteNaPista_LavaVence()
        {
            var c = DerrotaGenerica();
            c.bossUsedLaser = true;
            c.diedOnTrack = true;
            c.diedToHazard = true;
            Assert.Equal(FailReason.HitByLava, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void SemCausaDireta_PerdasVencemTamanho()
        {
            var c = DerrotaGenerica();
            c.armyPeak = 40;
            c.unitsLostOnTrack = 25;
            c.armySizeAtBossStart = 5;
            Assert.Equal(FailReason.TooManyLossesOnTrack, FailReasonResolver.Resolve(c));
        }

        [Fact]
        public void TamanhoVenceDanoInsuficiente()
        {
            var c = DerrotaGenerica();
            c.armySizeAtBossStart = 5;
            c.damageDealtToBoss = 10f;
            Assert.Equal(FailReason.ArmyTooSmall, FailReasonResolver.Resolve(c));
        }

        // ---------------------------------------------------------------- Nunca None em derrota

        [Fact]
        public void DerrotaSemNenhumGatilho_ResolveLowUpgradePower_NuncaNone()
        {
            var c = DerrotaGenerica();
            var reason = FailReasonResolver.Resolve(c);
            Assert.Equal(FailReason.LowUpgradePower, reason);
            Assert.NotEqual(FailReason.None, reason);
        }

        [Fact]
        public void ContextoZerado_AindaResolveAlgo_NuncaNone()
        {
            // default(struct): tudo 0/false — dano 0 < HP 0 × 0.5 é falso, peak 0 < 10...
            // a cadeia desce até ArmyTooSmall (0 < 10). O contrato vale: nunca None.
            var reason = FailReasonResolver.Resolve(default);
            Assert.NotEqual(FailReason.None, reason);
            Assert.Equal(FailReason.ArmyTooSmall, reason);
        }
    }
}
