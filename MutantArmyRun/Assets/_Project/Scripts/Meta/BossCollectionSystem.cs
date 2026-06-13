using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// Álbum de bosses (missão Nota 10 §4.3): recordes por boss persistidos em
    /// SaveData.bossCollection (schema v5). A matemática dos recordes vive no Domain
    /// (BossCollectionMath); este sistema só liga o bus ao save: OnBossDied →
    /// FindOrAdd + RegisterKill + MarkDirty. Gameplay não enxerga Meta (doc 12 §2.3),
    /// então TUDO chega por evento — inclusive as aproximações da luta corrente:
    /// · survivors ≈ último OnCrowdChanged (o CrowdManager é Gameplay, inacessível);
    /// · usedWeakness = houve OnBossElementalHit com relation Weakness na fase.
    /// RECOMPENSA: nenhuma aqui de propósito — a 1ª vitória (e todas as seguintes) já
    /// paga o killReward do boss via RewardSystem.HandleLevelFinished; conceder de novo
    /// pela coleção duplicaria o crédito (risco 3 do mapa meta-systems).
    ///
    /// CONTRATO DE API (telas futuras do álbum): Records · Find(bossId) · TotalKills ·
    /// eventos OnCollectionChanged / OnRecordImproved(bossId) ("NOVO RECORDE!").
    /// </summary>
    public class BossCollectionSystem : MonoBehaviour, IInitializable
    {
        public static BossCollectionSystem Instance { get; private set; }

        // Lista vazia compartilhada: leitura antes do SaveSystem nascer degrada sem alocar.
        private static readonly List<BossCollectionMath.BossRecord> EmptyRecords =
            new List<BossCollectionMath.BossRecord>();

        // ---- Aproximações da luta corrente (reset por fase em LevelStarted) ----
        private int _lastCrowdCount;        // último count do exército ≈ sobreviventes na morte do boss
        private int _weaknessHitsThisRun;   // golpes Weakness (rate-limited ≥0,5 s na origem)

        /// <summary>Disparado após registrar uma vitória no álbum (telas re-renderizam, doc 12 §3.2).</summary>
        public event Action OnCollectionChanged;

        /// <summary>Algum recorde do boss melhorou (tempo/sobreviventes/fraqueza) — toast "NOVO RECORDE!".</summary>
        public event Action<string> OnRecordImproved;

        /// <summary>
        /// Chamado pelo GameBootstrap via IInitializable, DEPOIS do SaveSystem (wiring Onda 4).
        /// Defensivo: tudo aqui degrada sem save/GameManager — nunca quebra o boot.
        /// </summary>
        public void Init()
        {
            Instance = this;

            // Bus estático sobrevive a cenas: -= antes de += (re-Init nunca duplica, doc 12 §3.2).
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnBossDied += HandleBossDied;
            GameEvents.OnCrowdChanged -= HandleCrowdChanged;
            GameEvents.OnCrowdChanged += HandleCrowdChanged;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnBossElementalHit += HandleBossElementalHit;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
                GameManager.Instance.LevelStarted += HandleLevelStarted;
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnCrowdChanged -= HandleCrowdChanged;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            if (GameManager.Instance != null)
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
        }

        // Soft reset por fase (doc 12 §4.11): retry/próxima fase zeram as aproximações da luta.
        private void HandleLevelStarted(int levelIndex)
        {
            _weaknessHitsThisRun = 0;
            _lastCrowdCount = 0;
        }

        private void HandleCrowdChanged(int count, int supplyUsed)
        {
            _lastCrowdCount = count;
        }

        private void HandleBossElementalHit(BossElementalHit hit)
        {
            if (hit.relation == ElementRelation.Weakness) _weaknessHitsThisRun++;
        }

        // OnBossDied dispara na morte, ANTES do ChangeState(Victory) (contrato §5) — o save
        // só é marcado dirty; o flush real acontece no RecordLevelEnd do fim de fase.
        private void HandleBossDied(BossDied died)
        {
            if (SaveSystem.Instance == null) return;
            SaveData d = SaveSystem.Instance.Data;
            if (d == null) return;

            // Defensivo: a migração v5 normaliza null, mas um save adulterado nunca derruba o registro.
            if (d.bossCollection == null) d.bossCollection = new List<BossCollectionMath.BossRecord>();

            BossCollectionMath.BossRecord record = BossCollectionMath.FindOrAdd(d.bossCollection, died.bossId);
            bool improved = BossCollectionMath.RegisterKill(
                record,
                died.fightSeconds,
                _lastCrowdCount,                // aproximação: último OnCrowdChanged ≈ sobreviventes
                _weaknessHitsThisRun > 0,       // explorou a fraqueza em algum momento da fase
                died.wasRare);

            SaveSystem.Instance.MarkDirty();    // padrão do funil de save: dirty agora, I/O na transição
            OnCollectionChanged?.Invoke();
            if (improved) OnRecordImproved?.Invoke(record.bossId);
        }

        // ==================================================================
        // CONTRATO DE API — leitura (tela de álbum futura)
        // ==================================================================

        /// <summary>Todos os recordes do álbum (a lista viva do save — tratar como somente-leitura).</summary>
        public IReadOnlyList<BossCollectionMath.BossRecord> Records
        {
            get
            {
                if (SaveSystem.Instance == null || SaveSystem.Instance.Data == null
                    || SaveSystem.Instance.Data.bossCollection == null)
                    return EmptyRecords;
                return SaveSystem.Instance.Data.bossCollection;
            }
        }

        /// <summary>Recorde de UM boss; null se nunca venceu (a tela mostra a silhueta "???").</summary>
        public BossCollectionMath.BossRecord Find(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return null;
            IReadOnlyList<BossCollectionMath.BossRecord> list = Records;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].bossId == bossId) return list[i];
            }
            return null;
        }

        /// <summary>Total de vitórias somando todos os bosses (progresso global da coleção).</summary>
        public int TotalKills
        {
            get
            {
                if (SaveSystem.Instance == null || SaveSystem.Instance.Data == null) return 0;
                return BossCollectionMath.TotalKills(SaveSystem.Instance.Data.bossCollection);
            }
        }
    }
}
