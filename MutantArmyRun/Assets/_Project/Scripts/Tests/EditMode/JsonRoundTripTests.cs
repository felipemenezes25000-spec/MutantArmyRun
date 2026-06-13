using NUnit.Framework;
using UnityEngine;
using MutantArmy.Domain;

namespace MutantArmy.Tests
{
    /// <summary>
    /// Round-trip do SaveData completo via JsonUtility (o serializador REAL usado pelo
    /// SaveSystem em device — por isso este teste é EditMode, não xUnit) + contrato de
    /// checksum/backup do doc 12 §4.7: payload adulterado falha e o load cai pro backup.
    /// </summary>
    public class JsonRoundTripTests
    {
        private static SaveData BuildFullSave()
        {
            var data = new SaveData
            {
                schemaVersion = SaveMigration.CurrentVersion,
                playerId = "uid-test-123",
                firstLaunchUnixUtc = 1750000000L,
                lastSaveUnixUtc = 1750000500L,
                playerLevel = 4,
                playerXp = 230,
                highestLevelCleared = 9,
                coins = 777,
                gems = 45,
                equippedSkinId = "soldier_neon",
                supplyCap = 60,
                adsRemoved = true,
                seasonPassActive = true,
                seasonPassExpiryUnix = 1760000000L,
                starterOfferState = "shown",
                levelsSinceInterstitial = 2,
                consecutiveDefeats = 1,
                usedReviveThisLevel = true,
                lastDailyChestUnix = 1749990000L,
                loginStreak = 3,
                sessionCount = 12,
                sfxOn = false,
                musicOn = true,
                hapticsOn = false,
                consentStatus = "granted",
                tutorialStepMask = 0b1011                        // passos 0, 1 e 3 vistos (v5)
            };
            data.levelRecords.Add(new LevelRecord { levelIndex = 9, won = true, bestSurvivors = 31, bestTime = 72.5f });
            data.units.Add(new UnitProgress { unitId = "unit_soldier", level = 3, shards = 12, unlocked = true });
            data.units.Add(new UnitProgress { unitId = "unit_mage", level = 1, shards = 4, unlocked = false });
            data.ownedSkinIds.Add("soldier_default");
            data.ownedSkinIds.Add("soldier_neon");
            data.upgradeTracks.Add(new TrackProgress { trackId = "StartDamage", level = 3 });
            data.bossCollection.Add(new BossCollectionMath.BossRecord
            {
                bossId = "golem_pedra",
                kills = 6,
                bestTimeSeconds = 27.5f,
                bestSurvivors = 18,
                weaknessDiscovered = true,
                rareKills = 2
            });
            return data;
        }

        // Mesma semântica de fallback do SaveSystem.Load (doc 12 §4.7), em cima do Domain.
        private static SaveData TryLoadPayload(string payload)
        {
            string json;
            if (!SaveChecksum.TryUnpack(payload, out json)) return null;
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            SaveMigration.Migrate(data);
            return data;
        }

        [Test]
        public void JsonUtility_RoundTrip_PreservesAllFields()
        {
            SaveData original = BuildFullSave();
            string json = JsonUtility.ToJson(original);
            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            Assert.NotNull(loaded);
            Assert.AreEqual(original.schemaVersion, loaded.schemaVersion);
            Assert.AreEqual(original.playerId, loaded.playerId);
            Assert.AreEqual(original.firstLaunchUnixUtc, loaded.firstLaunchUnixUtc);
            Assert.AreEqual(original.lastSaveUnixUtc, loaded.lastSaveUnixUtc);
            Assert.AreEqual(original.playerLevel, loaded.playerLevel);
            Assert.AreEqual(original.playerXp, loaded.playerXp);
            Assert.AreEqual(original.highestLevelCleared, loaded.highestLevelCleared);
            Assert.AreEqual(original.coins, loaded.coins);
            Assert.AreEqual(original.gems, loaded.gems);
            Assert.AreEqual(original.equippedSkinId, loaded.equippedSkinId);
            Assert.AreEqual(original.supplyCap, loaded.supplyCap);
            Assert.AreEqual(original.adsRemoved, loaded.adsRemoved);
            Assert.AreEqual(original.seasonPassActive, loaded.seasonPassActive);
            Assert.AreEqual(original.seasonPassExpiryUnix, loaded.seasonPassExpiryUnix);
            Assert.AreEqual(original.starterOfferState, loaded.starterOfferState);
            Assert.AreEqual(original.levelsSinceInterstitial, loaded.levelsSinceInterstitial);
            Assert.AreEqual(original.consecutiveDefeats, loaded.consecutiveDefeats);
            Assert.AreEqual(original.usedReviveThisLevel, loaded.usedReviveThisLevel);
            Assert.AreEqual(original.lastDailyChestUnix, loaded.lastDailyChestUnix);
            Assert.AreEqual(original.loginStreak, loaded.loginStreak);
            Assert.AreEqual(original.sessionCount, loaded.sessionCount);
            Assert.AreEqual(original.sfxOn, loaded.sfxOn);
            Assert.AreEqual(original.musicOn, loaded.musicOn);
            Assert.AreEqual(original.hapticsOn, loaded.hapticsOn);
            Assert.AreEqual(original.consentStatus, loaded.consentStatus);

            Assert.AreEqual(original.levelRecords.Count, loaded.levelRecords.Count);
            Assert.AreEqual(original.levelRecords[0].levelIndex, loaded.levelRecords[0].levelIndex);
            Assert.AreEqual(original.levelRecords[0].won, loaded.levelRecords[0].won);
            Assert.AreEqual(original.levelRecords[0].bestSurvivors, loaded.levelRecords[0].bestSurvivors);
            Assert.AreEqual(original.levelRecords[0].bestTime, loaded.levelRecords[0].bestTime);

            Assert.AreEqual(original.units.Count, loaded.units.Count);
            Assert.AreEqual(original.units[0].unitId, loaded.units[0].unitId);
            Assert.AreEqual(original.units[0].level, loaded.units[0].level);
            Assert.AreEqual(original.units[0].shards, loaded.units[0].shards);
            Assert.AreEqual(original.units[0].unlocked, loaded.units[0].unlocked);
            Assert.AreEqual(original.units[1].unitId, loaded.units[1].unitId);
            Assert.AreEqual(original.units[1].unlocked, loaded.units[1].unlocked);

            Assert.AreEqual(original.ownedSkinIds.Count, loaded.ownedSkinIds.Count);
            Assert.AreEqual(original.ownedSkinIds[0], loaded.ownedSkinIds[0]);
            Assert.AreEqual(original.ownedSkinIds[1], loaded.ownedSkinIds[1]);

            Assert.AreEqual(original.upgradeTracks.Count, loaded.upgradeTracks.Count);
            Assert.AreEqual(original.upgradeTracks[0].trackId, loaded.upgradeTracks[0].trackId);
            Assert.AreEqual(original.upgradeTracks[0].level, loaded.upgradeTracks[0].level);

            // Campos da missão Nota 10 (schema v5): álbum de bosses + máscara de tutorial
            Assert.AreEqual(original.tutorialStepMask, loaded.tutorialStepMask);
            Assert.AreEqual(original.bossCollection.Count, loaded.bossCollection.Count);
            Assert.AreEqual(original.bossCollection[0].bossId, loaded.bossCollection[0].bossId);
            Assert.AreEqual(original.bossCollection[0].kills, loaded.bossCollection[0].kills);
            Assert.AreEqual(original.bossCollection[0].bestTimeSeconds, loaded.bossCollection[0].bestTimeSeconds);
            Assert.AreEqual(original.bossCollection[0].bestSurvivors, loaded.bossCollection[0].bestSurvivors);
            Assert.AreEqual(original.bossCollection[0].weaknessDiscovered, loaded.bossCollection[0].weaknessDiscovered);
            Assert.AreEqual(original.bossCollection[0].rareKills, loaded.bossCollection[0].rareKills);
        }

