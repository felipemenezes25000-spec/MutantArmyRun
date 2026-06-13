using System.Collections.Generic;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    // Missão Nota 10 (CONTRACT §2): álbum de bosses — FindOrAdd idempotente,
    // RegisterKill só retorna true quando algum RECORDE melhora, TotalKills soma o álbum.
    public class BossCollectionMathTests
    {
        // ---------------------------------------------------------------- FindOrAdd

        [Fact]
        public void FindOrAdd_BossNovo_CriaEAnexaNaLista()
        {
            var list = new List<BossCollectionMath.BossRecord>();
            var r = BossCollectionMath.FindOrAdd(list, "golem_pedra");
            Assert.Single(list);
            Assert.Same(r, list[0]);
            Assert.Equal("golem_pedra", r.bossId);
            Assert.Equal(0, r.kills);
            Assert.Equal(0f, r.bestTimeSeconds);
            Assert.False(r.weaknessDiscovered);
        }

        [Fact]
        public void FindOrAdd_EhIdempotente_SegundaChamadaDevolveAMesmaInstancia()
        {
            var list = new List<BossCollectionMath.BossRecord>();
            var primeiro = BossCollectionMath.FindOrAdd(list, "golem_pedra");
            var segundo = BossCollectionMath.FindOrAdd(list, "golem_pedra");
            Assert.Same(primeiro, segundo);
            Assert.Single(list);
        }

        [Fact]
        public void FindOrAdd_BossesDiferentes_CriamRegistrosSeparados()
        {
            var list = new List<BossCollectionMath.BossRecord>();
            var a = BossCollectionMath.FindOrAdd(list, "golem_pedra");
            var b = BossCollectionMath.FindOrAdd(list, "zumbi_tita");
            Assert.NotSame(a, b);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void FindOrAdd_ListaNull_DegradaParaRegistroAvulso_SemLancar()
        {
            // null-safety greybox-friendly: save corrompido não derruba o álbum
            var r = BossCollectionMath.FindOrAdd(null, "golem_pedra");
            Assert.NotNull(r);
            Assert.Equal("golem_pedra", r.bossId);
        }

        [Fact]
        public void FindOrAdd_IgnoraEntradasNullDaLista()
        {
            // lista vinda de JSON adulterado pode conter null — o FindOrAdd pula sem lançar
            var list = new List<BossCollectionMath.BossRecord> { null };
            var r = BossCollectionMath.FindOrAdd(list, "golem_pedra");
            Assert.NotNull(r);
            Assert.Equal(2, list.Count);
        }

        // ---------------------------------------------------------------- RegisterKill

        [Fact]
        public void RegisterKill_PrimeiraVitoria_MelhoraRecordesEIncrementaKills()
        {
            var r = new BossCollectionMath.BossRecord { bossId = "golem_pedra" };
            bool improved = BossCollectionMath.RegisterKill(r, 30f, 12, usedWeakness: false, wasRare: false);
            Assert.True(improved);          // primeiro tempo e primeiros sobreviventes são recordes
            Assert.Equal(1, r.kills);
            Assert.Equal(30f, r.bestTimeSeconds, 3);
            Assert.Equal(12, r.bestSurvivors);
            Assert.Equal(0, r.rareKills);
            Assert.False(r.weaknessDiscovered);
        }

        [Fact]
        public void RegisterKill_TempoMenor_MelhoraORecorde()
        {
            var r = new BossCollectionMath.BossRecord { bossId = "b", kills = 1, bestTimeSeconds = 30f, bestSurvivors = 12 };
            bool improved = BossCollectionMath.RegisterKill(r, 20f, 5, usedWeakness: false, wasRare: false);
            Assert.True(improved);
            Assert.Equal(20f, r.bestTimeSeconds, 3);
            Assert.Equal(12, r.bestSurvivors);   // sobreviventes piores NÃO regridem o recorde
        }

        [Fact]
        public void RegisterKill_VitoriaPior_NaoMelhoraNada_MasContaOKill()
        {
            var r = new BossCollectionMath.BossRecord { bossId = "b", kills = 3, bestTimeSeconds = 20f, bestSurvivors = 12, weaknessDiscovered = true };
            bool improved = BossCollectionMath.RegisterKill(r, 40f, 5, usedWeakness: true, wasRare: false);
            Assert.False(improved);
            Assert.Equal(4, r.kills);            // kill SEMPRE conta, recorde intacto
            Assert.Equal(20f, r.bestTimeSeconds, 3);
            Assert.Equal(12, r.bestSurvivors);
        }

        [Fact]
        public void RegisterKill_MaisSobreviventes_MelhoraORecorde()
        {
            var r = new BossCollectionMath.BossRecord { bossId = "b", kills = 1, bestTimeSeconds = 20f, bestSurvivors = 12 };
            bool improved = BossCollectionMath.RegisterKill(r, 25f, 30, usedWeakness: false, wasRare: false);
            Assert.True(improved);
            Assert.Equal(30, r.bestSurvivors);
            Assert.Equal(20f, r.bestTimeSeconds, 3);   // tempo pior NÃO regride
        }

        [Fact]
        public void RegisterKill_PrimeiraVitoriaComFraqueza_DescobreEMelhora()
        {
            var r = new BossCollectionMath.BossRecord { bossId = "b", kills = 2, bestTimeSeconds = 10f, bestSurvivors = 50 };
            bool improved = BossCollectionMath.RegisterKill(r, 60f, 1, usedWeakness: true, wasRare: false);
            Assert.True(improved);               // descoberta de fraqueza É um recorde novo
            Assert.True(r.weaknessDiscovered);
        }

        [Fact]
        public void RegisterKill_VitoriaRara_IncrementaRareKills()
        {
            var r = new BossCollectionMath.BossRecord { bossId = "b", kills = 5, bestTimeSeconds = 10f, bestSurvivors = 50, weaknessDiscovered = true };
            BossCollectionMath.RegisterKill(r, 60f, 1, usedWeakness: false, wasRare: true);
            Assert.Equal(1, r.rareKills);
            Assert.Equal(6, r.kills);
        }

        [Fact]
        public void RegisterKill_TempoZero_NaoViraRecorde()
        {
            // 0 é sentinela de "sem medição" — nunca pode roubar o recorde de tempo
            var r = new BossCollectionMath.BossRecord { bossId = "b", kills = 1, bestTimeSeconds = 20f, bestSurvivors = 50, weaknessDiscovered = true };
            bool improved = BossCollectionMath.RegisterKill(r, 0f, 10, usedWeakness: false, wasRare: false);
            Assert.False(improved);
            Assert.Equal(20f, r.bestTimeSeconds, 3);
        }

        [Fact]
        public void RegisterKill_RecordNull_RetornaFalseSemLancar()
        {
            Assert.False(BossCollectionMath.RegisterKill(null, 10f, 5, usedWeakness: true, wasRare: true));
        }

        // ---------------------------------------------------------------- TotalKills

        [Fact]
        public void TotalKills_SomaTodosOsBossesDoAlbum()
        {
            var list = new List<BossCollectionMath.BossRecord>
            {
                new BossCollectionMath.BossRecord { bossId = "a", kills = 3 },
                new BossCollectionMath.BossRecord { bossId = "b", kills = 7 },
                null,   // entrada corrompida não derruba a soma
                new BossCollectionMath.BossRecord { bossId = "c", kills = 0 }
            };
            Assert.Equal(10, BossCollectionMath.TotalKills(list));
        }

        [Fact]
        public void TotalKills_ListaVaziaOuNull_RetornaZero()
        {
            Assert.Equal(0, BossCollectionMath.TotalKills(new List<BossCollectionMath.BossRecord>()));
            Assert.Equal(0, BossCollectionMath.TotalKills(null));
        }

        // ---------------------------------------------------------------- Fluxo integrado

        [Fact]
        public void FluxoCompleto_FindOrAddMaisRegisterKill_AtualizaOAlbum()
        {
            var album = new List<BossCollectionMath.BossRecord>();

            // 1ª vitória contra o golem (rara, com fraqueza)
            var golem = BossCollectionMath.FindOrAdd(album, "golem_pedra");
            Assert.True(BossCollectionMath.RegisterKill(golem, 45f, 8, usedWeakness: true, wasRare: true));

            // 2ª vitória, mais rápida
            Assert.True(BossCollectionMath.RegisterKill(BossCollectionMath.FindOrAdd(album, "golem_pedra"),
                                                        30f, 4, usedWeakness: true, wasRare: false));

            Assert.Single(album);
            Assert.Equal(2, album[0].kills);
            Assert.Equal(1, album[0].rareKills);
            Assert.Equal(30f, album[0].bestTimeSeconds, 3);
            Assert.Equal(8, album[0].bestSurvivors);
            Assert.True(album[0].weaknessDiscovered);
            Assert.Equal(2, BossCollectionMath.TotalKills(album));
        }
    }
}
