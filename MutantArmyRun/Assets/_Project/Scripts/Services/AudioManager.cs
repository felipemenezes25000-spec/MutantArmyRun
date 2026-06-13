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
    /// Core (§2.3), então é aqui que o evento é assinado. Ao entrar em MainMenu, toca a música
    /// de menu (catálogo.menuMusic). PlayMusic(clip) segue público para quem queira forçar uma
    /// faixa. Clips são opcionais (campos vazios são no-op silencioso) — o jogo roda com qualquer
    /// subconjunto. Preferências persistem via BindSaveState (SaveData é Domain — atravessa a
    /// fronteira Meta/Services).
    ///
    /// MIXAGEM (missão Nota 10): música em volume mais BAIXO que os SFX (padrão de runner — o
    /// punch vem do SFX), crossfade ao trocar de faixa (suaviza a emenda entre jingles curtos —
    /// ver teto da Lacuna L7) e DUCKING: SFX grandes (morte de boss, cascata de combos, fanfarra
    /// de Supply, fim de fase, chuva de moedas, risco vencido) mergulham a música por um instante
    /// para o impacto respirar. Todo fade/duck roda em TEMPO REAL (atravessa o slow motion da
    /// morte cinematográfica e o timeScale 8-10x dos testes intacto).
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

        // Impacto de obstáculo da pista: obstáculos podem cair em sequência — rate-limit evita
        // metralhadora de explosões (mesma filosofia do hit no boss).
        private const float ObstacleSfxMinInterval = 0.12f;
        private float _lastObstacleSfxTime = -1f;

        // Crescimento da multidão (OnCrowdChanged): cada salto positivo solta um pop, mas a
        // multidão muda em rajada (multiplicação 1→N) — rate-limit evita metralhadora.
        private const float CrowdPopMinInterval = 0.06f;
        private float _lastCrowdPopTime = -1f;
        private int _lastCrowdCount = -1;

        // Golpe elemental classificado (FRAQUEZA!/RESISTIU!): já é rate-limited ≥0,5 s na
        // origem (CONTRACT §5) — segundo guarda aqui por robustez, padrão do bossHit.
        private const float ElementalSfxMinInterval = 0.3f;
        private float _lastElementalSfxTime = -1f;

        // Inimigos de pista morrem em rajada (horda inteira no mesmo segmento): mesmo
        // tratamento do pop de multidão — rate-limit curto, nunca metralhadora.
        private const float EnemyPopMinInterval = 0.06f;
        private float _lastEnemyPopTime = -1f;

        // Veredito de portal (BOA ESCOLHA!/armadilha): 1 por par de portais, mas o guarda
        // protege contra exércitos divididos consumindo 2 portais quase juntos.
        private const float ChoiceSfxMinInterval = 0.1f;
        private float _lastChoiceSfxTime = -1f;

        // Telegraph do especial: 1 por golpe na origem; guarda evita alarme duplo em re-Raise.
        private const float WarningSfxMinInterval = 0.4f;
        private float _lastWarningSfxTime = -1f;

        // Combos chegam TODOS no mesmo frame (avaliados na morte do boss, CONTRACT §5):
        // cascata staggered (padrão PopCascadeRoutine) em vez de rate-limit que engoliria
        // os stings — cada combo conquistado soa, um após o outro.
        private const float ComboStingStagger = 0.16f;
        private int _pendingComboStings;
        private Coroutine _comboCascade;

        // Música por mundo: troca SÓ quando o worldIndex muda (mudar de fase no mesmo mundo
        // não reinicia a faixa). -1 = nenhuma faixa tocando ainda. Sentinela do menu = -2
        // (não é mundo nenhum, mas distingue "menu tocando" de "nada tocando" no cache).
        private const int MenuMusicWorld = -2;
        private int _currentMusicWorld = -1;

        // ---- Mixagem (música mais BAIXA que SFX, padrão de runner: SFX dão o punch) ----
        // A música é ambiente; os SFX é que "tocam o jogo". Volumes relativos sensatos para
        // os jingles CC0 (que são brilhantes/cortantes) não cansarem por cima do gameplay.
        private const float MusicBaseVolume = 0.42f;   // teto da música quando sem ducking
        private const float SfxVolume = 1.0f;          // SFX em cheio (one-shots já mixados)
        private const float MusicFadeSeconds = 0.6f;   // fade ao trocar de faixa (suaviza a emenda)

        // Ducking: SFX grandes (morte de boss, combos, fanfarra, fim de fase) abaixam a música
        // por um instante para o impacto "respirar", depois ela volta. Tempo REAL (atravessa o
        // slow motion da morte cinematográfica e o timeScale 8-10x dos testes intacto).
        private const float DuckVolume = 0.12f;        // quão fundo a música cai durante o duck
        private const float DuckAttackSeconds = 0.05f; // entra rápido (o impacto é AGORA)
        private const float DuckHoldSeconds = 0.18f;   // segura no fundo
        private const float DuckReleaseSeconds = 0.5f; // sobe devagar (volta natural)
        private Coroutine _musicFade;                  // fade de TROCA de faixa
        private Coroutine _musicDuck;                  // duck por SFX grande
        private bool _ducking;                         // duck em andamento (release reinicia o ciclo)

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
            // Mixagem: SFX em cheio, música ambiente mais baixa (padrão runner — o punch vem do
            // SFX). Idempotente: re-Init reaplica os mesmos volumes.
            if (_sfxSource != null) _sfxSource.volume = SfxVolume;
            if (_pitchedSource != null) _pitchedSource.volume = SfxVolume;
            if (_musicSource != null) _musicSource.volume = MusicBaseVolume;
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
            JuiceEvents.OnObstacleHit += HandleObstacleHit;
            // Eventos da missão Nota 10 (CONTRACT §5): boss elemental, combos, especiais,
            // inimigos de pista e vereditos de portal/risco — todos catálogo-driven (clip
            // vazio = no-op silencioso, jogo intacto sem os assets).
            GameEvents.OnBossElementalHit += HandleBossElementalHit;
            GameEvents.OnComboEarned += HandleComboEarned;
            GameEvents.OnBossSpecialWarning += HandleBossSpecialWarning;
            GameEvents.OnBossDied += HandleBossDied;
            GameEvents.OnTrackEnemyKilled += HandleTrackEnemyKilled;
            GameEvents.OnEnemyWaveCleared += HandleEnemyWaveCleared;
            JuiceEvents.OnGoodGateChoice += HandleGoodGateChoice;
            JuiceEvents.OnBadGateChoice += HandleBadGateChoice;
            JuiceEvents.OnRiskResolved += HandleRiskResolved;

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
            JuiceEvents.OnObstacleHit -= HandleObstacleHit;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnBossSpecialWarning -= HandleBossSpecialWarning;
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            GameEvents.OnEnemyWaveCleared -= HandleEnemyWaveCleared;
            JuiceEvents.OnGoodGateChoice -= HandleGoodGateChoice;
            JuiceEvents.OnBadGateChoice -= HandleBadGateChoice;
            JuiceEvents.OnRiskResolved -= HandleRiskResolved;
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
        // Crossfade simples (não hard-cut): abaixa o volume da faixa atual, troca o clip e sobe
        // de novo — a emenda entre jingles curtos fica menos abrupta. Sem cena ativa (teste sem
        // GameObject habilitado), degrada para a troca direta (corrotina exige isActiveAndEnabled).
        private void PlayMusicTrack(AudioClip track)
        {
            if (_musicSource == null || track == null) return;
            if (_musicSource.clip == track && _musicSource.isPlaying) return;

            if (!isActiveAndEnabled)
            {
                SwapMusicClip(track);   // teste/headless: troca seca, sem corrotina de fade
                return;
            }
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(CrossfadeRoutine(track));
        }

        // Swap direto do clip mantendo o teto de volume atual (respeita um duck em andamento).
        private void SwapMusicClip(AudioClip track)
        {
            _musicSource.clip = track;
            _musicSource.loop = true;
            _musicSource.mute = !_musicOn;
            _musicSource.volume = _ducking ? DuckVolume : MusicBaseVolume;
            _musicSource.Play();
        }

        private IEnumerator CrossfadeRoutine(AudioClip track)
        {
            // Fade-out da faixa atual (se houver), troca, fade-in. Tudo em tempo REAL (atravessa
            // slow motion e timeScale dos testes). O teto do fade-in respeita um duck ativo.
            float startVol = _musicSource.volume;
            if (_musicSource.isPlaying && _musicSource.clip != null)
            {
                float t = 0f;
                while (t < MusicFadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    _musicSource.volume = Mathf.Lerp(startVol, 0f, t / MusicFadeSeconds);
                    yield return null;
                }
            }
            SwapMusicClip(track);
            // Teto do fade-in respeita um duck que iniciou ANTES do crossfade; mas se um duck
            // começar DURANTE este fade, ele assume o volume — não sobrescrevemos no fim.
            float ceiling = _ducking ? DuckVolume : MusicBaseVolume;
            _musicSource.volume = 0f;
            float u = 0f;
            while (u < MusicFadeSeconds)
            {
                if (_musicDuck != null) break;   // duck assumiu o controle do volume
                u += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(0f, ceiling, u / MusicFadeSeconds);
                yield return null;
            }
            if (_musicDuck == null) _musicSource.volume = ceiling;
            _musicFade = null;
        }

        public void StopMusic()
        {
            _currentMusicWorld = -1;
            if (_musicFade != null) { StopCoroutine(_musicFade); _musicFade = null; }
            if (_musicSource != null) { _musicSource.Stop(); _musicSource.volume = MusicBaseVolume; }
        }

        // ---- Música por mundo: resolvida na entrada em Running (troca ao mudar de mundo) ----

        private void HandleStateEntered(GameState state)
        {
            // Menu/meta: música calma de fundo (jingle CC0 em loop — ver pendência da Lacuna L7).
            // Volta ao menu depois da corrida REINICIA a faixa de menu (sai de mundo p/ menu).
            if (state == GameState.MainMenu)
            {
                PlayMenuMusic();
                return;
            }
            // Só na entrada da corrida; BossFight herda a faixa (mesma fase/mundo).
            if (state != GameState.Running) return;
            _lastCrowdCount = -1;   // nova corrida: não soa pop pela diferença com a fase anterior
            UpdateWorldMusic();
        }

        /// <summary>
        /// Música de fundo das telas de menu/meta. Troca SÓ quando saímos de uma faixa de mundo
        /// para o menu (cache <see cref="MenuMusicWorld"/>) — reentrar no menu não corta o loop.
        /// </summary>
        public void PlayMenuMusic()
        {
            AudioClip track = _catalog != null ? _catalog.menuMusic : null;
            if (track == null) return;   // sem faixa de menu: silêncio honesto
            if (_currentMusicWorld == MenuMusicWorld && _musicSource != null && _musicSource.isPlaying)
                return;
            _currentMusicWorld = MenuMusicWorld;
            PlayMusicTrack(track);
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

        // ---- Ducking: SFX grande abaixa a música por um instante (impacto "respira") ----

        /// <summary>
        /// Mergulha a música por um instante para um SFX grande (morte de boss, combos, fanfarra,
        /// fim de fase) ganhar destaque, depois ela volta. Tempo REAL — atravessa o slow motion da
        /// morte cinematográfica e o timeScale 8-10x dos testes. No-op sem música ou fora de cena.
        /// </summary>
        private void DuckMusic()
        {
            if (_musicSource == null || !_musicOn || !isActiveAndEnabled) return;
            if (!_musicSource.isPlaying) return;
            // O duck SEMPRE roda sua própria corrotina (dono único do volume durante a janela).
            // Se um crossfade estiver em andamento, ele detecta _musicDuck != null e cede o
            // controle — nunca duas corrotinas brigando pelo mesmo volume.
            if (_musicDuck != null) StopCoroutine(_musicDuck);
            _musicDuck = StartCoroutine(DuckRoutine());
        }

        private IEnumerator DuckRoutine()
        {
            _ducking = true;
            float from = _musicSource.volume;
            // Ataque rápido até o fundo do duck.
            float t = 0f;
            while (t < DuckAttackSeconds)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(from, DuckVolume, t / DuckAttackSeconds);
                yield return null;
            }
            _musicSource.volume = DuckVolume;
            yield return new WaitForSecondsRealtime(DuckHoldSeconds);
            // Release lento de volta ao teto base.
            float r = 0f;
            while (r < DuckReleaseSeconds)
            {
                r += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(DuckVolume, MusicBaseVolume, r / DuckReleaseSeconds);
                yield return null;
            }
            _musicSource.volume = MusicBaseVolume;
            _ducking = false;
            _musicDuck = null;
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
        {
            PlaySfx(FirstOf(_catalog != null ? _catalog.supplyFanfare : null, _supplyFanfareClip));
            DuckMusic();   // fanfarra de prêmio (CANON §3.2) ganha o palco por um instante
        }

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
            DuckMusic();   // jingle de fim de fase no primeiro plano (vitória/derrota)
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

        // Impacto de obstáculo da pista (doc 12 §4.11): camada de peso (explosão), pitch random,
        // rate-limited. PlayExplosion é no-op silencioso se o catálogo não tem o clip.
        private void HandleObstacleHit(Vector3 worldPosition)
        {
            if (Time.unscaledTime - _lastObstacleSfxTime < ObstacleSfxMinInterval) return;
            _lastObstacleSfxTime = Time.unscaledTime;
            PlayExplosion();
        }

        // ---- Handlers da missão Nota 10 (CONTRACT §5) ----

        // Golpe elemental classificado: FRAQUEZA! soa brilhante, RESISTIU!/IMUNE! soa seco.
        // Neutral fica de fora — o pulso genérico (OnBossHitPulse) já cobre o hit comum.
        // O JuiceController re-publica BossHitPulse DESTE MESMO hit (origens mutuamente
        // exclusivas, polling vs evento): ao tocar o SFX dedicado, o carimbo em
        // _lastBossHitSfxTime faz o rate-limit do bossHit genérico engolir o eco — nunca
        // 2 SFX no mesmo golpe. Sem clip dedicado, o genérico segue valendo (fallback).
        private void HandleBossElementalHit(BossElementalHit hit)
        {
            AudioClip clip = null;
            if (_catalog != null)
            {
                if (hit.relation == ElementRelation.Weakness) clip = _catalog.weaknessHit;
                else if (hit.relation == ElementRelation.Resisted || hit.relation == ElementRelation.Immune)
                    clip = _catalog.resistedHit;
            }
            if (clip == null) return;
            // Ordem invertida (re-Init em teste): se o bossHit genérico JÁ soou neste exato
            // frame, este hit já foi sonorizado — não empilha o dedicado por cima.
            if (Mathf.Approximately(_lastBossHitSfxTime, Time.unscaledTime)) return;
            if (Time.unscaledTime - _lastElementalSfxTime < ElementalSfxMinInterval) return;
            _lastElementalSfxTime = Time.unscaledTime;
            _lastBossHitSfxTime = Time.unscaledTime;   // cala o bossHit genérico deste hit
            PlaySfxPitched(clip);
        }

        // Combos chegam todos no mesmo frame (morte do boss): enfileira e toca em cascata
        // staggered — cada conquista soa, nenhuma é engolida (padrão PopCascadeRoutine).
        private void HandleComboEarned(ComboEarned combo)
        {
            if (_catalog == null || _catalog.comboSting == null) return;
            _pendingComboStings++;
            if (_comboCascade == null && isActiveAndEnabled)
            {
                _comboCascade = StartCoroutine(ComboCascadeRoutine());
                DuckMusic();   // 1× no início da cascata (não por sting — não pumpa a música)
            }
        }

        private IEnumerator ComboCascadeRoutine()
        {
            // Tempo REAL: a cascata atravessa o slow motion da morte cinematográfica intacta.
            while (_pendingComboStings > 0)
            {
                _pendingComboStings--;
                PlaySfxPitched(_catalog != null ? _catalog.comboSting : null);
                yield return new WaitForSecondsRealtime(ComboStingStagger);
            }
            _comboCascade = null;
        }

        // Telegraph do especial (janela de leitura ANTES do golpe, CANON §6): alarme SEM
        // pitch random — leitura clara é mais importante que variedade.
        private void HandleBossSpecialWarning(BossSpecialTelegraph t)
        {
            AudioClip clip = _catalog != null ? _catalog.specialWarning : null;
            if (clip == null) return;
            if (Time.unscaledTime - _lastWarningSfxTime < WarningSfxMinInterval) return;
            _lastWarningSfxTime = Time.unscaledTime;
            PlaySfx(clip);
        }

        // Morte do boss (1× por luta): camada de peso da sequência cinematográfica —
        // PlayOneShot não é afetado pelo slow motion do golpe final. Ducking forte: a música
        // mergulha para a explosão + chuva de combos da morte cinematográfica respirarem.
        private void HandleBossDied(BossDied d)
        {
            PlaySfx(_catalog != null ? _catalog.bossDeath : null);
            DuckMusic();
        }

        // Inimigo de pista morto: pop com pitch random, rate-limited (hordas caem em rajada).
        private void HandleTrackEnemyKilled(TrackEnemyKilled k)
        {
            AudioClip clip = _catalog != null ? _catalog.enemyPop : null;
            if (clip == null) return;
            if (Time.unscaledTime - _lastEnemyPopTime < EnemyPopMinInterval) return;
            _lastEnemyPopTime = Time.unscaledTime;
            PlaySfxPitched(clip);
        }

        // Wave limpa: chuva de moedas — fanfarra curta, 1× por wave (sem rate-limit).
        private void HandleEnemyWaveCleared(EnemyWaveCleared w)
        {
            PlaySfx(_catalog != null ? _catalog.coinBurst : null);
            DuckMusic();   // chuva de moedas em destaque
        }

        // Veredito de portal (BOA ESCOLHA!/armadilha): sting CURTO em camada sobre o som do
        // portal (gatePositive/gateNegative continuam no OnGateConsumed — são o "whoosh",
        // este é o juízo). Mesmo guarda para os dois lados: nunca good+bad empilhados.
        private void HandleGoodGateChoice(Vector3 worldPosition)
            => PlayChoiceSfx(_catalog != null ? _catalog.goodChoice : null);

        private void HandleBadGateChoice(Vector3 worldPosition)
            => PlayChoiceSfx(_catalog != null ? _catalog.badChoice : null);

        private void PlayChoiceSfx(AudioClip clip)
        {
            if (clip == null) return;
            if (Time.unscaledTime - _lastChoiceSfxTime < ChoiceSfxMinInterval) return;
            _lastChoiceSfxTime = Time.unscaledTime;
            PlaySfxPitched(clip);
        }

        // Zona de risco: sucesso = fanfarra "x10!", falha = impacto seco (1× por zona).
        private void HandleRiskResolved(bool success, Vector3 worldPosition)
        {
            PlaySfx(_catalog != null ? (success ? _catalog.riskWin : _catalog.riskLose) : null);
            if (success) DuckMusic();   // só o triunfo "x10!" ganha o palco; a falha some sozinha
        }
    }
}
