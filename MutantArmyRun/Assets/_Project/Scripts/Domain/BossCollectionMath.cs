using System;
using System.Collections.Generic;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Álbum de bosses (missão Nota 10): recordes por boss persistidos no SaveData
    /// (lista bossCollection, schema v5). Mesmo espírito do SeasonPassMath — dado puro
    /// que a UI formata sozinha; o BossCollectionSystem (Meta) chama RegisterKill no
    /// OnBossDied e grava via MarkSaveDirty.
    /// </summary>
    public static class BossCollectionMath
    {
        /// <summary>
        /// Recorde de UM boss no save. Classe [Serializable] de campos públicos —
        /// JsonUtility do Unity não serializa structs aninhadas em List nem propriedades.
        /// bestTimeSeconds = 0 significa "sem recorde ainda" (nunca lutou/venceu).
        /// </summary>
        [Serializable]
        public class BossRecord
        {
            public string bossId;
            public int kills;
            public float bestTimeSeconds;     // menor tempo de luta (0 = sem recorde)
            public int bestSurvivors;         // maior nº de sobreviventes numa vitória
            public bool weaknessDiscovered;   // jogador já venceu explorando a fraqueza
            public int rareKills;             // vitórias contra a variante rara
        }

        /// <summary>
        /// Busca o recorde do boss na lista; cria e anexa se não existir (idempotente:
        /// segunda chamada devolve a MESMA instância). Lista null degrada para um recorde
        /// avulso — nunca lança (regra de null-safety greybox-friendly).
        /// </summary>
        public static BossRecord FindOrAdd(List<BossRecord> list, string bossId)
        {
            if (bossId == null) bossId = string.Empty;
            if (list == null) return new BossRecord { bossId = bossId };

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].bossId == bossId) return list[i];
            }

            var record = new BossRecord { bossId = bossId };
            list.Add(record);
            return record;
        }

        /// <summary>
        /// Registra UMA vitória sobre o boss e atualiza recordes. Retorna true se algum
        /// recorde melhorou (tempo menor, mais sobreviventes ou fraqueza descoberta) —
        /// gatilho do toast "NOVO RECORDE!" na UI. kills/rareKills sempre incrementam.
        /// </summary>
        public static bool RegisterKill(BossRecord r, float timeSeconds, int survivors, bool usedWeakness, bool wasRare)
        {
            if (r == null) return false;

            bool improved = false;

            // Tempo 0 é sentinela de "sem recorde" — só tempos positivos competem.
            if (timeSeconds > 0f && (r.bestTimeSeconds <= 0f || timeSeconds < r.bestTimeSeconds))
            {
                r.bestTimeSeconds = timeSeconds;
                improved = true;
            }

            if (survivors > r.bestSurvivors)
            {
                r.bestSurvivors = survivors;
                improved = true;
            }

            if (usedWeakness && !r.weaknessDiscovered)
            {
                r.weaknessDiscovered = true;
                improved = true;
            }

            r.kills++;
            if (wasRare) r.rareKills++;

            return improved;
        }

        /// <summary>Total de vitórias somando todos os bosses do álbum (progresso global da coleção).</summary>
        public static int TotalKills(List<BossRecord> list)
        {
            if (list == null) return 0;
            int total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) total += list[i].kills;
            }
            return total;
        }
    }
}
