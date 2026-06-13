using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Pool;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Spawn e luta do boss (doc 12 §4.5). Três decisões do contrato:
    /// 1. Waves da arena são DADOS ordenados por tempo, consumidos por PONTEIRO de próximo
    ///    evento (Domain.WavePointer) — cada evento dispara exatamente 1×, nunca polling.
    /// 2. Telegraph e cooldown são Countdown puros do Domain — testáveis com dt sintético.
    /// 3. Entrada na arena = handoff sim→cinemática (CrowdManager.EnterArenaFormation).
    /// Fases de vida via Domain.CombatMath.BossPhase; morte com slow motion canônico.
    ///
    /// Missão Nota 10 (W2-A): bosses deixam de ser "números num asset" — um BossBehavior
    /// modular vive na VIEW (autorado no prefab ou resolvido pelo BossBehaviorRegistry por
    /// bossId) e recebe os 7 hooks da missão nos pontos certos deste loop; a morte vira uma
    /// SEQUÊNCIA cinematográfica de ~1,2 s (estado permanece BossFight; BossFight→Victory
    /// continua sendo a única transição legal); variante RARA rolada no BossScout com RNG
    /// derivado da seed (contrato §1.6).
    /// </summary>
    public class BossManager : MonoBehaviour, IInitializable
    {
        public static BossManager Instance { get; private set; }

        public BossRuntime Current { get; private set; }

        /// <summary>Behavior modular da luta ATUAL (null fora da luta) — CombatSystem chama os hooks elementais.</summary>
        public BossBehavior CurrentBehavior { get; private set; }

        /// <summary>Resumo da ÚLTIMA luta (vitória OU derrota) — sobrevive ao fim da luta (contrato §5: ComboSystem/álbum).</summary>
        public BossFightSummary LastFight { get; private set; }

        /// <summary>Rolado no StateEntered(BossScout) com RNG derivado (seed×48611+3); consumido no BeginFight.</summary>
        public bool NextFightRare { get; private set; }

        /// <summary>O especial do boss conectou nesta luta — alimenta o fail reason HitByLaser (FailReasonResolver).</summary>
        public bool UsedLaserThisFight { get; set; }

        /// <summary>Último veredito elemental registrado na luta (Neutral fora dela) — o CrowdManager
        /// usa no DefeatContext do wipe (o exército morto não tem mais DPS para reclassificar).</summary>
        public ElementRelation LastElementalRelation { get; private set; } = ElementRelation.Neutral;

        // limiares canônicos das 3 fases (doc 05 §2.3); Domain.CombatMath.BossPhase usa os mesmos.
        // Público: a barra segmentada do HUD (doc 09 §4.3) lê daqui.
        public static readonly float[] PhaseThresholds = { 0.5f, 0.25f };

        // chance default de boss raro (missão §4.2): 6% — tuning futuro via RC chega por parâmetro
        [SerializeField] private float _rareBossChance = 0.06f;

        private readonly Countdown _specialCooldown = new Countdown();   // Domain
        private readonly Countdown _telegraph = new Countdown();         // Domain
        private bool _telegraphing;
        private int _nextWaveEvent;   // ponteiro: nunca varre a lista (doc 12 §4.5)
        private ArenaWave[] _domainWaves = Array.Empty<ArenaWave>();

        // Morte cinematográfica (missão Nota 10): janela entre Die() e ChangeState(Victory).
        // Tickada em tempo UNSCALED no Update mesmo com Current==null — o slow motion do golpe
        // final (0,3×) não pode alongar a própria sequência.
        private const float DeathSequenceSeconds = 1.2f;
        private readonly Countdown _deathSequence = new Countdown();     // Domain
        private bool _deathSequenceActive;
        private float _lastBossHpRaised = 1f;   // último HP normalizado publicado no bus (passo 0,5%)

        // contadores da luta p/ o LastFight (contrato §5) — alimentados pelo CombatSystem
        private int _weaknessHits;
        private int _resistedHits;

        // ---- View do boss: BossConfigSO.prefab pooled; cápsula fallback se nulo ----
        [SerializeField] private float _viewAheadMeters = 12f;   // boss nasce à frente do exército, centro da pista

        // Clímax imponente (CANON §6 / Pilar 3): o golem precisa LER como GIGANTE perto das
        // tropas (~0,6 de escala). Piso de escala visual garante altura aparente ≥ ~3–4× a
        // tropa mesmo se o prefab/SO vier pequeno — só PARA CIMA, nunca encolhe o que já é grande.
        [SerializeField] private float _viewMinScale = 3.5f;
        // Eleva levemente o boss para a base ficar visível e a silhueta inteira caber no
        // terço superior do enquadramento de boss (a câmera mira o foco entre exército e boss).
        [SerializeField] private float _viewLiftMeters = 0.5f;

        // contrato do parâmetro int "State" do AnimatorController de boss (UnitVisualFactory)
        private static readonly int ViewStateParamHash = Animator.StringToHash("State");
        private const int ViewStateIdle = 0;
        private const int ViewStateAttack = 1;

        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _viewPools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();     // pool POR PREFAB (doc 12 §6.4)
        private readonly Dictionary<GameObject, GameObject> _viewTemplateByInstance =
            new Dictionary<GameObject, GameObject>();
        private GameObject _view;
        private Animator _viewAnimator;
        private int _viewAnimState = -1;
        private Vector3 _viewTargetScale = Vector3.one;
        private float _entranceSeconds;
        private readonly Countdown _entrance = new Countdown();      // Domain: entrada ≤ 2 s (CANON §6)
        private bool _entranceActive;
        private GameObject _fallbackTemplate;                        // cápsula construída 1× sob demanda

        // Inimigos da arena como AGREGADOS (combate agregado, doc 12 §4.4): grupo = dado,
        // sem GameObject por inimigo. O CombatSystem consome estes agregados por tick.
        private sealed class ArenaEnemyGroup
        {
            public UnitConfigSO Type;
            public int Count;
            public float Hp;
            public Vector3 Position;
        }

        private readonly List<ArenaEnemyGroup> _arenaEnemies = new List<ArenaEnemyGroup>();

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            if (GameManager.Instance != null)
            {
                // Contrato doc 12 §4.1 (EnterState): entrar em BossFight inicia a luta —
                // Core não enxerga Gameplay, então é o manager que assina StateEntered;
                // -= antes de += para Init repetido não duplicar a inscrição.
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateEntered += HandleStateEntered;
                GameManager.Instance.StateExited -= HandleStateExited;
                GameManager.Instance.StateExited += HandleStateExited;
            }
        }

        private void HandleStateEntered(GameState s)
        {
            if (s == GameState.BossScout)
            {
                RollRareBoss();   // ANTES da fase: o cartão do Scout pode anunciar a variante
                return;
            }
            if (s != GameState.BossFight) return;
            LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
            if (level != null) BeginFight(level.boss, level.bossHpMultiplier);
        }

        // Variante RARA (missão §4.2): rolada 1× por scout com RNG DERIVADO da seed
        // (System.Random, padrão do contrato §1.6 — primo 48611, offset 3, fixados no adendo §5).
        // Surpresa justa: anunciada no cartão ANTES da corrida, nunca emboscada.
        private void RollRareBoss()
        {
            NextFightRare = false;
            GameManager gm = GameManager.Instance;
            LevelConfigSO level = gm != null ? gm.CurrentLevel : null;
            if (level == null || level.boss == null) return;

            var rng = new System.Random(level.seed * 48611 + 3);
            NextFightRare = RareBossMath.Roll(rng, _rareBossChance);
            if (NextFightRare)
                GameEvents.RaiseRareBossAnnounced(new RareBossAnnounce(
                    level.boss.bossId, RareBossMath.HpMultiplier, RareBossMath.RewardMultiplier));
        }

        // Vitória, derrota e saída para o menu passam TODAS por ExitState(BossFight)
        // (inclusive o caminho do revive recusado: Pop + ChangeState) — único ponto de release.
        private void HandleStateExited(GameState s)
        {
            if (s != GameState.BossFight) return;

            // Saiu da luta com o boss VIVO = derrota/abandono: fotografa o LastFight aqui
            // (na vitória o Die() já preencheu com victory=true e Current já é null).
            if (Current != null)
            {
                FillLastFight(Current, victory: false);
                Current = null;
                _arenaEnemies.Clear();
                _telegraphing = false;
            }
            _deathSequenceActive = false;
            ReleaseView();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateExited -= HandleStateExited;
            }
        }

        public void BeginFight(BossConfigSO config)
        {
            BeginFight(config, 1f);
        }

        // O multiplicador de HP do Remote Config (boss_hp_mult_<id>) chega por PARÂMETRO:
        // Gameplay não referencia Services (fronteira de asmdef, doc 12 §2.3).
        public void BeginFight(BossConfigSO config, float hpMultiplier)
        {
            if (config == null) return;

            // consome a rolagem do Scout: variante rara = HP ×1.5 (RareBossMath) — 1 rolagem por fase
            bool isRare = NextFightRare;
            NextFightRare = false;
            if (isRare) hpMultiplier *= RareBossMath.HpMultiplier;

            Current = new BossRuntime(config, hpMultiplier) { IsRare = isRare };
            _nextWaveEvent = 0;
            _telegraphing = false;
            _arenaEnemies.Clear();
            BuildDomainWaves(config);

            // barra de HP do HUD (UI não enxerga Gameplay): cheia no início, depois por evento
            _lastBossHpRaised = 1f;
            GameEvents.RaiseBossHpChanged(1f);

            // soft reset do estado por LUTA (contrato §1.5: todo estado novo tem caminho de reset)
            _deathSequenceActive = false;
            UsedLaserThisFight = false;
            _weaknessHits = 0;
            _resistedHits = 0;
            LastElementalRelation = ElementRelation.Neutral;

            if (CrowdManager.Instance != null)
                CrowdManager.Instance.EnterArenaFormation();   // handoff sim→cinemática (decisão 3)

            SpawnView(config);   // prefab do SO (pooled) ou cápsula fallback, escala 0→alvo
            AttachBehavior(config, isRare);   // behavior modular na view + OnFightStart (missão W2-A)

            // entrada ≤ 2 s (CANON §6): a luta "começa" depois da animação de entrada
            _specialCooldown.Set(config.specialBaseCooldown + config.entranceSeconds);
        }

        // ------------------------------------------------------------------
        // BossBehavior modular (missão Nota 10): prefab autorado tem prioridade; sem ele, o
        // registry resolve por bossId e o componente é ADICIONADO na raiz da view. A view é
        // POOLED: no reuso, o componente adicionado ainda existe — procurar antes de Add.
        // Componentes stale de OUTRO boss (pool da cápsula fallback é compartilhado) ficam
        // inertes: nunca recebem Begin(), e todo behavior é no-op com FightActive=false.
        // ------------------------------------------------------------------
        private void AttachBehavior(BossConfigSO config, bool isRare)
        {
            CurrentBehavior = null;
            if (_view == null || Current == null) return;

            Type expected = BossBehaviorRegistry.Resolve(config.bossId);
            BossBehavior chosen = null;
            BossBehavior firstAuthored = null;
            BossBehavior[] existing = _view.GetComponentsInChildren<BossBehavior>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null) continue;
                // autorado no PREFAB (sem flag de registry) tem prioridade máxima — o autor decide
                if (!existing[i].AddedByRegistry && firstAuthored == null) firstAuthored = existing[i];
                if (chosen == null && existing[i].GetType() == expected) chosen = existing[i];
            }
            if (firstAuthored != null) chosen = firstAuthored;
            if (chosen == null)
            {
                chosen = _view.AddComponent(expected) as BossBehavior;
                if (chosen != null) chosen.AddedByRegistry = true;   // marca p/ reuso correto do pool
            }
            if (chosen == null) return;   // defensivo: tipo inválido no registry nunca derruba a luta

            CurrentBehavior = chosen;
            GameManager gm = GameManager.Instance;
            int levelIndex = gm != null && gm.CurrentLevel != null ? gm.CurrentLevel.levelIndex : 0;
            chosen.Begin(new BossContext(Current, config, _view.transform, levelIndex, isRare));
        }

        private void BuildDomainWaves(BossConfigSO config)
        {
            ArenaWaveEvent[] src = config.arenaWaves;
            if (src == null || src.Length == 0)
            {
                _domainWaves = Array.Empty<ArenaWave>();
                return;
            }
            if (_domainWaves.Length != src.Length) _domainWaves = new ArenaWave[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                _domainWaves[i] = new ArenaWave
                {
                    time = src[i] != null ? src[i].time : float.MaxValue,
                    enemyTypeId = i,
                    count = src[i] != null ? src[i].count : 0
                };
            }
        }

        private void Update()
        {
            TickDeathSequence();   // roda mesmo com Current==null: a sequência vive ENTRE Die() e Victory

            if (Current == null) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.BossFight) return;

            float dt = Time.deltaTime;
            Current.FightTime += dt;
            Current.TickStatus(dt);
            TickEntrance(dt);   // cosmético: escala 0→alvo com ease-out (CANON §6: ≤ 2 s)

            // Waves da arena: lista ORDENADA + ponteiro do Domain (decisão 1) — dt gigante
            // dispara todas em ordem, dt pequeno nunca duplica
            int before = _nextWaveEvent;
            int fired = WavePointer.Consume(Current.FightTime, _domainWaves, ref _nextWaveEvent);
            for (int i = before; i < before + fired; i++)
                SpawnWave(Current.Config.arenaWaves[i]);

            // Especial: cooldown → telegraph (decal + windup) → golpe (decisão 2)
            _specialCooldown.Tick(dt);
            if (_specialCooldown.Done && !_telegraphing)
            {
                _telegraphing = true;
                _telegraph.Set(Current.Config.telegraphSeconds);   // janela de leitura, padrão 1,0 s
                ApplyViewAnim(ViewStateAttack);                    // windup: o corpo telegrapha junto do decal
                if (VFXManager.Instance != null)
                    VFXManager.Instance.ShowTelegraph(Current.Config.specialAttackArea,
                                                      Current.Config.telegraphSeconds);

                // missão Nota 10: o aviso vira EVENTO (HUD pisca, áudio toca o sting) + hook do behavior.
                // Epicentro = massa do exército, o mesmo alvo do decal genérico.
                Vector3 epicenter = CrowdManager.Instance != null
                    ? CrowdManager.Instance.Centroid
                    : CrowdAnchor.Position;
                GameEvents.RaiseBossSpecialWarning(new BossSpecialTelegraph(
                    Current.Config.telegraphSeconds, epicenter, Current.Config.bossId));
                if (CurrentBehavior != null) CurrentBehavior.OnSpecialAttackWarning();
            }
            if (_telegraphing)
            {
                _telegraph.Tick(dt);
                if (_telegraph.Done) FireSpecial();
            }

            CheckPhaseAndDeath();   // cobre dano via DoT/efeitos que reduzem Hp direto no runtime
        }

        private void FireSpecial()
        {
            _telegraphing = false;
            ApplyViewAnim(ViewStateIdle);   // golpe disparado: corpo volta ao idle até o próximo windup
            if (Current == null) return;
            if (CrowdManager.Instance != null)
                CrowdManager.Instance.DamageArea(Current.Config.specialAttackArea,
                                                 Current.EffectiveSpecialDamage);   // ×mult de behavior (laser fase 2)
            _specialCooldown.Set(Current.SpecialCooldown());   // diminui por fase (e por behavior)

            // hook do behavior DEPOIS do golpe genérico: é aqui que o boss "assina" o especial
            // (horda do Titã, núcleo exposto do Escorpião, portal negativo da Entidade)
            if (CurrentBehavior != null) CurrentBehavior.OnSpecialAttackExecute();
        }

        public void ApplyDamage(float raw)
        {
            if (Current == null) return;
            // chart elemental já aplicado no DPS agregado (doc 12 §4.4); a vulnerabilidade dos
            // behaviors (núcleo exposto ×2 / voo ×0.25 / escudo ×0.4) entra NESTE funil único
            Current.Hp -= raw * Current.VulnerabilityMultiplier;
            CheckPhaseAndDeath();
        }

        /// <summary>
        /// Veredito elemental do tick do CombatSystem (já rate-limited ≥0,5 s na origem):
        /// alimenta os contadores do LastFight e o LastElementalRelation do fail reason.
        /// </summary>
        public void RegisterElementalHit(ElementRelation relation)
        {
            LastElementalRelation = relation;
            if (relation == ElementRelation.Weakness) _weaknessHits++;
            else if (relation == ElementRelation.Resisted || relation == ElementRelation.Immune) _resistedHits++;
        }

        /// <summary>Posição da view do boss (âncora de textos/VFX); fallback = ponto nominal da arena.</summary>
        public Vector3 CurrentBossPosition =>
            _view != null ? _view.transform.position
                          : CrowdAnchor.Position + Vector3.forward * _viewAheadMeters;

        private void CheckPhaseAndDeath()
        {
            if (Current == null) return;

            // HP do HUD por evento, passo ≥0,5% (padrão OnRunProgress — nunca polling da UI)
            float normalized = Current.MaxHp > 0f ? Mathf.Clamp01(Current.Hp / Current.MaxHp) : 0f;
            if (Mathf.Abs(normalized - _lastBossHpRaised) >= 0.005f)
            {
                _lastBossHpRaised = normalized;
                GameEvents.RaiseBossHpChanged(normalized);
            }

            int phase = CombatMath.BossPhase(Current.Hp, Current.MaxHp);   // Domain: limiares 0.5/0.25
            if (phase != Current.Phase)
            {
                Current.Phase = phase;   // fase nova = especial mais frequente
                if (Current.Config.rotatingWeakness) Current.RotateWeakness();   // Alien Supremo (M8)
                GameEvents.RaiseBossPhaseChanged(new BossPhase(phase, Current.ActiveWeakness));
                if (CurrentBehavior != null && Current != null)
                    CurrentBehavior.OnHealthPhaseChanged(Mathf.Clamp01(Current.Hp / Current.MaxHp));
            }

            if (Current != null && Current.Hp <= 0f) Die();
        }

        // Morte CINEMATOGRÁFICA (missão Nota 10): hooks/eventos disparam JÁ, mas a transição
        // para Victory espera ~1,2 s — slow motion + queda/explosões acontecem com o estado
        // ainda em BossFight (BossFight→Victory segue sendo a única transição legal).
        private void Die()
        {
            if (Current == null || _deathSequenceActive) return;
            BossRuntime dying = Current;

            // ordem do contrato (§5): LastFight ANTES do RaiseBossDied (listeners leem o resumo)
            FillLastFight(dying, victory: true);
            Vector3 deathPosition = CurrentBossPosition;

            // behavior lê o estado final ANTES de Current zerar (queda do Gigante etc.)
            if (CurrentBehavior != null) CurrentBehavior.OnDeath();

            Current = null;   // guarda contra re-entrada (dano múltiplo no mesmo tick)
            _arenaEnemies.Clear();
            _telegraphing = false;

            _lastBossHpRaised = 0f;
            GameEvents.RaiseBossHpChanged(0f);   // barra zera junto do golpe final

            GameEvents.RaiseBossDied(new BossDied(
                dying.Config.bossId, deathPosition, dying.IsRare, dying.FightTime));

            // golpe final: timeScale 0,3 com fixedDeltaTime escalado junto (doc 12 §3.1).
            // 1,6 s (vs 0,8 s antigos): cobre a sequência de 1,2 s e ainda entra ~0,4 s no
            // Victory — preserva a captura "golpe_final" do DevScreenshotRig (slow-mo ativo).
            if (VFXManager.Instance != null) VFXManager.Instance.SlowMotion(0.3f, 1.6f);

            _deathSequence.Set(DeathSequenceSeconds);
            _deathSequenceActive = true;
        }

        private void TickDeathSequence()
        {
            if (!_deathSequenceActive) return;
            GameManager gm = GameManager.Instance;
            if (gm == null) { _deathSequenceActive = false; return; }
            // abandono via pausa→menu zera a pilha sem ExitState(BossFight): cancela sem Victory fantasma
            if (gm.State == GameState.MainMenu || gm.State == GameState.Boot)
            {
                _deathSequenceActive = false;
                return;
            }
            if (gm.State != GameState.BossFight) return;   // overlay por cima congela a sequência

            // PAUSA hard (timeScale 0) congela a sequência: sem isso o tick unscaled avançaria
            // durante a pausa e transicionaria p/ Victory por baixo do PauseOverlay (bug do
            // fluxo pausa-na-morte). O slow-mo canônico (0,3) NÃO congela — só a pausa real.
            if (Time.timeScale <= 0f) return;

            _deathSequence.Tick(Time.unscaledDeltaTime);   // unscaled: o slow-mo não alonga a si mesmo
            if (!_deathSequence.Done) return;

            _deathSequenceActive = false;
            // recompensa do boss é responsabilidade da Meta, que reage a OnLevelFinished
            // (disparado pelo GameManager no ResolveEnd) — fronteira de asmdef (doc 12 §2.3);
            // a view é devolvida ao pool no ExitState(BossFight) que esta transição dispara
            gm.ChangeState(GameState.Victory);
        }

        // Fotografia da luta p/ ComboSystem (W2-C), álbum e ResultScreen (Onda 3) — contrato §5.
        private void FillLastFight(BossRuntime runtime, bool victory)
        {
            LastFight = new BossFightSummary
            {
                bossId = runtime.Config != null ? runtime.Config.bossId : string.Empty,
                maxHp = runtime.MaxHp,
                fightSeconds = runtime.FightTime,
                wasRare = runtime.IsRare,
                weaknessHits = _weaknessHits,
                resistedHits = _resistedHits,
                victory = victory
            };
        }

        // ------------------------------------------------------------------
        // View do boss: BossConfigSO.prefab quando existir (instância POOLED na arena,
        // entrada escala 0→alvo com ease-out); cápsula fallback quando nulo.
        // Cosmético puro: HP/fase/dano vivem no BossRuntime — nada aqui altera a luta.
        // ------------------------------------------------------------------

        private void SpawnView(BossConfigSO config)
        {
            ReleaseView();   // defensivo: re-entrada sem ExitState não vaza instância

            bool usingRealPrefab = config.prefab != null;
            GameObject template = usingRealPrefab ? config.prefab : GetFallbackTemplate();
            if (template == null) return;

            _view = GetViewPool(template).Get();
            _viewTemplateByInstance[_view] = template;

            // arena: centro da pista (x=0), alguns metros à frente de onde o exército chegou
            Vector3 pos = CrowdAnchor.Position;
            pos.x = 0f;
            pos.y = _viewLiftMeters;   // leve elevação: base visível, silhueta inteira no terço superior
            pos.z += _viewAheadMeters;
            // boss encara o exército (que chega vindo de −Z) E a câmera (atrás/abaixo do exército):
            // de frente para −Z é a mesma direção — o chefão encara o jogador no clímax.
            _view.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.back));

            // entrada canônica (CANON §6, ≤ 2 s): escala 0 → escala-alvo com ease-out.
            // PISO de escala visual (clímax imponente): garante que o golem leia GIGANTE perto
            // da tropa mesmo se o prefab/SO vier pequeno — uniformiza só PARA CIMA. Aplicado
            // SÓ ao prefab real (onde a escala do root é a medida do boss); o greybox/cápsula
            // fallback já codifica os ~8 m num filho 4× e mantém a escala de root 1.0 intacta.
            Vector3 templateScale = template.transform.localScale;
            _viewTargetScale = usingRealPrefab ? MakeImposing(templateScale) : templateScale;
            _view.transform.localScale = Vector3.zero;

            // Empurra o enquadramento de boss para a CameraRig (ela só interpola; não conhece o
            // boss — sem acoplamento/ciclo, doc 12 §2.3). Foco = meio entre o exército e o boss.
            PushBossFraming(pos);
            _entranceSeconds = Mathf.Max(0.05f, config.entranceSeconds);
            _entrance.Set(_entranceSeconds);
            _entranceActive = true;

            _viewAnimator = _view.GetComponentInChildren<Animator>(true);
            if (_viewAnimator != null && !HasIntParam(_viewAnimator, ViewStateParamHash))
                _viewAnimator = null;   // cápsula greybox/fallback não tem o parâmetro — vira no-op
            _viewAnimState = -1;
            ApplyViewAnim(ViewStateIdle);
        }

        // Piso de escala visual do clímax: leva a MENOR componente da escala-alvo a ≥ _viewMinScale,
        // preservando a proporção do prefab. Só aumenta — um boss já grande (ex.: 5.0) fica intacto.
        private Vector3 MakeImposing(Vector3 baseScale)
        {
            float minComponent = Mathf.Min(baseScale.x, Mathf.Min(baseScale.y, baseScale.z));
            if (minComponent <= 0f) return new Vector3(_viewMinScale, _viewMinScale, _viewMinScale);
            if (minComponent >= _viewMinScale) return baseScale;
            float k = _viewMinScale / minComponent;
            return baseScale * k;
        }

        // Foco do enquadramento = ponto médio entre o Centroid do exército e a posição do boss,
        // empurrado para a CameraRig (que só interpola). A câmera mira aqui com leve ângulo
        // p/ baixo: golem inteiro no terço superior-central, frente do exército no inferior.
        private void PushBossFraming(Vector3 bossPos)
        {
            if (CameraRig.Instance == null) return;
            Vector3 army = CrowdManager.Instance != null ? CrowdManager.Instance.Centroid : CrowdAnchor.Position;
            Vector3 focus = Vector3.Lerp(army, bossPos, 0.6f);   // viés ao boss: ele é o protagonista do clímax
            CameraRig.Instance.SetBossFraming(focus);
        }

        private void TickEntrance(float dt)
        {
            if (!_entranceActive || _view == null) return;
            _entrance.Tick(dt);
            float t = 1f - Mathf.Clamp01(_entrance.Remaining / _entranceSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);   // ease-out cúbico: chega "pesado", sem pop
            _view.transform.localScale = _viewTargetScale * eased;
            if (_entrance.Done)
            {
                _entranceActive = false;
                _view.transform.localScale = _viewTargetScale;
            }
        }

        private void ReleaseView()
        {
            // behavior morre com a view (pooled!): End() para coroutines e limpa o tint MPB —
            // a instância volta ao pool limpa; o componente em si SOBREVIVE (reusado no GetComponents)
            if (CurrentBehavior != null)
            {
                CurrentBehavior.End();
                CurrentBehavior = null;
            }

            _entranceActive = false;
            _viewAnimator = null;
            _viewAnimState = -1;

            // Saída do BossFight (vitória/derrota/menu): devolve a câmera ao enquadramento de
            // corrida suavemente. Único ponto de release, simétrico ao SetBossFraming do spawn.
            if (CameraRig.Instance != null) CameraRig.Instance.ClearBossFraming();

            if (_view == null) return;

            if (_viewTemplateByInstance.TryGetValue(_view, out GameObject template) && template != null
                && _viewPools.TryGetValue(template, out ObjectPool<GameObject> pool))
            {
                _viewTemplateByInstance.Remove(_view);
                pool.Release(_view);   // Release, nunca Destroy (doc 12 §6.4)
            }
            else
            {
                _viewTemplateByInstance.Remove(_view);
                _view.SetActive(false);
            }
            _view = null;
        }

        private void ApplyViewAnim(int state)
        {
            if (_viewAnimator == null || _viewAnimState == state) return;
            _viewAnimState = state;
            _viewAnimator.SetInteger(ViewStateParamHash, state);
        }

        private ObjectPool<GameObject> GetViewPool(GameObject template)
        {
            if (_viewPools.TryGetValue(template, out ObjectPool<GameObject> pool)) return pool;
            pool = new ObjectPool<GameObject>(
                () =>
                {
                    GameObject go = Instantiate(template, transform);
                    go.SetActive(false);
                    return go;
                },
                go => go.SetActive(true),
                go => go.SetActive(false),
                go => { if (go != null) Destroy(go); },
                collectionCheck: false, defaultCapacity: 1, maxSize: 2);
            _viewPools[template] = pool;
            return pool;
        }

        // Cápsula greybox (~8 m) construída 1× sob demanda — mantém a leitura do boss
        // mesmo com BossConfigSO.prefab nulo (mesma silhueta do Boss_Greybox do factory).
        private GameObject GetFallbackTemplate()
        {
            if (_fallbackTemplate != null) return _fallbackTemplate;
            var root = new GameObject("Boss_FallbackCapsule");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Collider collider = body.GetComponent<Collider>();
            if (collider != null) Destroy(collider);   // view pura: sem física na arena
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 4f, 0f);
            body.transform.localScale = new Vector3(4f, 4f, 4f);
            _fallbackTemplate = root;
            return root;
        }

        private static bool HasIntParam(Animator animator, int hash)
        {
            AnimatorControllerParameter[] ps = animator.parameters;   // aloca: 1× por spawn, nunca por frame
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Int) return true;
            return false;
        }

        private void SpawnWave(ArenaWaveEvent w)
        {
            if (w == null) return;
            SpawnExtraWave(w.enemyType, w.count);
        }

        /// <summary>
        /// Invoca um grupo agregado EXTRA na arena (missão Nota 10, W2-A) — API pública dos
        /// BossBehaviors (hordas do Zumbi Titã, drones do Escorpião). Reusa o mesmo agregado
        /// ArenaEnemyGroup das waves de dados: zero GameObject por inimigo (doc 12 §4.4).
        /// No-op fora da luta ou com parâmetros inválidos (null-safe, contrato §1.12).
        /// </summary>
        public void SpawnExtraWave(UnitConfigSO type, int count)
        {
            if (Current == null || type == null || count <= 0) return;
            // grupo agregado posicionado ao redor da arena, alternando os flancos
            int idx = _arenaEnemies.Count;
            Vector3 center = CrowdAnchor.Position + new Vector3(0f, 0f, 10f);
            Vector3 offset = new Vector3((idx & 1) == 0 ? -3f : 3f, 0f, 2f * (idx % 3));
            _arenaEnemies.Add(new ArenaEnemyGroup
            {
                Type = type,
                Count = count,
                Hp = type.baseHp * count,
                Position = center + offset
            });
        }

        /// <summary>DPS somado das waves vivas — contagem proporcional ao HP restante do grupo.</summary>
        public float TotalArenaDps
        {
            get
            {
                float dps = 0f;
                for (int i = 0; i < _arenaEnemies.Count; i++)
                {
                    ArenaEnemyGroup g = _arenaEnemies[i];
                    if (g.Type == null || g.Type.baseHp <= 0f) continue;
                    int alive = Mathf.CeilToInt(g.Hp / g.Type.baseHp);
                    dps += g.Type.baseDps * Mathf.Min(alive, g.Count);
                }
                return dps;
            }
        }

        /// <summary>
        /// Waves vivas absorvem o dano do exército ANTES do boss (estão entre os dois).
        /// Retorna o dano que sobrou para o boss.
        /// </summary>
        public float DamageArenaEnemies(float damage)
        {
            for (int i = _arenaEnemies.Count - 1; i >= 0 && damage > 0f; i--)
            {
                ArenaEnemyGroup g = _arenaEnemies[i];
                if (g.Hp > damage)
                {
                    g.Hp -= damage;
                    damage = 0f;
                }
                else
                {
                    damage -= g.Hp;
                    _arenaEnemies.RemoveAt(i);   // fim de onda por esgotamento do agregado
                }
            }
            return damage;
        }

        /// <summary>Encadeamento do Raio (CANON §4): dano nos até N grupos mais próximos da origem.</summary>
        public void DamageNearestArenaEnemies(Vector3 origin, float damage, int maxTargets)
        {
            ArenaEnemyGroup previousHit = null;
            for (int hit = 0; hit < maxTargets; hit++)
            {
                int best = -1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < _arenaEnemies.Count; i++)
                {
                    ArenaEnemyGroup g = _arenaEnemies[i];
                    if (g == previousHit) continue;
                    float sqr = (g.Position - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }
                if (best < 0) return;

                ArenaEnemyGroup target = _arenaEnemies[best];
                previousHit = target;
                if (target.Hp > damage) target.Hp -= damage;
                else _arenaEnemies.RemoveAt(best);
            }
        }
    }

    /// <summary>
    /// Resumo da última luta de boss (contrato §5, missão Nota 10) — preenchido pelo
    /// BossManager na morte (victory=true) OU na saída com boss vivo (derrota/abandono).
    /// SOBREVIVE ao fim da luta: ComboSystem (W2-C) monta os combos a partir daqui e a
    /// Onda 3 (álbum/ResultScreen) lê depois do OnBossDied/OnLevelFinished.
    /// </summary>
    public struct BossFightSummary
    {
        public string bossId;
        public float maxHp;          // HP máximo JÁ com multiplicadores (RC + variante rara)
        public float fightSeconds;   // duração da luta (combo BossBreaker ≤ 8 s; recorde do álbum)
        public bool wasRare;         // variante rara (RareBossMath) → recompensa ×3
        public int weaknessHits;     // vereditos de FRAQUEZA registrados (rate-limited na origem)
        public int resistedHits;     // vereditos RESISTIU/IMUNE registrados
        public bool victory;
    }
}
