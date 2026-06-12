using System;
using System.Collections;
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

        [Header("SFX por evento do bus (legado — o catálogo abaixo tem precedência)")]
        [SerializeField] private AudioClip _gateConsumedClip;       // multiplicação/escolha de portal
        [SerializeField] private AudioClip _supplyFanfareClip;      // overflow de Supply (fanfarra!)
        [SerializeField] private AudioClip _mutationClip;
        [SerializeField] private AudioClip _bossPhaseClip;
        [SerializeField] private AudioClip _victoryClip;
        [SerializeField] private AudioClip _defeatClip;
        [SerializeField] private AudioClip _coinClip;

        [Header("Catálogo de juice (preenchido pelo MAR Tools/Build Juice)")]
        [SerializeField] private AudioCatalogSO _catalog;

        private SaveData _save;
        private Action _markSaveDirty;
        private bool _sfxOn = true;
        private bool _musicOn = true;

        // Fonte dedicada aos one-shots com pitch random ±5% (repetitivos): mexer no pitch
        // do _sfxSource compartilhado distorceria one-shots ainda tocando nele.
        private AudioSource _pitchedSource;
        private const float PitchVariance = 0.05f;
        private Coroutine _popCascade;

        // Moedas chegam em rajada (commit, overflow): rate limit evita metralhadora de SFX.
        private const float CoinSfxMinInterval = 0.05f;
        private float _lastCoinSfxTime = -1f;

        // Pulso de hit no boss já é rate-limited na origem; segundo guarda aqui por robustez.
        private const float BossHitSfxMinInterval = 0.2f;
        private float _lastBossHitSfxTime = -1f;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable.</summary>
        public void Init()
        {
            Instance = this;
            // Fonte de pitch variado criada por código: o prefab [Services] segue intocado.
            if (_pitchedSource == null)
            {
                _pitchedSource = gameObject.AddComponent<AudioSource>();
                _pitchedSource.playOnAwake = false;
            }
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
            JuiceEvents.OnBossHitPulse += HandleBossHitPulse;
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
            JuiceEvents.OnBossHitPulse -= HandleBossHitPulse;
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

        /// <summary>One-shot com pitch random ±5% (repetitivos: moeda, pop, hit) na fonte dedicada.</summary>
        public void PlaySfxPitched(AudioClip clip)
        {
            if (!_sfxOn || clip == null) return;
            if (_pitchedSource == null)
            {
                PlaySfx(clip);   // Init ainda não rodou (teste): degrada para a fonte normal
                return;
            }
            _pitchedSource.pitch = 1f + UnityEngine.Random.Range(-PitchVariance, PitchVariance);
            _pitchedSource.PlayOneShot(clip);
        }

        // ---- UI (botões chamam via blackboard/glue — UI não referencia Services §2.3) ----

        public void PlayUiClick() => PlaySfx(_catalog != null ? _catalog.uiClick : null);

        public void PlayUiConfirm() => PlaySfx(_catalog != null ? _catalog.uiConfirm : null);

        private void ApplyMusicMute()
        {
            if (_musicSource != null) _musicSource.mute = !_musicOn;
        }

        /// <summary>Primeiro clip não-nulo: catálogo tem precedência, legado é fallback.</summary>
        private static AudioClip FirstOf(AudioClip preferred, AudioClip fallback)
        {
            return preferred != null ? preferred : fallback;
        }

        // ---- Handlers do bus (payload pode ser ignorado — reação é cosmética) ----

        private void HandleGateConsumed(GateResult r)
        {
            GateConfigSO gate = r.gate;
            bool negative = gate != null
                && ((gate.gateType == GateType.Multiply && gate.value < 1f)
                    || (gate.gateType == GateType.AddFlat && gate.value <= 0f));

            AudioClip catalogClip = _catalog != null
                ? (negative ? _catalog.gateNegative : _catalog.gatePositive)
                : null;
            PlaySfxPitched(FirstOf(catalogClip, _gateConsumedClip));

            // multiplicação ×2+: cascata de pep pops com stagger (espelha o juice visual)
            if (gate != null && gate.gateType == GateType.Multiply && gate.value >= 2f)
            {
                if (_popCascade != null) StopCoroutine(_popCascade);
                if (isActiveAndEnabled) _popCascade = StartCoroutine(PopCascadeRoutine());
            }
        }

        private IEnumerator PopCascadeRoutine()
        {
            AudioClip pop = _catalog != null ? _catalog.pop : null;
            if (pop != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    PlaySfxPitched(pop);
                    yield return new WaitForSecondsRealtime(0.07f);
                }
            }
            _popCascade = null;
        }

        private void HandleSupplyOverflow(SupplyOverflow o) => PlaySfx(_supplyFanfareClip);

        private void HandleMutationGained(MutationConfigSO m) => PlaySfx(_mutationClip);

        private void HandleBossPhaseChanged(BossPhase p)
        {
            PlaySfx(FirstOf(_catalog != null ? _catalog.bossRoar : null, _bossPhaseClip));
        }

        private void HandleLevelFinished(LevelResult r)
        {
            AudioClip jingle = r.won
                ? FirstOf(_catalog != null ? _catalog.victoryJingle : null, _victoryClip)
                : FirstOf(_catalog != null ? _catalog.defeatJingle : null, _defeatClip);
            PlaySfx(jingle);
        }

        private void HandleCurrencyChanged(CurrencyChange c)
        {
            if (c.type != CurrencyType.Coin || c.amount <= 0) return;
            if (Time.unscaledTime - _lastCoinSfxTime < CoinSfxMinInterval) return;
            _lastCoinSfxTime = Time.unscaledTime;
            PlaySfxPitched(FirstOf(_catalog != null ? _catalog.coin : null, _coinClip));
        }

        private void HandleBossHitPulse(Vector3 worldPosition)
        {
            if (_catalog == null || _catalog.bossHit == null) return;
            if (Time.unscaledTime - _lastBossHitSfxTime < BossHitSfxMinInterval) return;
            _lastBossHitSfxTime = Time.unscaledTime;
            PlaySfxPitched(_catalog.bossHit);
        }
    }
}
