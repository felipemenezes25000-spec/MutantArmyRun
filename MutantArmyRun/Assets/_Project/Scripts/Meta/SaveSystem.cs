using System;
using System.IO;
using System.Threading.Tasks;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Meta
{
    /// <summary>
    /// Persistência local-first (doc 12 §4.7). Quatro garantias de produção:
    /// (a) migração incremental por gates de versão (Domain.SaveMigration);
    /// (b) gravação ATÔMICA — escreve em .tmp, copia main→bak e renomeia; queda de energia
    ///     no meio do write nunca corrompe o save principal;
    /// (c) flush garantido em OnApplicationFocus(false), OnApplicationPause(true) e
    ///     OnApplicationQuit — os três, porque Android e iOS divergem em qual callback chega;
    /// (d) save assíncrono com dirty flag centralizado: durante a corrida só se chama
    ///     MarkDirty(); o I/O real acontece nas transições de estado, nunca a 60 fps.
    /// Checksum e formato do payload vivem no Domain (SaveChecksum); sync Firestore entra
    /// pós-MVP atrás de interface — nada aqui depende de rede.
    /// </summary>
    public class SaveSystem : MonoBehaviour, IInitializable
    {
        public static SaveSystem Instance { get; private set; }

        public SaveData Data { get; private set; }

        // Application.persistentDataPath só pode ser lido na MAIN thread; os caminhos são
        // cacheados no Init para o WriteAtomic poder rodar dentro de Task.Run (SaveAsync).
        private string _mainPath;
        private string _backupPath;
        private bool _dirty;

        // Save() síncrono (callbacks de saída do app) e SaveAsync() (transições de estado)
        // podem se sobrepor: o lock serializa a sequência tmp→bak→rename entre threads.
        private static readonly object IoLock = new object();

        private string MainPath => _mainPath;
        private string BackupPath => _backupPath;

        /// <summary>Chamado PRIMEIRO pelo GameBootstrap (doc 12 §3.3) — síncrono, nunca depende de rede.</summary>
        public void Init()
        {
            Instance = this;
            _mainPath = Path.Combine(Application.persistentDataPath, "save.json");
            _backupPath = Path.Combine(Application.persistentDataPath, "save.bak");
            Load();
            Data.sessionCount++;            // cada boot conta uma sessão (doc 12 §4.7 — retenção)
            MarkDirty();

            // Publica no blackboard do Core o que os SDKs precisam ANTES de inicializar
            // (contrato do GameBootstrap §3.3): Core não conhece este tipo concreto.
            if (GameBootstrap.Current != null)
            {
                GameBootstrap.Current.AdsRemoved = Data.adsRemoved;
                GameBootstrap.Current.ConsentStatus = Data.consentStatus;
                // Instância viva + dirty flag para Ads/IAP/Audio fazerem BindSaveState no
                // próprio Init (doc 12 §4.8) — Services não enxerga Meta, SaveData é Domain.
                GameBootstrap.Current.Save = Data;
                GameBootstrap.Current.MarkSaveDirty = MarkDirty;
            }

            // Auto-wiring nos hooks do GameManager (doc 12 §4.1): Meta enxerga Core, nunca
            // o inverso — por isso é o manager que se liga, não o bootstrap.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelEndRecorder = RecordLevelEnd;            // save imediato pós-fase
                GameManager.Instance.ReviveAlreadyUsed = () => Data.usedReviveThisLevel;
                // += de propósito: o hook é compartilhado com o CrowdManager (Gameplay);
                // atribuição com "=" apagaria o handler do outro assinante.
                GameManager.Instance.ReviveCrowd += MarkReviveUsed;
            }
        }

        /// <summary>Revive aceito (CANON §11: 1×/fase) — persiste o uso antes da fase seguir.</summary>
        private void MarkReviveUsed()
        {
            if (Data == null) return;
            Data.usedReviveThisLevel = true;
            MarkDirty();
        }

        /// <summary>Main → backup → save novo; qualquer leitura inválida cai para o próximo fallback.</summary>
        public void Load()
        {
            SaveData loaded = TryLoad(MainPath);
            if (loaded == null) loaded = TryLoad(BackupPath);
            if (loaded == null)
            {
                loaded = new SaveData
                {
                    firstLaunchUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    schemaVersion = SaveMigration.CurrentVersion
                };
            }
            SaveMigration.Migrate(loaded);  // schemaVersion antigo atravessa TODOS os gates até o atual
            Data = loaded;
        }

        /// <summary>Checksum inválido/arquivo corrompido retorna null — o chamador decide o fallback.</summary>
        private SaveData TryLoad(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                string raw = File.ReadAllText(path);
                string json;
                if (!SaveChecksum.TryUnpack(raw, out json)) return null;   // adulterado/corrompido → backup
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Falha lendo '{path}': {e.Message} — usando fallback.");
                return null;
            }
        }

        /// <summary>
        /// Save SÍNCRONO — reservado aos callbacks de saída do app (flush triplo abaixo).
        /// Nas transições de estado, usar SaveAsync(); na corrida, apenas MarkDirty().
        /// </summary>
        public void Save()
        {
            if (Data == null) return;
            Data.lastSaveUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = JsonUtility.ToJson(Data);            // snapshot serializado na main thread
            string payload = SaveChecksum.Pack(json);
            _dirty = false;
            WriteAtomic(payload, MainPath, BackupPath);
        }

        /// <summary>
        /// Save assíncrono (doc 12 §4.7): o snapshot é serializado NA main thread (JsonUtility
        /// e o estado do jogo não são thread-safe) e só o I/O vai para a Task.
        /// </summary>
        public Task SaveAsync()
        {
            if (Data == null) return Task.CompletedTask;
            Data.lastSaveUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = JsonUtility.ToJson(Data);
            string payload = SaveChecksum.Pack(json);
            _dirty = false;
            string main = MainPath;
            string backup = BackupPath;
            return Task.Run(() => WriteAtomic(payload, main, backup));
        }

        /// <summary>ÚNICO ponto de "preciso salvar"; o flush é centralizado nas transições de estado.</summary>
        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// Fim de fase (doc 12 §4.1 ResolveEnd → §4.7): atualiza records, streaks e os campos
        /// de pacing de ads, e dispara o save imediato pós-fase.
        /// </summary>
        public void RecordLevelEnd(LevelResult r)
        {
            if (Data == null) return;

            LevelRecord record = Data.levelRecords.Find(x => x.levelIndex == r.levelIndex);
            if (record == null)
            {
                record = new LevelRecord { levelIndex = r.levelIndex };
                Data.levelRecords.Add(record);
            }

            if (r.won)
            {
                record.won = true;
                if (r.survivors > record.bestSurvivors) record.bestSurvivors = r.survivors;
                if (record.bestTime <= 0f || (r.durationSeconds > 0f && r.durationSeconds < record.bestTime))
                    record.bestTime = r.durationSeconds;
                if (r.levelIndex > Data.highestLevelCleared) Data.highestLevelCleared = r.levelIndex;
                Data.consecutiveDefeats = 0;
            }
            else
            {
                Data.consecutiveDefeats++;
            }

            Data.levelsSinceInterstitial++;     // pacing do CANON §11 — zerado pelo AdsManager ao exibir
            Data.usedReviveThisLevel = false;   // revive é 1×/fase: a próxima fase nasce elegível

            // Fire-and-forget intencional: o WriteAtomic engole exceções de I/O com log próprio.
            SaveAsync();
        }

        // ---- Flush triplo (doc 12 §4.7): Android/iOS divergem em qual callback chega,
        //      e em swipe-kill só alguns chegam — por isso os TRÊS. ----
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _dirty) Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _dirty) Save();
        }

        private void OnApplicationQuit()
        {
            if (_dirty) Save();
        }

        /// <summary>
        /// Sequência atômica tmp→bak→rename: se o app morrer entre quaisquer passos, ou o
        /// main antigo ou o tmp completo sobrevive — nunca um arquivo meio-escrito sem backup.
        /// </summary>
        private static void WriteAtomic(string payload, string mainPath, string backupPath)
        {
            lock (IoLock)
            {
                try
                {
                    string tmp = mainPath + ".tmp";
                    File.WriteAllText(tmp, payload);
                    if (File.Exists(mainPath)) File.Copy(mainPath, backupPath, overwrite: true);
                    File.Delete(mainPath);
                    File.Move(tmp, mainPath);
                }
                catch (Exception e)
                {
                    // Nunca propagar para os callbacks de saída do app: save falho mantém
                    // o arquivo anterior íntegro (a sequência só renomeia no fim).
                    Debug.LogError($"[SaveSystem] Falha gravando save: {e.Message}");
                }
            }
        }
    }
}
