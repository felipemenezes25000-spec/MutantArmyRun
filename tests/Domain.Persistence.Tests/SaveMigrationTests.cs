using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class SaveMigrationTests
    {
        /// <summary>Simula um save gravado pela v1 do app (campos novos ainda não existiam).</summary>
        private static SaveData SaveV1Legado()
        {
            return new SaveData
            {
                schemaVersion = 1,
                coins = 500,
                gems = 7,
                playerLevel = 3,
                playerXp = 120,
                highestLevelCleared = 6,
                supplyCap = 0,            // campo inexistente na v1 → desserializa como 0
                equippedSkinId = null,    // idem para as strings adicionadas depois
                starterOfferState = null,
                consentStatus = null,
                levelRecords = null,      // listas ausentes no JSON antigo viram null
                units = null,
                ownedSkinIds = null,
                upgradeTracks = null
            };
        }

        [Fact]
        public void VersaoAtual_EhMaiorQueUm()
        {
            // existir pelo menos um gate (v2) é pré-condição do esquema de migração
            Assert.True(SaveMigration.CurrentVersion >= 2);
        }

        [Fact]
        public void SaveV1_TerminaNaVersaoAtual()
        {
            var d = SaveV1Legado();
            SaveMigration.Migrate(d);
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
        }

        [Fact]
        public void SaveV1_PreservaMoedasEProgresso()
        {
            var d = SaveV1Legado();
            SaveMigration.Migrate(d);
            Assert.Equal(500L, d.coins);
            Assert.Equal(7, d.gems);
            Assert.Equal(3, d.playerLevel);
            Assert.Equal(120, d.playerXp);
            Assert.Equal(6, d.highestLevelCleared);
        }

        [Fact]
        public void SaveV1_AtravessaTodosOsGatesIncrementais()
        {
            // prova que os gates rodam em SEQUÊNCIA (nunca switch exclusivo):
            // o efeito do gate v2 (supplyCap) E o do gate v3 (strings) aparecem juntos
            var d = SaveV1Legado();
            SaveMigration.Migrate(d);
            Assert.Equal(60, d.supplyCap);                       // gate v2
            Assert.Equal("soldier_default", d.equippedSkinId);   // gate v3
            Assert.Equal("eligible", d.starterOfferState);       // gate v3
            Assert.Equal("unknown", d.consentStatus);            // gate v3
        }

        [Fact]
        public void SaveV1_ListasNulasViramVazias()
        {
            var d = SaveV1Legado();
            SaveMigration.Migrate(d);
            Assert.NotNull(d.levelRecords);
            Assert.NotNull(d.units);
            Assert.NotNull(d.ownedSkinIds);
            Assert.NotNull(d.upgradeTracks);
        }

        [Fact]
        public void SaveV2_SoRecebeGatesPosteriores()
        {
            // gate v2 NÃO re-executa sobre um save que já é v2 (supplyCap custom preservado);
            // gate v3 executa normalmente
            var d = new SaveData { schemaVersion = 2, supplyCap = 90, consentStatus = null };
            SaveMigration.Migrate(d);
            Assert.Equal(90, d.supplyCap);
            Assert.Equal("unknown", d.consentStatus);
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
        }

        [Fact]
        public void SaveNaVersaoAtual_MigrarEhIdempotente()
        {
            var d = new SaveData
            {
                schemaVersion = SaveMigration.CurrentVersion,
                coins = 999,
                supplyCap = 60,
                equippedSkinId = "soldier_gold"
            };
            SaveMigration.Migrate(d);
            SaveMigration.Migrate(d);
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
            Assert.Equal(999L, d.coins);
            Assert.Equal(60, d.supplyCap);
            Assert.Equal("soldier_gold", d.equippedSkinId);
        }

        [Fact]
        public void SaveV1_AtravessaOGateV4_ComCamposDeRetencaoNormalizados()
        {
            // o efeito do gate v4 aparece mesmo partindo da v1: a lista de missões nasce
            // vazia, contadores em 0 — e a migração segue até a versão ATUAL (>= 5).
            var d = SaveV1Legado();
            d.dailyMissions = null;   // campo inexistente na v1 → null no JSON
            SaveMigration.Migrate(d);
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
            Assert.NotNull(d.dailyMissions);
            Assert.Empty(d.dailyMissions);
            Assert.Equal(0, d.chestPityCounter);
            Assert.Equal(0L, d.lastLoginRewardUnix);
            Assert.Equal(0L, d.lastMissionResetUnix);
        }

        [Fact]
        public void SaveV3_RecebeGatesV4EV5()
        {
            // dados v3 preservados; gates v4 (missões) e v5 (álbum de bosses) rodam em sequência.
            var d = new SaveData { schemaVersion = 3, coins = 777, dailyMissions = null, bossCollection = null };
            SaveMigration.Migrate(d);
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
            Assert.Equal(777L, d.coins);
            Assert.NotNull(d.dailyMissions);
            Assert.NotNull(d.bossCollection);
        }

        [Fact]
        public void SaveV4_RecebeGateV5_ComAlbumNormalizado()
        {
            // save v4 real (missão Nota 10 não existia): bossCollection ausente no JSON → null;
            // tutorialStepMask desserializa como 0 (nenhum passo de tutorial visto).
            var d = new SaveData { schemaVersion = 4, coins = 555, bossCollection = null };
            SaveMigration.Migrate(d);
            Assert.Equal(5, d.schemaVersion);
            Assert.Equal(555L, d.coins);
            Assert.NotNull(d.bossCollection);
            Assert.Empty(d.bossCollection);
            Assert.Equal(0, d.tutorialStepMask);
        }

        [Fact]
        public void SaveV4_GateV5_PreservaDadosDaV4()
        {
            // gate v5 é puramente aditivo: missões/pity da v4 atravessam intactos.
            var d = new SaveData { schemaVersion = 4, chestPityCounter = 9, bossCollection = null };
            d.dailyMissions.Add(new MissionProgress { missionId = "win_levels", progress = 2, target = 3 });
            SaveMigration.Migrate(d);
            Assert.Equal(5, d.schemaVersion);
            Assert.Equal(9, d.chestPityCounter);
            Assert.Single(d.dailyMissions);
            Assert.Equal("win_levels", d.dailyMissions[0].missionId);
        }

        [Fact]
        public void SaveV5_NaoReexecutaOGateV5()
        {
            // álbum populado de um save já v5 nunca é tocado pela migração (idempotência).
            var d = new SaveData { schemaVersion = 5, tutorialStepMask = 7 };
            d.bossCollection.Add(new BossCollectionMath.BossRecord { bossId = "golem_pedra", kills = 4 });
            SaveMigration.Migrate(d);
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
            Assert.Single(d.bossCollection);
            Assert.Equal(4, d.bossCollection[0].kills);
            Assert.Equal(7, d.tutorialStepMask);
        }

        [Fact]
        public void SaveV1_AtravessaAteV5_ComAlbumNormalizado()
        {
            // a cadeia COMPLETA v1→v5: efeito de todos os gates num único Migrate.
            var d = SaveV1Legado();
            d.dailyMissions = null;
            d.bossCollection = null;
            SaveMigration.Migrate(d);
            Assert.Equal(5, d.schemaVersion);
            Assert.Equal(60, d.supplyCap);                       // gate v2
            Assert.Equal("soldier_default", d.equippedSkinId);   // gate v3
            Assert.NotNull(d.dailyMissions);                     // gate v4
            Assert.NotNull(d.bossCollection);                    // gate v5
            Assert.Empty(d.bossCollection);
        }

        [Fact]
        public void SaveDeVersaoFutura_NaoEhRebaixado()
        {
            // app antigo lendo save de app novo: nunca regredir a versão (dados ficam como estão)
            int futura = SaveMigration.CurrentVersion + 5;
            var d = new SaveData { schemaVersion = futura, coins = 42 };
            SaveMigration.Migrate(d);
            Assert.Equal(futura, d.schemaVersion);
            Assert.Equal(42L, d.coins);
        }
    }
}
