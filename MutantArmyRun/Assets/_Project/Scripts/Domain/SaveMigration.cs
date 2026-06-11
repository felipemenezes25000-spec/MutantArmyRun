using System.Collections.Generic;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Migração de schema do save (doc 12 §4.7).
    /// Gates INCREMENTAIS: executam em sequência — um save v1 atravessa v2, v3... até o atual.
    /// NUNCA usar switch exclusivo por versão (pularia etapas); NUNCA remover um gate antigo.
    /// </summary>
    public static class SaveMigration
    {
        public const int CurrentVersion = 3;

        public static void Migrate(SaveData d)
        {
            if (d == null) return;

            // ---- v2: campos estruturais adicionados após a v1 ----
            // Saves v1 desserializam supplyCap como 0 (campo não existia no JSON) e
            // podem trazer listas null; normaliza para os defaults canônicos.
            if (d.schemaVersion < 2)
            {
                if (d.supplyCap <= 0) d.supplyCap = 60;          // CANON §15: cap fixo do MVP
                if (d.levelRecords == null) d.levelRecords = new List<LevelRecord>();
                if (d.units == null) d.units = new List<UnitProgress>();
                if (d.ownedSkinIds == null) d.ownedSkinIds = new List<string>();
                if (d.upgradeTracks == null) d.upgradeTracks = new List<TrackProgress>();
                d.schemaVersion = 2;
            }

            // ---- v3: strings de monetização/consentimento adicionadas após a v2 ----
            // null/vazio recebe o default canônico do modelo (doc 12 §4.7).
            if (d.schemaVersion < 3)
            {
                if (string.IsNullOrEmpty(d.equippedSkinId)) d.equippedSkinId = "soldier_default";
                if (string.IsNullOrEmpty(d.starterOfferState)) d.starterOfferState = "eligible";
                if (string.IsNullOrEmpty(d.consentStatus)) d.consentStatus = "unknown";
                d.schemaVersion = 3;
            }

            // Versão futura (app antigo lendo save de app novo): não rebaixar nem tocar nos dados.
        }
    }
}