        [Test]
        public void Migration_V4Payload_GanhaAlbumVazioEVersao5()
        {
            // Save v4 REAL: o JSON gravado antes da missão Nota 10 não tinha bossCollection/
            // tutorialStepMask. JsonUtility.FromJson sobre um JSON sem o campo mantém o valor
            // do ctor (lista já vazia) — o gate v5 garante o invariante mesmo se vier null.
            SaveData v4 = BuildFullSave();
            v4.schemaVersion = 4;
            v4.bossCollection = null;     // pior caso: campo nulo após desserialização legada
            v4.tutorialStepMask = 0;

            string payload = SaveChecksum.Pack(JsonUtility.ToJson(v4));
            SaveData loaded = TryLoadPayload(payload);   // Load real: unpack → FromJson → Migrate

            Assert.NotNull(loaded);
            Assert.AreEqual(SaveMigration.CurrentVersion, loaded.schemaVersion);
            Assert.NotNull(loaded.bossCollection);
            Assert.AreEqual(0, loaded.bossCollection.Count);
            Assert.AreEqual(0, loaded.tutorialStepMask);
            Assert.AreEqual(777, loaded.coins);          // dados v4 preservados pelo gate aditivo
        }

        [Test]
        public void Checksum_Pack_TryUnpack_RoundTrip()
        {
            string json = JsonUtility.ToJson(BuildFullSave());
            string payload = SaveChecksum.Pack(json);

            string unpacked;
            Assert.IsTrue(SaveChecksum.TryUnpack(payload, out unpacked));
            Assert.AreEqual(json, unpacked);
        }

        [Test]
        public void Checksum_TamperedPayload_FailsUnpack()
        {
            // coins = 777 no save cheio; adulterar o valor simula edição manual do arquivo.
            string json = JsonUtility.ToJson(BuildFullSave());
            StringAssert.Contains("777", json);
            string payload = SaveChecksum.Pack(json);
            string tampered = payload.Replace("777", "999999");

            string unpacked;
            Assert.IsFalse(SaveChecksum.TryUnpack(tampered, out unpacked));
            Assert.IsNull(unpacked);
        }

        [Test]
        public void Checksum_PayloadWithoutNewline_FailsWithoutThrow()
        {
            string unpacked;
            Assert.IsFalse(SaveChecksum.TryUnpack("payload-sem-quebra-de-linha", out unpacked));
            Assert.IsFalse(SaveChecksum.TryUnpack(string.Empty, out unpacked));
            Assert.IsFalse(SaveChecksum.TryUnpack(null, out unpacked));
        }

        [Test]
        public void Load_FallsBackToBackup_WhenMainChecksumInvalid()
        {
            SaveData original = BuildFullSave();
            string json = JsonUtility.ToJson(original);

            string mainPayload = SaveChecksum.Pack(json).Replace("777", "999999");   // principal corrompido
            string backupPayload = SaveChecksum.Pack(json);                          // backup íntegro

            // Sequência do SaveSystem.Load (doc 12 §4.7): main → backup → novo save.
            SaveData loaded = TryLoadPayload(mainPayload);
            Assert.IsNull(loaded, "Payload adulterado deveria falhar a verificação de checksum.");

            loaded = TryLoadPayload(backupPayload);
            Assert.NotNull(loaded, "Backup íntegro deveria carregar.");
            Assert.AreEqual(777, loaded.coins, "O fallback preserva as moedas do backup.");
            Assert.AreEqual(SaveMigration.CurrentVersion, loaded.schemaVersion);
        }
    }
}
