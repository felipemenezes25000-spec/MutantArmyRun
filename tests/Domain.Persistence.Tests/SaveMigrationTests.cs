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
