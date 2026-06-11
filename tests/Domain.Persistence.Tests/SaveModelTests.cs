using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class SaveModelTests
    {
        // ---------- Defaults canônicos (doc 12 §4.7) ----------

        [Fact]
        public void NovoSave_TemDefaultsCanonicos()
        {
            var d = new SaveData();
            // Save novo nasce já na versão atual do schema — nunca como legado v1.
            Assert.Equal(SaveMigration.CurrentVersion, d.schemaVersion);
            Assert.Equal(1, d.playerLevel);
            Assert.Equal(0, d.playerXp);
            Assert.Equal(0, d.highestLevelCleared);
            Assert.Equal(0L, d.coins);
            Assert.Equal(0, d.gems);
            Assert.Equal(60, d.supplyCap);               // CANON §15: fixo no MVP
            Assert.False(d.adsRemoved);
            Assert.False(d.seasonPassActive);
            Assert.Equal("eligible", d.starterOfferState);
            Assert.Equal(0, d.levelsSinceInterstitial);
            Assert.Equal(0, d.consecutiveDefeats);
            Assert.False(d.usedReviveThisLevel);
            Assert.Equal(0, d.loginStreak);
            Assert.Equal(0, d.sessionCount);
            Assert.True(d.sfxOn);
            Assert.True(d.musicOn);
            Assert.True(d.hapticsOn);
            Assert.Equal("unknown", d.consentStatus);
            Assert.Equal("soldier_default", d.equippedSkinId);
        }

        [Fact]
        public void NovoSave_ListasInicializadasVazias()
        {
            var d = new SaveData();
            Assert.NotNull(d.levelRecords);
            Assert.Empty(d.levelRecords);
            Assert.NotNull(d.units);
            Assert.Empty(d.units);
            Assert.NotNull(d.ownedSkinIds);
            Assert.Empty(d.ownedSkinIds);
            Assert.NotNull(d.upgradeTracks);
            Assert.Empty(d.upgradeTracks);
        }

        [Fact]
        public void UnitProgress_TemDefaultsCanonicos()
        {
            var u = new UnitProgress();
            Assert.Equal(1, u.level);
            Assert.Equal(0, u.shards);
            Assert.False(u.unlocked);
            Assert.Null(u.unitId);
        }

        [Fact]
        public void TrackProgress_E_LevelRecord_SaoAtribuiveis()
        {
            var t = new TrackProgress { trackId = "start_damage", level = 3 };
            Assert.Equal("start_damage", t.trackId);
            Assert.Equal(3, t.level);

            var r = new LevelRecord { levelIndex = 7, won = true, bestSurvivors = 42, bestTime = 61.5f };
            Assert.Equal(7, r.levelIndex);
            Assert.True(r.won);
            Assert.Equal(42, r.bestSurvivors);
            Assert.Equal(61.5f, r.bestTime, 3);
        }

        // ---------- Contrato de serialização (JsonUtility exige campos públicos) ----------

        [Theory]
        [InlineData("schemaVersion")]
        [InlineData("playerId")]
        [InlineData("firstLaunchUnixUtc")]
        [InlineData("lastSaveUnixUtc")]
        [InlineData("playerLevel")]
        [InlineData("playerXp")]
        [InlineData("highestLevelCleared")]
        [InlineData("levelRecords")]
        [InlineData("coins")]
        [InlineData("gems")]
        [InlineData("units")]
        [InlineData("ownedSkinIds")]
        [InlineData("equippedSkinId")]
        [InlineData("upgradeTracks")]
        [InlineData("supplyCap")]
        [InlineData("adsRemoved")]
        [InlineData("seasonPassActive")]
        [InlineData("seasonPassExpiryUnix")]
        [InlineData("starterOfferState")]
        [InlineData("levelsSinceInterstitial")]
        [InlineData("consecutiveDefeats")]
        [InlineData("usedReviveThisLevel")]
        [InlineData("lastDailyChestUnix")]
        [InlineData("loginStreak")]
        [InlineData("sessionCount")]
        [InlineData("sfxOn")]
        [InlineData("musicOn")]
        [InlineData("hapticsOn")]
        [InlineData("consentStatus")]
        public void SaveData_TemCampoPublicoDeInstancia(string nome)
        {
            FieldInfo f = typeof(SaveData).GetField(nome, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(f);
        }

        [Theory]
        [InlineData(typeof(SaveData))]
        [InlineData(typeof(UnitProgress))]
        [InlineData(typeof(TrackProgress))]
        [InlineData(typeof(LevelRecord))]
        public void TiposDoModelo_TemAtributoSerializable(Type tipo)
        {
            // JsonUtility do Unity exige [Serializable] nos tipos aninhados
            Assert.NotNull(tipo.GetCustomAttribute<SerializableAttribute>());
        }

        // ---------- Round-trip JSON por campos (espelha o que o JsonUtility fará no Unity) ----------

        [Fact]
        public void RoundTripJson_PreservaTodosOsCampos()
        {
            var opts = new JsonSerializerOptions { IncludeFields = true };
            var original = new SaveData
            {
                schemaVersion = 3,
                playerId = "anon-123",
                firstLaunchUnixUtc = 1_770_000_000L,
                lastSaveUnixUtc = 1_770_000_500L,
                playerLevel = 4,
                playerXp = 250,
                highestLevelCleared = 9,
                coins = 12_345L,
                gems = 30,
                equippedSkinId = "soldier_red",
                supplyCap = 60,
                adsRemoved = true,
                seasonPassActive = false,
                seasonPassExpiryUnix = 0L,
                starterOfferState = "shown",
                levelsSinceInterstitial = 2,
                consecutiveDefeats = 1,
                usedReviveThisLevel = true,
                lastDailyChestUnix = 1_769_999_000L,
                loginStreak = 5,
                sessionCount = 17,
                sfxOn = false,
                musicOn = true,
                hapticsOn = false,
                consentStatus = "granted"
            };
            original.levelRecords.Add(new LevelRecord { levelIndex = 9, won = true, bestSurvivors = 33, bestTime = 58.2f });
            original.units.Add(new UnitProgress { unitId = "soldier", level = 2, shards = 15, unlocked = true });
            original.ownedSkinIds.Add("soldier_red");
            original.upgradeTracks.Add(new TrackProgress { trackId = "start_army", level = 4 });

            string json = JsonSerializer.Serialize(original, opts);
            SaveData copia = JsonSerializer.Deserialize<SaveData>(json, opts);

            Assert.Equal(original.schemaVersion, copia.schemaVersion);
            Assert.Equal(original.playerId, copia.playerId);
            Assert.Equal(original.firstLaunchUnixUtc, copia.firstLaunchUnixUtc);
            Assert.Equal(original.lastSaveUnixUtc, copia.lastSaveUnixUtc);
            Assert.Equal(original.playerLevel, copia.playerLevel);
            Assert.Equal(original.playerXp, copia.playerXp);
            Assert.Equal(original.highestLevelCleared, copia.highestLevelCleared);
            Assert.Equal(original.coins, copia.coins);
            Assert.Equal(original.gems, copia.gems);
            Assert.Equal(original.equippedSkinId, copia.equippedSkinId);
            Assert.Equal(original.supplyCap, copia.supplyCap);
            Assert.Equal(original.adsRemoved, copia.adsRemoved);
            Assert.Equal(original.seasonPassActive, copia.seasonPassActive);
            Assert.Equal(original.seasonPassExpiryUnix, copia.seasonPassExpiryUnix);
            Assert.Equal(original.starterOfferState, copia.starterOfferState);
            Assert.Equal(original.levelsSinceInterstitial, copia.levelsSinceInterstitial);
            Assert.Equal(original.consecutiveDefeats, copia.consecutiveDefeats);
            Assert.Equal(original.usedReviveThisLevel, copia.usedReviveThisLevel);
            Assert.Equal(original.lastDailyChestUnix, copia.lastDailyChestUnix);
            Assert.Equal(original.loginStreak, copia.loginStreak);
            Assert.Equal(original.sessionCount, copia.sessionCount);
            Assert.Equal(original.sfxOn, copia.sfxOn);
            Assert.Equal(original.musicOn, copia.musicOn);
            Assert.Equal(original.hapticsOn, copia.hapticsOn);
            Assert.Equal(original.consentStatus, copia.consentStatus);

            Assert.Single(copia.levelRecords);
            Assert.Equal(9, copia.levelRecords[0].levelIndex);
            Assert.True(copia.levelRecords[0].won);
            Assert.Equal(33, copia.levelRecords[0].bestSurvivors);
            Assert.Equal(58.2f, copia.levelRecords[0].bestTime, 3);

            Assert.Single(copia.units);
            Assert.Equal("soldier", copia.units[0].unitId);
            Assert.Equal(2, copia.units[0].level);
            Assert.Equal(15, copia.units[0].shards);
            Assert.True(copia.units[0].unlocked);

            Assert.Single(copia.ownedSkinIds);
            Assert.Equal("soldier_red", copia.ownedSkinIds[0]);

            Assert.Single(copia.upgradeTracks);
            Assert.Equal("start_army", copia.upgradeTracks[0].trackId);
            Assert.Equal(4, copia.upgradeTracks[0].level);
        }
    }
}
