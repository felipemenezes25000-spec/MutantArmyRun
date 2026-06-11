using System;
using System.Collections.Generic;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Modelo de save canônico (doc 12 §4.7). POCO de campos públicos — o JsonUtility do
    /// Unity serializa apenas campos, por isso o modelo NÃO usa propriedades.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // Save NOVO nasce na versão ATUAL do schema: a forma deste POCO já é a forma vigente,
        // então instalações frescas não devem atravessar gates de migração como se fossem
        // legados v1 (um gate futuro com efeito colateral atingiria saves novos por engano).
        // Saves antigos desserializados sobrescrevem este valor com a versão gravada no JSON.
        public int schemaVersion = SaveMigration.CurrentVersion;   // migração entre versões do app
        public string playerId;                       // Firebase anon UID (vazio offline)
        public long firstLaunchUnixUtc, lastSaveUnixUtc;

        // Progresso
        public int playerLevel = 1, playerXp;         // nv2 Upgrades, nv3 Baús... (CANON §8)
        public int highestLevelCleared;               // gate de desbloqueio do mapa
        public List<LevelRecord> levelRecords = new List<LevelRecord>();

        // Moedas e tropas
        public long coins;
        public int gems;
        public List<UnitProgress> units = new List<UnitProgress>();   // nível 1–10 + fragmentos por tropa
        public List<string> ownedSkinIds = new List<string>();
        public string equippedSkinId = "soldier_default";

        // Meta
        public List<TrackProgress> upgradeTracks = new List<TrackProgress>();  // 4 trilhas no MVP
        public int supplyCap = 60;                          // fixo no MVP (CANON §15)

        // Monetização / ads pacing (regras do CANON §11 dependem destes campos)
        public bool adsRemoved;
        public bool seasonPassActive;
        public long seasonPassExpiryUnix;
        public string starterOfferState = "eligible";       // eligible|shown|purchased|expired (48 h)
        public int levelsSinceInterstitial;
        public int consecutiveDefeats;
        public bool usedReviveThisLevel;

        // Retenção / sessão
        public long lastDailyChestUnix;
        public int loginStreak;
        public int sessionCount;

        // Configurações e consentimento
        public bool sfxOn = true, musicOn = true, hapticsOn = true;
        public string consentStatus = "unknown";            // resultado UMP cacheado
    }

    [Serializable]
    public class UnitProgress
    {
        public string unitId;
        public int level = 1;
        public int shards;
        public bool unlocked;
    }

    [Serializable]
    public class TrackProgress
    {
        public string trackId;
        public int level;
    }

    [Serializable]
    public class LevelRecord
    {
        public int levelIndex;
        public bool won;
        public int bestSurvivors;
        public float bestTime;
    }
}
