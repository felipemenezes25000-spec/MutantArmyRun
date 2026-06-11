using System;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Áudio por EVENTO do bus (doc 12 §3.1): multiplicação de portal, fanfarra de Supply
    /// (CANON §3.2 — prêmio, nunca punição), mutação, fase de boss e fim de fase. Música
    /// por mundo entra via PlayMusic(WorldConfigSO.musicTrack) chamado pelo fluxo de fase.
    /// Stub do MVP: clips são opcionais (campos vazios são no-op) — os hooks e o gate de
    /// sfxOn/musicOn já são os definitivos. Preferências persistem via BindSaveState
    /// (SaveData é Domain — atravessa a fronteira Meta/Services).
    /// </summary>
    public class AudioManager : MonoBehaviour, IInitializable
    {
        public static AudioManager Instance { get; private set; }

        [Header("Fontes (no prefab [Services])")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("SFX por evento do bus")]
        [SerializeField] private AudioClip _gateConsumedClip;       // multiplicação/escolha de portal
        [SerializeField] private AudioClip _supplyFanfareClip;      // overflow de Supply (fanfarra!)
        [SerializeField] private AudioClip _mutationClip;
        [SerializeField] private AudioClip _bossPhaseClip;
        [SerializeField] private AudioClip _victoryClip;
        [SerializeField] private AudioClip _defeatClip;
        [SerializeField] private AudioClip _coinClip;

        private SaveData _save;
        private Action _markSaveDirty;
        private bool _sfxOn = true;
        private bool _musicOn = true;

        // Moedas chegam em rajada (commit, overflow): rate limit evita metralhadora de SFX.
        private const float CoinSfxMinInterval = 0.05f;
        private float _lastCoinSfxTime = -1f;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable.</summary>
        public void Init()
        {
            Instance = this;
            // Preferências (sfxOn/musicOn) vêm do save vivo do blackboard (Save roda antes
            // na ordem §3.3); sem bootstrap (teste), o bind fica a cargo do teste.
            if (GameBootstrap.Current != null)
                BindSaveState(GameBootstrap.Current.Save, GameBootstrap.Current.MarkSaveDirty);
            // -= antes de += : Init repetido (re-bootstrap em teste) não duplica inscrição.
            Unsubscribe();
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
            GameEvents.OnMutationGained += HandleMutationGained;
            GameEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnCurrencyChanged += HandleCurrencyChanged;
        }

        private void OnDestroy()
        {
            Unsubscribe();   // bus estático sobrevive a cenas — sempre limpar (doc 12 §3.2)
        }

        private void Unsubscribe()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnMutationGained -= HandleMutationGained;
            GameEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnCurrencyChanged -= HandleCurrencyChanged;
        }

        /// <summary>Liga as preferências ao save (sfxOn/musicOn) — mesma instância viva do SaveSystem.</summary>
        public void BindSaveState(SaveData saveData, Action markDirty)
        {
            _save = saveData;
            _markSaveDirty = markDirty;
            if (_save != null)
            {
                _sfxOn = _save.sfxOn;
                _musicOn = _save.musicOn;
            }
            ApplyMusicMute();
        }

        public bool SfxOn => _sfxOn;
        public bool MusicOn => _musicOn;

        public void SetSfxOn(bool on)
        {
            _sfxOn = on;
            if (_save != null)
            {
                _save.sfxOn = on;
                if (_markSaveDirty != null) _markSaveDirty();
            }
        }

        public void SetMusicOn(bool on)
        {
            _musicOn = on;
            if (_save != null)
            {
                _save.musicOn = on;
                if (_markSaveDirty != null) _markSaveDirty();
            }
            ApplyMusicMute();
        }

        /// <summary>Música por mundo (doc 12 §3.1): o fluxo de fase passa WorldConfigSO.musicTrack.</summary>
        public void PlayMusic(AudioClip track)
        {
            if (_musicSource == null || track == null) return;
            if (_musicSource.clip == track && _musicSource.isPlaying) return;
            _musicSource.clip = track;
            _musicSource.loop = true;
            _musicSource.mute = !_musicOn;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null) _musicSource.Stop();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (!_sfxOn || _sfxSource == null || clip == null) return;
            _sfxSource.PlayOneShot(clip);
        }

        private void ApplyMusicMute()
        {
            if (_musicSource != null) _musicSource.mute = !_musicOn;
        }

        // ---- Handlers do bus (payload pode ser ignorado — reação é cosmética) ----

        private void HandleGateConsumed(GateResult r) => PlaySfx(_gateConsumedClip);

        private void HandleSupplyOverflow(SupplyOverflow o) => PlaySfx(_supplyFanfareClip);

        private void HandleMutationGained(MutationConfigSO m) => PlaySfx(_mutationClip);

        private void HandleBossPhaseChanged(BossPhase p) => PlaySfx(_bossPhaseClip);

        private void HandleLevelFinished(LevelResult r) => PlaySfx(r.won ? _victoryClip : _defeatClip);

        private void HandleCurrencyChanged(CurrencyChange c)
        {
            if (c.type != CurrencyType.Coin || c.amount <= 0) return;
            if (Time.unscaledTime - _lastCoinSfxTime < CoinSfxMinInterval) return;
            _lastCoinSfxTime = Time.unscaledTime;
            PlaySfx(_coinClip);
        }
    }
}
