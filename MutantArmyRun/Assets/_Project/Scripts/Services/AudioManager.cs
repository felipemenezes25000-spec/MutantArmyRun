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
    /// por mundo toca em loop e troca sozinha ao mudar de mundo/fase: o manager assina
    /// GameManager.StateEntered e, ao entrar em Running, resolve a faixa do mundo da fase
    /// atual (catálogo musicByWorld[worldIndex] → WorldConfigSO.musicTrack → no-op). Mesma
    /// regra de fronteira do WorldAtmosphereApplier e do AnalyticsManager: Services enxerga
    /// Core (§2.3), então é aqui que o evento é assinado. PlayMusic(clip) segue público para
    /// quem queira forçar uma faixa. Clips são opcionais (campos vazios são no-op silencioso) —
    /// o jogo roda com qualquer subconjunto. Preferências persistem via BindSaveState
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

        // Crescimento da multidão (OnCrowdChanged): cada salto positivo solta um pop, mas a
        // multidão muda em rajada (multiplicação 1→N) — rate-limit evita metralhadora.
        private const float CrowdPopMinInterval = 0.06f;
        private float _lastCrowdPopTime = -1f;
        private int _lastCrowdCount = -1;

        // Música por mundo: troca SÓ quando o worldIndex muda (mudar de fase no mesmo mundo
        // não reinicia a faixa). -1 = nenhuma faixa tocando ainda.
        private int _currentMusicWorld = -1;

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
            GameEvents.OnCrowdChanged += HandleCrowdChanged;
            GameEvents.OnMutationGained += HandleMutationGained;
            GameEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnCurrencyChanged += HandleCurrencyChanged;
            JuiceEvents.OnBossHitPulse += HandleBossHitPulse;

            // Música por mundo (Services enxerga Core §2.3, igual ao AnalyticsManager): assina
            // a entrada em Running para resolver a faixa do mundo da fase atual.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateEntered += HandleStateEntered;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();   // bus estático sobrevive a cenas — sempre limpar (doc 12 §3.2)
        }

        private void Unsubscribe()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnCrowdChanged -= HandleCrowdChanged;
            GameEvents.OnMutationGained -= HandleMutationGained;
            GameEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnCurrencyChanged -= HandleCurrencyChanged;
            JuiceEvents.OnBossHitPulse -= HandleBossHitPulse;
            if (GameManager.Instance != null)
                GameManager.Instance.StateEntered -= HandleStateEntered;
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

        /// <summary>
        /// Música por mundo (doc 12 §3.1): toca a faixa em loop. Chamada direta (fora do fluxo
        /// de mundo) reseta o índice de mundo para que a próxima troca automática reavalie.
        /// </summary>
        public void PlayMusic(AudioClip track)
        {
            _currentMusicWorld = -1;   // faixa forçada manualmente — desliga o cache de mundo
            PlayMusicTrack(track);
        }

        // Troca a faixa SÓ se for diferente da que já está tocando (não corta o loop atual).
        private void PlayMusicTrack(AudioClip track)
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
            _currentMusicWorld = -1;
            if (_musicSource != null) _musicSource.Stop();
        }

        // ---- Música por mundo: resolvida na entrada em Running (troca ao mudar de mundo) ----

        private void HandleStateEntered(GameState state)
        {
            // Só na entrada da corrida; BossFight herda a faixa (mesma fase/mundo).
            if (state != GameState.Running) return;
            _lastCrowdCount = -1;   // nova corrida: não soa pop pela diferença com a fase anterior
            UpdateWorldMusic();
        }

        /// <summary>
        /// Resolve e toca a música do mundo da fase atual. Precedência: catálogo
        /// musicByWorld[worldIndex] → WorldConfigSO.musicTrack → no-op silencioso. Troca a
        /// faixa apenas quando o worldIndex muda (mudar de fase no mesmo mundo não reinicia).
        /// </summary>
        public void UpdateWorldMusic()
        {
            LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
            WorldConfigSO world = level != null ? level.world : null;
            if (world == null) return;

            if (world.worldIndex == _currentMusicWorld && _musicSource != null && _musicSource.isPlaying)
                return;   // mesmo mundo, já tocando — não corta o loop

            AudioClip track = _catalog != null ? _catalog.MusicForWorld(world.worldIndex) : null;
            if (track == null) track = world.musicTrack;   // fallback do próprio mundo (Lacuna L7)
            if (track == null) return;                     // sem faixa: silêncio honesto

            _currentMusicWorld = world.worldIndex;
            PlayMusicTrack(track);
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

        // ---- Hooks cosméticos opcionais (gameplay chama; catálogo-driven, no-op se vazio) ----

        /// <summary>Passo agregado da multidão (pitch random ±5%) — chamado pelo loop de locomoção.</summary>
        public void PlayFootstep() => PlaySfxPitched(_catalog != null ? _catalog.footstep : null);

        /// <summary>Explosão (telegraph do boss / golpe final) — camada de peso, pitch random.</summary>
        public void PlayExplosion() => PlaySfxPitched(_catalog != null ? _catalog.explosion : null);

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

        private void HandleSupplyOverflow(SupplyOverflow o)
            => PlaySfx(FirstOf(_catalog != null ? _catalog.supplyFanfare : null, _supplyFanfareClip));

        private void HandleMutationGained(MutationConfigSO m)
            => PlaySfx(FirstOf(_catalog != null ? _catalog.mutation : null, _mutationClip));

        // Multidão cresce (OnCrowdChanged): cada salto positivo solta um pop com pitch random,
        // rate-limited (multiplicação 1→N dispara várias mudanças no mesmo frame). Queda
        // (perdas) não soa aqui — o feedback de dano é cosmético em outro lugar.
        private void HandleCrowdChanged(int count, int supplyUsed)
        {
            int previous = _lastCrowdCount;
            _lastCrowdCount = count;
            if (previous < 0 || count <= previous) return;   // primeira leitura ou não cresceu
            AudioClip pop = _catalog != null ? _catalog.pop : null;
            if (pop == null) return;
            if (Time.unscaledTime - _lastCrowdPopTime < CrowdPopMinInterval) return;
            _lastCrowdPopTime = Time.unscaledTime;
            PlaySfxPitched(pop);
        }

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
