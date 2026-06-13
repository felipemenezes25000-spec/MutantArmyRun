using System.Collections;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Orquestrador de JUICE da corrida (doc 09 §4 / doc 14 §5/§7) — 100% cosmético, assina o
    /// bus (GameEvents) e NUNCA muta estado de jogo. Reações:
    /// · portal consumido → ScalePop no rótulo + flash no frame + burst tintado (VFXManager);
    /// · multiplicação ×2+ → cascata de pops com stagger; ×3+ (grande) → screen shake + soco
    ///   de FOV + vibração média (doc 14 §5);
    /// · mutação ativada (OnMutationGained) → burst magenta no centróide + pulso de câmera +
    ///   vibração média (o selo textual e o slot do HUD são de outras camadas);
    /// · estouro de Supply → burst dourado no centróide (as moedas até o HUD são do
    ///   FloatingTextSpawner, camada UI);
    /// · dano no boss → micro shake + flash vermelho (MaterialPropertyBlock — nunca
    ///   instancia material) + JuiceEvents p/ o SFX (Services não é visível daqui, §2.3) +
    ///   vibração leve por pulso;
    /// · fase de vida do boss → shake forte + zoom punch de FOV + vibração forte;
    /// · vitória → confete + vibração forte (o slow-mo 0,3/0,8 do golpe final é disparado pelo
    ///   BossManager.Die via VFXManager.SlowMotion); derrota → dessaturação rápida (Volume 0→1).
    /// Reações da missão Nota 10 (CONTRACT §5 — eventos publicados por W2-A/W2-C):
    /// · golpe elemental no boss (OnBossElementalHit) → flash na COR DO ELEMENTO em Weakness
    ///   (fogo laranja, gelo azul, raio amarelo, veneno verde…) + shake leve; Resisted/Immune
    ///   → flash CINZA curto e NENHUM shake — sensação de "bateu em parede";
    /// · morte do boss (OnBossDied) → sequência cinematográfica: shake forte + PunchFocus da
    ///   CameraRig no corpo + chuva de moedas em bursts staggered + vibração pesada, tudo em
    ///   tempo REAL (o slow-mo canônico do BossManager.Die não a alonga);
    /// · combo conquistado (OnComboEarned) → pulso leve de FOV + tap (texto é da Onda 3/UI);
    /// · inimigo de pista morto (OnTrackEnemyKilled) → burst pequeno AGREGADO rate-limited
    ///   (nunca 1 VFX por inimigo, doc 12 §6.4) + tap esporádico;
    /// · veredito de portal/risco (JuiceEvents OnGood/BadGateChoice, OnRiskResolved) →
    ///   dourado no acerto, vermelho-escuro na armadilha.
    /// Vibração via Core.Haptics: hooks PREPARADOS, no-op fora de Android/iOS (build Windows/
    /// headless) e respeitando SaveData.hapticsOn — nunca atrapalha o autoteste.
    /// Dano no boss mantém o POLLING do BossRuntime.Hp como FALLBACK (boss sem golpe
    /// classificado continua dando feedback), mas o evento OnBossElementalHit tem precedência:
    /// o polling pula o próprio flash por uma janela curta após cada evento — nunca 2× juice.
    /// </summary>
    public class JuiceController : MonoBehaviour
    {
        [Header("Derrota: dessaturação (Volume com ColorAdjustments saturation −100)")]
        [SerializeField] private Volume _defeatVolume;
        [SerializeField] private float _desaturateSeconds = 0.35f;

        [Header("Hit no boss")]
        [SerializeField] private float _bossPulseInterval = 0.3f;   // pulso de feedback, não 1 por tick
        [SerializeField] private float _bossFlashSeconds = 0.18f;

        [Header("Cascata de multiplicação")]
        [SerializeField] private int _cascadeMaxPops = 8;
        [SerializeField] private float _cascadeStagger = 0.05f;

        [Header("Punch / shake de portal (doc 14 §5)")]
        [SerializeField] private float _bigMultiplyValue = 3f;      // ×3+ é "grande": ganha shake + punch
        [SerializeField] private float _bigMultiplyShake = 1.2f;    // graus de shake na multiplicação grande
        [SerializeField] private float _strongPortalFovPunch = -4f; // estreita o FOV num soco curto
        [SerializeField] private float _mutationShake = 0.8f;       // mutação ativada: pulso leve + burst

        [Header("Missão Nota 10: fraqueza / morte do boss / combos / pista")]
        [SerializeField] private float _eventFlashSuppressSeconds = 0.6f; // polling cala o flash após um evento
        [SerializeField] private float _weaknessShake = 0.7f;             // shake leve do acerto de fraqueza
        [SerializeField] private float _resistedFlashSeconds = 0.12f;     // flash cinza CURTO ("parede")
        [SerializeField] private float _comboFovPunch = -2.5f;            // pulso leve por combo conquistado
        [SerializeField] private float _bossDeathShake = 2.5f;            // shake forte da morte cinematográfica
        [SerializeField] private int _bossDeathCoinBursts = 3;            // bursts de moeda staggered (2-3)
        [SerializeField] private float _bossDeathBurstStagger = 0.18f;    // intervalo REAL entre bursts
        [SerializeField] private float _trackKillBurstInterval = 0.15f;   // burst agregado de pista (≥0,15 s)
        [SerializeField] private float _trackKillHapticInterval = 0.5f;   // tap esporádico, nunca zumbido

        [Header("Crescimento do exército (o ato central — doc 14 §5): peso + legibilidade em 3s")]
        // DECISÃO (causa-raiz da missão): pegar portal e crescer era SILENCIOSO (só o número do
        // HUD mudava). Aqui o crescimento ganha CORPO — texto flutuante grande ("+N"/"×N"/
        // "ARQUEIRO!"), burst tingido na multidão e soco de câmera proporcional ao ganho — tudo
        // cosmético, reagindo ao OnGateConsumed que JÁ trafega gate+newCount (sem tocar o
        // CrowdManager nem o funil de consumo). Verde alegre na soma, dourado no multiplicador,
        // ciano-transformação na conversão de classe.
        [SerializeField] private float _growthBurstUp = 1.3f;             // altura do burst de crescimento sobre o centróide
        [SerializeField] private float _addFlatBigThreshold = 25f;        // +25/+50 "estala" (punch maior + Medium)
        [SerializeField] private float _addFlatFovPunch = -3.5f;          // soco base da soma forte
        [SerializeField] private float _multiplyFovPunchPerStep = -1.8f;  // soco por "passo" de multiplicador (×2→1 passo…)

        [Header("Impacto que se SENTE (hitstop sem mexer no timeScale)")]
        // DECISÃO (CONTRACT §1.10): um freeze real de timeScale colidiria com o slow-mo de
        // morte do BossManager (SlowMotion(0.3,1.6) — a regra de aninhamento ficaria com a
        // janela MAIOR e a escala MAIS profunda, alongando a luta) e com a pausa; em PlayMode
        // a 8-10× ainda atrapalharia o ritmo. Em vez disso o "punch" de impacto entrega o
        // PESO via shake seco + soco de FOV mais fundo + flash forte — 100% cosmético, em
        // tempo unscaled, à prova de pausa/slow-mo e dos testes.
        [SerializeField] private float _impactShake = 1.4f;               // shake SECO do impacto grande
        [SerializeField] private float _impactShakeSeconds = 0.14f;       // curto: tranco, não terremoto
        [SerializeField] private float _impactFovPunch = -5f;             // soco de FOV mais fundo que o normal
        [SerializeField] private float _strongWeaknessDamage = 12f;       // ≥ isso, o acerto de fraqueza "estala"

        private bool _subscribedToGameManager;
        private BossRuntime _trackedBoss;
        private float _lastBossHp;
        private float _lastBossPulseTime;
        private float _lastElementalHitTime = -999f;   // janela em que o polling cala o próprio flash
        private float _lastTrackKillBurstTime = -999f;
        private float _lastTrackKillHapticTime = -999f;
        private readonly List<Renderer> _bossRenderers = new List<Renderer>(8);
        private MaterialPropertyBlock _mpb;
        private Coroutine _bossFlashRoutine;
        private Coroutine _fovPunchRoutine;
        private Coroutine _bossDeathRoutine;
        private MutantArmy.UI.FloatingTextSpawner _floatingText;   // cache do glue de texto flutuante (camada UI)

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
            GameEvents.OnMutationGained += HandleMutationGained;
            GameEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnBossElementalHit += HandleBossElementalHit;
            GameEvents.OnBossDied += HandleBossDied;
            GameEvents.OnComboEarned += HandleComboEarned;
            GameEvents.OnTrackEnemyKilled += HandleTrackEnemyKilled;
            GameEvents.OnEnemyWaveCleared += HandleEnemyWaveCleared;
            JuiceEvents.OnGoodGateChoice += HandleGoodGateChoice;
            JuiceEvents.OnBadGateChoice += HandleBadGateChoice;
            JuiceEvents.OnRiskResolved += HandleRiskResolved;
            TrySubscribeGameManager();
        }

        private void OnDisable()
        {
            // bus estático sobrevive a cenas — sempre limpar (doc 12 §3.2)
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnMutationGained -= HandleMutationGained;
            GameEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnBossElementalHit -= HandleBossElementalHit;
            GameEvents.OnBossDied -= HandleBossDied;
            GameEvents.OnComboEarned -= HandleComboEarned;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            GameEvents.OnEnemyWaveCleared -= HandleEnemyWaveCleared;
            JuiceEvents.OnGoodGateChoice -= HandleGoodGateChoice;
            JuiceEvents.OnBadGateChoice -= HandleBadGateChoice;
            JuiceEvents.OnRiskResolved -= HandleRiskResolved;
            if (GameManager.Instance != null)
                GameManager.Instance.StateEntered -= HandleStateEntered;
            _subscribedToGameManager = false;
            StopAllCoroutines();
            _bossFlashRoutine = null;
            _bossDeathRoutine = null;
            ResetDesaturation();
            ClearBossOverrides();
        }

        // GameManager nasce no Boot; se a cena Game abrir direto no editor ele pode não
        // existir no OnEnable — re-tenta no Update (idempotente, -= antes de +=).
        private void TrySubscribeGameManager()
        {
            if (_subscribedToGameManager || GameManager.Instance == null) return;
            GameManager.Instance.StateEntered -= HandleStateEntered;
            GameManager.Instance.StateEntered += HandleStateEntered;
            _subscribedToGameManager = true;
        }

        private void Update()
        {
            TrySubscribeGameManager();
            PollBossDamage();
        }

        // ------------------------------------------------------------------ portal

        private void HandleGateConsumed(GateResult result)
        {
            GateConfigSO gate = result.gate;
            if (gate == null) return;

            GatePairView pair = FindNearestPair(CrowdAnchor.Position);
            GateView chosen = FindChosenView(pair, gate);

            Vector3 burstPos = chosen != null
                ? chosen.transform.position + Vector3.up * 1.5f
                : CrowdAnchor.Position + Vector3.up * 1.5f;

            if (chosen != null)
            {
                TMP_Text label = chosen.GetComponentInChildren<TMP_Text>(true);
                if (label != null) Tween.ScalePop(label.transform, 0.35f);
                MeshRenderer frame = FindFrameRenderer(chosen);
                if (frame != null) StartCoroutine(FlashFrame(frame, gate.portalColor));
            }

            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(burstPos, GateBurstColor(gate));

            // CRESCIMENTO/TRANSFORMAÇÃO com PESO (causa-raiz da missão): texto flutuante grande,
            // burst tingido na multidão e soco proporcional ao ganho. Roteado por tipo — cada
            // portal "diz" o que fez em ≤3 s. Mantém a cascata/soco antigos do multiplicador.
            switch (gate.gateType)
            {
                case GateType.AddFlat:
                    HandleAddFlatGrowth(gate);
                    break;
                case GateType.Multiply:
                    HandleMultiplyGrowth(gate, result.newCount);
                    break;
                case GateType.ClassConvert:
                    HandleClassConvert(gate);
                    break;
            }
        }

        // SOMA (+N): verde alegre na multidão + texto "+N" grande + soco/vibração dosados pelo
        // tamanho. +25/+50 "estala" (punch fundo + Medium); +10 honesto fica leve. Soma NEGATIVA
        // (−10, punição disfarçada) cai no caminho vermelho-escuro — nunca humilha (CANON).
        private void HandleAddFlatGrowth(GateConfigSO gate)
        {
            int amount = (int)gate.value;
            Vector3 center = CrowdCenter();

            if (amount > 0)
            {
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayGateBurst(center + Vector3.up * _growthBurstUp, GrowthGreen);
                ShowGrowthText("+" + amount);
                // ganho grande soca mais fundo; +10 fica num pulso leve
                bool big = gate.value >= _addFlatBigThreshold;
                PunchCameraFov(_addFlatFovPunch * (big ? 1.6f : 1f), 0.25f);
                if (big) Core.Haptics.Medium();
                else Core.Haptics.Light();
            }
            else if (amount < 0)
            {
                PlayNegativeGrowth(center, amount.ToString());   // ex.: "-10"
            }
        }

        // MULTIPLICADOR (×N): dourado de proeza + texto "×N" + cascata de pops já existente. O
        // soco cresce com o multiplicador (×2 leve, ×3/×5 clímax). ÷N (value<1) é armadilha →
        // caminho vermelho-escuro. value≈1 (sem ganho) não festeja para o elogio não virar ruído.
        private void HandleMultiplyGrowth(GateConfigSO gate, int newCount)
        {
            Vector3 center = CrowdCenter();

            if (gate.value < 1f)
            {
                PlayNegativeGrowth(center, "÷" + Mathf.RoundToInt(1f / Mathf.Max(0.01f, gate.value)));
                return;
            }
            if (gate.value < 1.05f) return;   // ×1.0x: ganho nulo, sem festa

            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(center + Vector3.up * _growthBurstUp, GoldFeedback);
            ShowGrowthText("×" + FormatMultiplier(gate.value));

            // ×2+ mantém a cascata sobre a formação; ×3/×5 (big) sobe o espetáculo (doc 14 §5)
            if (gate.value >= 2f)
            {
                StartCoroutine(CascadePops(newCount));
                if (gate.value >= _bigMultiplyValue)
                {
                    Tween.ShakeCamera(_bigMultiplyShake, 0.22f);
                    PunchCameraFov(_strongPortalFovPunch, 0.25f);
                    Core.Haptics.Medium();
                }
                else
                {
                    // ×2: punch proporcional aos "passos" de multiplicador, ainda sem terremoto
                    PunchCameraFov(_multiplyFovPunchPerStep * (gate.value - 1f), 0.22f);
                    Core.Haptics.Light();
                }
            }
        }

        // CONVERSÃO DE CLASSE ("VIRAR ARQUEIRO/MAGO"): os modelos já trocam sozinhos (o
        // CrowdRenderer rebatcha por typeId) — aqui mora o MOMENTO da transformação: burst
        // ciano-transformação + texto com o nome da classe ("ARQUEIRO!"). Sem mexer no exército.
        private void HandleClassConvert(GateConfigSO gate)
        {
            Vector3 center = CrowdCenter();
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(center + Vector3.up * _growthBurstUp, TransformCyan);
            string className = gate.unitToAdd != null && !string.IsNullOrEmpty(gate.unitToAdd.displayName)
                ? gate.unitToAdd.displayName.ToUpperInvariant()
                : "NOVA CLASSE";
            ShowGrowthText(className + "!");
            PunchCameraFov(-3f, 0.22f);
            Core.Haptics.Light();
        }

        // Portal NEGATIVO (−N / ÷N): efeito curto vermelho-escuro + shake leve. CANON: o jogo
        // não humilha — feedback honesto da perda, sem exagero. Texto também mostra a perda.
        private void PlayNegativeGrowth(Vector3 center, string label)
        {
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(center + Vector3.up * _growthBurstUp, BadDarkRed);
            ShowGrowthText(label);
            Tween.ShakeCamera(0.5f, 0.14f);
            Core.Haptics.Light();
        }

        // Texto flutuante grande do crescimento (camada UI, FloatingTextSpawner — Gameplay pode
        // chamar UI, asmdef permite). Cache resolvido sob demanda e re-tentado se a UI ainda não
        // existir (cena Game aberta direto no editor); null-safe — degrada para só o burst.
        private void ShowGrowthText(string text)
        {
            if (_floatingText == null)
                _floatingText = FindFirstObjectByType<MutantArmy.UI.FloatingTextSpawner>(FindObjectsInactive.Include);
            if (_floatingText != null) _floatingText.ShowFloatingText(text);
        }

        // Centróide vivo da multidão (âncora do espetáculo de crescimento); fallback no anchor.
        private static Vector3 CrowdCenter()
        {
            CrowdManager crowd = CrowdManager.Instance;
            return crowd != null ? crowd.Centroid : CrowdAnchor.Position;
        }

        // "×2" / "×1.5": inteiro quando redondo, 1 casa quando fracionário — leitura honesta.
        private static string FormatMultiplier(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static readonly Color GrowthGreen = new Color(0.40f, 1f, 0.45f);   // soma: verde alegre
        private static readonly Color TransformCyan = new Color(0.35f, 0.85f, 1f); // conversão de classe

        private static Color GateBurstColor(GateConfigSO gate)
        {
            bool negative = (gate.gateType == GateType.Multiply && gate.value < 1f)
                            || (gate.gateType == GateType.AddFlat && gate.value <= 0f);
            return negative ? new Color(1f, 0.45f, 0.10f) : gate.portalColor;
        }

        // o par consumido está (por construção) colado no exército: o mais próximo em |Δz|
        private static GatePairView FindNearestPair(Vector3 anchorPos)
        {
            GatePairView[] pairs = FindObjectsByType<GatePairView>(FindObjectsSortMode.None);
            GatePairView best = null;
            float bestDist = 8f;   // além disso não é o par que acabou de ser tocado
            for (int i = 0; i < pairs.Length; i++)
            {
                float d = Mathf.Abs(pairs[i].transform.position.z - anchorPos.z);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = pairs[i];
                }
            }
            return best;
        }

        private static GateView FindChosenView(GatePairView pair, GateConfigSO gate)
        {
            if (pair == null) return null;
            GateView[] views = pair.GetComponentsInChildren<GateView>(true);
            for (int i = 0; i < views.Length; i++)
                if (views[i].Config == gate) return views[i];
            return null;
        }

        // o rótulo TMP 3D também usa MeshRenderer: o flash é no FRAME, nunca no texto
        private static MeshRenderer FindFrameRenderer(GateView view)
        {
            MeshRenderer[] renderers = view.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].GetComponent<TMP_Text>() == null) return renderers[i];
            return null;
        }

        // flash branco → cor do portal, via MPB (idêntico ao caminho do GateView — sem
        // instanciar material). Termina re-aplicando a cor honesta do dado.
        private IEnumerator FlashFrame(MeshRenderer frame, Color portalColor)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            float elapsed = 0f;
            const float seconds = 0.3f;
            while (elapsed < seconds)
            {
                if (frame == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                Color c = Color.Lerp(Color.white, portalColor, Mathf.Clamp01(elapsed / seconds));
                c.a = portalColor.a;
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);
                frame.SetPropertyBlock(_mpb);
                yield return null;
            }
        }

        // pops em cascata sobre a formação: escala/burst com stagger — espetáculo
        // sequencial, nunca frame-spike (mesma filosofia do metering, CANON §3.2)
        private IEnumerator CascadePops(int newCount)
        {
            int pops = Mathf.Clamp(newCount / 4, 3, _cascadeMaxPops);
            for (int i = 0; i < pops; i++)
            {
                CrowdManager crowd = CrowdManager.Instance;
                Vector3 center = crowd != null ? crowd.Centroid : CrowdAnchor.Position;
                float radius = 0.45f * Mathf.Sqrt((crowd != null ? crowd.Count : 1) + 1);
                Vector2 offset = Random.insideUnitCircle * radius;
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayPopBurst(center + new Vector3(offset.x, 0.5f, offset.y));
                yield return new WaitForSecondsRealtime(_cascadeStagger);
            }
        }

        // ------------------------------------------------------------------ supply / boss / fim

        private void HandleSupplyOverflow(SupplyOverflow overflow)
        {
            // moedas voadoras até o HUD são do FloatingTextSpawner (UI); aqui só o
            // espetáculo de mundo: burst dourado no centróide da formação
            if (VFXManager.Instance == null) return;
            CrowdManager crowd = CrowdManager.Instance;
            Vector3 center = crowd != null ? crowd.Centroid : CrowdAnchor.Position;
            VFXManager.Instance.PlayGateBurst(center + Vector3.up * 1.2f, new Color(1f, 0.84f, 0.25f));
        }

        // Mutação ativada (CANON §3.3): burst colorido no centróide + pulso leve de câmera +
        // vibração média. O selo textual "MUTATION!" e o slot do HUD são de outras camadas
        // (FeedbackTextController/HudController, via OnMutationGained) — aqui só o espetáculo.
        private void HandleMutationGained(MutationConfigSO mutation)
        {
            CrowdManager crowd = CrowdManager.Instance;
            Vector3 center = crowd != null ? crowd.Centroid : CrowdAnchor.Position;
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(center + Vector3.up * 1.4f, MutationBurstColor);
            // nome da mutação flutua junto do burst magenta (ganho raro e permanente, CANON §3.3);
            // o selo "MUTATION!" do FeedbackTextController é outra camada — aqui o NOME concreto
            if (mutation != null && !string.IsNullOrEmpty(mutation.displayName))
                ShowGrowthText(mutation.displayName.ToUpperInvariant() + "!");
            Tween.ShakeCamera(_mutationShake, 0.18f);
            PunchCameraFov(-3f, 0.22f);
            Core.Haptics.Medium();
        }

        // Mutação "brilha" num magenta vivo, distinto de qualquer portal — leitura clara de
        // que algo raro e permanente aconteceu (CANON §3.3). O MutationConfigSO não carrega cor.
        private static readonly Color MutationBurstColor = new Color(0.85f, 0.35f, 1f);

        private void HandleBossPhaseChanged(BossPhase phase)
        {
            Tween.ShakeCamera(2.5f, 0.5f);   // shake FORTE: virada de fase é evento maiúsculo
            PunchCameraFov(-7f, 0.5f);
            Core.Haptics.Heavy();            // virada de fase do boss: vibração forte (doc 14 §5)
        }

        private void HandleLevelFinished(LevelResult result)
        {
            if (result.won)
            {
                if (VFXManager.Instance != null) VFXManager.Instance.PlayConfetti();
                Core.Haptics.Heavy();   // golpe final/vitória: junto do slow-mo do BossManager.Die
            }
            else
            {
                // dessaturação rápida — o mundo "apaga" na derrota (doc 09 §4.5)
                Volume volume = _defeatVolume;
                if (volume != null)
                    Tween.Float(0f, 1f, _desaturateSeconds, Tween.Ease.OutCubic,
                                w => { if (volume != null) volume.weight = w; });
            }
        }

        private void HandleStateEntered(GameState state)
        {
            // retry/nova corrida na MESMA cena (soft reset §4.11): cor volta ao normal e os
            // rate-limits/sequências da missão zeram — nada da corrida anterior vaza juice
            if (state == GameState.Running || state == GameState.BossScout)
            {
                ResetDesaturation();
                _trackedBoss = null;
                ClearBossOverrides();
                _bossRenderers.Clear();
                _lastElementalHitTime = -999f;
                _lastTrackKillBurstTime = -999f;
                _lastTrackKillHapticTime = -999f;
                if (_bossDeathRoutine != null)
                {
                    StopCoroutine(_bossDeathRoutine);   // restart no meio do espetáculo de morte
                    _bossDeathRoutine = null;
                }
            }
        }

        private void ResetDesaturation()
        {
            if (_defeatVolume != null) _defeatVolume.weight = 0f;
        }

        // ------------------------------------------------------------------ hit no boss

        private void PollBossDamage()
        {
            BossManager bm = BossManager.Instance;
            BossRuntime boss = bm != null ? bm.Current : null;

            if (!ReferenceEquals(boss, _trackedBoss))
            {
                _trackedBoss = boss;
                _lastBossHp = boss != null ? boss.Hp : 0f;
                ClearBossOverrides();
                _bossRenderers.Clear();
                if (boss != null) CacheBossRenderers();
                return;
            }
            if (boss == null) return;

            // FALLBACK: o evento OnBossElementalHit (W2-A) é a fonte rica de feedback; o
            // polling só age fora da janela de supressão — nunca flash/shake/pulso 2× por hit.
            if (boss.Hp < _lastBossHp - 0.01f
                && Time.unscaledTime - _lastBossPulseTime >= _bossPulseInterval
                && Time.unscaledTime - _lastElementalHitTime >= _eventFlashSuppressSeconds)
            {
                _lastBossPulseTime = Time.unscaledTime;
                Tween.ShakeCamera(0.5f, 0.12f);   // micro shake do hit
                StartBossFlash(NeutralHitRed, _bossFlashSeconds, emissionBoost: 2f);
                JuiceEvents.RaiseBossHitPulse(BossViewPosition());
                Core.Haptics.Light();             // tap leve por pulso de dano (rate-limited acima)
            }
            _lastBossHp = boss.Hp;
        }

        // O boss visual é instanciado a partir de BossConfigSO.prefab ("Boss_*"): busca
        // defensiva por nome de root — se a fase ainda não tem view, o shake fica sozinho.
        private void CacheBossRenderers()
        {
            Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r is ParticleSystemRenderer) continue;
                if (!r.transform.root.name.StartsWith("Boss", System.StringComparison.OrdinalIgnoreCase)) continue;
                _bossRenderers.Add(r);
            }
        }

        private Vector3 BossViewPosition()
        {
            if (_bossRenderers.Count > 0 && _bossRenderers[0] != null)
                return _bossRenderers[0].bounds.center;
            return CrowdAnchor.Position + Vector3.forward * 8f;   // arena fica à frente do exército
        }

        // flash COLORIDO via MPB: pulso emissivo + base color — SetPropertyBlock(null) no
        // fim devolve o material intocado (nunca instancia material, doc 12 §6.4).
        // Generalizado p/ missão Nota 10: vermelho neutro, COR DO ELEMENTO na fraqueza,
        // cinza apagado na resistência (emissionBoost baixo = "parede" sem brilho).
        private void StartBossFlash(Color flashColor, float seconds, float emissionBoost)
        {
            if (_bossFlashRoutine != null) StopCoroutine(_bossFlashRoutine);
            _bossFlashRoutine = StartCoroutine(FlashBoss(flashColor, seconds, emissionBoost));
        }

        private IEnumerator FlashBoss(Color flashColor, float seconds, float emissionBoost)
        {
            if (_bossRenderers.Count == 0) yield break;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            seconds = Mathf.Max(0.05f, seconds);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(elapsed / seconds);
                Color flash = Color.Lerp(Color.white, flashColor, 0.5f);
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, Color.Lerp(Color.white, flash, k));
                _mpb.SetColor(ColorId, Color.Lerp(Color.white, flash, k));
                _mpb.SetColor(EmissionColorId,
                              new Color(flashColor.r, flashColor.g, flashColor.b) * (emissionBoost * k));
                for (int i = 0; i < _bossRenderers.Count; i++)
                    if (_bossRenderers[i] != null) _bossRenderers[i].SetPropertyBlock(_mpb);
                yield return null;
            }

            ClearBossOverrides();
            _bossFlashRoutine = null;
        }

        private void ClearBossOverrides()
        {
            for (int i = 0; i < _bossRenderers.Count; i++)
                if (_bossRenderers[i] != null) _bossRenderers[i].SetPropertyBlock(null);
        }

        // ------------------------------------------------- missão Nota 10: fraqueza/morte

        private static readonly Color NeutralHitRed = new Color(1f, 0.1f, 0.1f);
        private static readonly Color ResistedGray = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color GoldFeedback = new Color(1f, 0.84f, 0.25f);   // mesmo dourado do Supply
        private static readonly Color BadDarkRed = new Color(0.55f, 0.08f, 0.08f);

        // Cor canônica de leitura do elemento (CANON §4): fogo laranja, gelo azul, raio
        // amarelo, veneno verde; os demais ganham tom próprio para o flash nunca mentir.
        private static Color ElementFlashColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1f, 0.45f, 0.10f);
                case ElementType.Ice: return new Color(0.25f, 0.65f, 1f);
                case ElementType.Lightning: return new Color(1f, 0.92f, 0.20f);
                case ElementType.Poison: return new Color(0.35f, 0.95f, 0.25f);
                case ElementType.Light: return new Color(1f, 0.95f, 0.70f);
                case ElementType.Shadow: return new Color(0.60f, 0.30f, 0.95f);
                case ElementType.Metal: return new Color(0.75f, 0.80f, 0.88f);
                case ElementType.Alien: return new Color(0.35f, 1f, 0.80f);
                default: return NeutralHitRed;
            }
        }

        // Golpe elemental JÁ CLASSIFICADO (WeaknessJudge, rate-limited ≥0,5 s na origem —
        // CONTRACT §5). O pulso de SFX (JuiceEvents) continua saindo daqui para o
        // AudioManager atual; o polling cala o próprio flash pela janela de supressão.
        private void HandleBossElementalHit(BossElementalHit hit)
        {
            _lastElementalHitTime = Time.unscaledTime;
            _lastBossPulseTime = Time.unscaledTime;   // alinha o rate-limit do pulso de SFX

            Vector3 pos = hit.position.sqrMagnitude > 0.01f ? hit.position : BossViewPosition();
            JuiceEvents.RaiseBossHitPulse(pos);

            switch (hit.relation)
            {
                case ElementRelation.Weakness:
                    // acertou a fraqueza: o boss ACENDE na cor do elemento. Fraqueza FORTE
                    // (dano alto) "estala" — flash mais brilhante + hitstop cosmético no lugar
                    // do shake leve; fraqueza fraca mantém o pulso suave de sempre. Um caminho
                    // OU o outro (nunca dois shakes/FOV empilhados no mesmo hit).
                    if (hit.damage >= _strongWeaknessDamage)
                    {
                        StartBossFlash(ElementFlashColor(hit.element), _bossFlashSeconds, emissionBoost: 3.5f);
                        ImpactPunch(0.8f);
                    }
                    else
                    {
                        StartBossFlash(ElementFlashColor(hit.element), _bossFlashSeconds, emissionBoost: 2.5f);
                        Tween.ShakeCamera(_weaknessShake, 0.15f);
                        Core.Haptics.Light();
                    }
                    break;
                case ElementRelation.Resisted:
                case ElementRelation.Immune:
                    // "bateu em parede": flash CINZA curto, sem brilho, SEM shake/vibração —
                    // o corpo do boss não responde, o jogador sente que o elemento está errado
                    StartBossFlash(ResistedGray, _resistedFlashSeconds, emissionBoost: 0.4f);
                    break;
                default:
                    // neutro: o mesmo feedback vermelho do polling (1 fonte por vez, nunca 2×)
                    StartBossFlash(NeutralHitRed, _bossFlashSeconds, emissionBoost: 2f);
                    Tween.ShakeCamera(0.5f, 0.12f);
                    Core.Haptics.Light();
                    break;
            }
        }

        private void HandleBossDied(BossDied died)
        {
            if (_bossDeathRoutine != null) StopCoroutine(_bossDeathRoutine);
            _bossDeathRoutine = StartCoroutine(BossDeathSpectacle(died));
        }

        // SEQUÊNCIA CINEMATOGRÁFICA da morte (missão Nota 10, frase central: "luta extremamente
        // satisfatória"): shake forte + zoom temporário no corpo (CameraRig.PunchFocus, camada
        // que se restaura sozinha — à prova da transição p/ Victory) + 2-3 bursts de moedas
        // staggered + vibração pesada. Tempo REAL (WaitForSecondsRealtime): o slow-mo canônico
        // do BossManager.Die não a alonga. 100% cosmética — estado é do BossManager/GameManager.
        private IEnumerator BossDeathSpectacle(BossDied died)
        {
            // payload sem posição (greybox/edge): ancora no view cacheado — nunca zoom no (0,0,0)
            Vector3 anchor = died.position.sqrMagnitude > 0.01f ? died.position : BossViewPosition();

            // GOLPE FINAL: hitstop cosmético no frame 0 (o "estalo" da morte) — soco de FOV
            // fundo + tranco seco. Vem ANTES da celebração sustentada para o jogador sentir o
            // peso do golpe; o slow-mo canônico (BossManager.Die) já roda por baixo.
            ImpactPunch(1f);
            Tween.ShakeCamera(_bossDeathShake, 0.6f);   // shake sustentado da celebração (canal próprio)
            Core.Haptics.Heavy();
            if (CameraRig.Instance != null)
                CameraRig.Instance.PunchFocus(anchor, 0.85f, 1.1f);

            // variante rara paga ×3 (RareBossMath): a chuva de moedas cresce junto
            int bursts = Mathf.Max(1, died.wasRare ? _bossDeathCoinBursts + 1 : _bossDeathCoinBursts);
            for (int i = 0; i < bursts; i++)
            {
                if (VFXManager.Instance != null)
                {
                    Vector3 jitter = new Vector3(Random.Range(-1.5f, 1.5f),
                                                 1f + 0.5f * i,
                                                 Random.Range(-0.5f, 0.5f));
                    VFXManager.Instance.PlayCoinBurst(anchor + jitter, died.wasRare ? 2 : 1);
                }
                yield return new WaitForSecondsRealtime(_bossDeathBurstStagger);
            }
            _bossDeathRoutine = null;
        }

        // Combo conquistado (ComboSystem, vitória): zoom-punch dosado pela "grandeza" do combo
        // + burst tintado por tipo no centróide + tap. O texto/celebração ("PERFECT GATE! +25")
        // é da Onda 3 (FeedbackTextController/ResultScreen) — aqui só o feel.
        private void HandleComboEarned(ComboEarned combo)
        {
            float weight = ComboPunchWeight(combo.kind);   // 1 = leve · 1.8 = clímax (BossBreaker/Clutch)
            PunchCameraFov(_comboFovPunch * weight, 0.25f);
            CrowdManager crowd = CrowdManager.Instance;
            Vector3 center = crowd != null ? crowd.Centroid : CrowdAnchor.Position;
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(center + Vector3.up * 1.4f, ComboBurstColor(combo.kind));
            // combos "grandes" merecem mais que um tap; os de rotina mantêm o toque leve
            if (weight >= 1.4f) Core.Haptics.Medium();
            else Core.Haptics.Light();
        }

        // Peso do soco por tipo de combo: os raros/clímax (derrubar o boss rápido, vencer no
        // fio) socam mais fundo; os de rotina ficam sutis para nunca enjoar (CANON: legível,
        // sem cansar). Append-safe: combo desconhecido cai no peso leve.
        private static float ComboPunchWeight(ComboKind kind)
        {
            switch (kind)
            {
                case ComboKind.BossBreaker: return 1.8f;
                case ComboKind.Clutch: return 1.7f;
                case ComboKind.Overkill: return 1.5f;
                case ComboKind.NoLoss: return 1.3f;
                case ComboKind.PerfectGate: return 1.2f;
                default: return 1f;   // WeaknessHit e demais: pulso leve
            }
        }

        // Cor de leitura por combo: dourado de proeza para os de exército/perfeição, tons
        // quentes/vivos para os de combate. Apenas o tint do burst — sem semântica de jogo.
        private static Color ComboBurstColor(ComboKind kind)
        {
            switch (kind)
            {
                case ComboKind.BossBreaker: return new Color(1f, 0.35f, 0.20f);   // laranja-fogo: derrubou o boss
                case ComboKind.Overkill: return new Color(1f, 0.20f, 0.35f);      // carmesim: destruição
                case ComboKind.Clutch: return new Color(0.30f, 0.95f, 1f);        // ciano-tensão: salvou no fio
                case ComboKind.WeaknessHit: return new Color(0.55f, 1f, 0.45f);   // verde-acerto
                default: return GoldFeedback;                                     // PerfectGate/NoLoss: dourado de proeza
            }
        }

        // Inimigo de pista morto: hordas caem em rajada — burst AGREGADO rate-limited
        // (≥0,15 s) e tap esporádico (≥0,5 s); nunca 1 partícula/vibração por inimigo.
        private void HandleTrackEnemyKilled(TrackEnemyKilled killed)
        {
            if (Time.unscaledTime - _lastTrackKillBurstTime >= _trackKillBurstInterval)
            {
                _lastTrackKillBurstTime = Time.unscaledTime;
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayPopBurst(killed.position + Vector3.up * 0.5f);
            }
            if (Time.unscaledTime - _lastTrackKillHapticTime >= _trackKillHapticInterval)
            {
                _lastTrackKillHapticTime = Time.unscaledTime;
                Core.Haptics.Light();
            }
        }

        // Wave limpa: fanfarra curta dourada no centro da wave (SFX/UI são das outras camadas).
        private void HandleEnemyWaveCleared(EnemyWaveCleared wave)
        {
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(wave.position + Vector3.up * 1f, GoldFeedback);
        }

        // Veredito da escolha de portal (ComboSystem→JuiceEvents): DOURADO no acerto — o
        // texto "BOA ESCOLHA!" é da Onda 3; aqui só o brilho de mundo.
        private void HandleGoodGateChoice(Vector3 worldPosition)
        {
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(worldPosition + Vector3.up * 1.2f, GoldFeedback);
            Core.Haptics.Light();
        }

        // Armadilha escolhida: vermelho-ESCURO + tranco curto — "doeu", sem punir a leitura.
        private void HandleBadGateChoice(Vector3 worldPosition)
        {
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayGateBurst(worldPosition + Vector3.up * 1.2f, BadDarkRed);
            Tween.ShakeCamera(0.5f, 0.12f);
        }

        // Zona de risco resolvida (RiskResolver): aposta paga = moedas + soco de FOV;
        // falhou = impacto seco vermelho-escuro (odds eram honestas, CANON §3.4).
        private void HandleRiskResolved(bool success, Vector3 worldPosition)
        {
            if (success)
            {
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayCoinBurst(worldPosition + Vector3.up * 1f, 1);
                PunchCameraFov(-3f, 0.25f);
                Core.Haptics.Medium();
            }
            else
            {
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayGateBurst(worldPosition + Vector3.up * 1f, BadDarkRed);
                Tween.ShakeCamera(0.8f, 0.2f);
                Core.Haptics.Light();
            }
        }

        // ------------------------------------------------------------------ câmera

        private float _baseFov = -1f;   // FOV de descanso, capturado 1× — punch sobreposto não deriva a base

        private void PunchCameraFov(float delta, float seconds)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_baseFov <= 0f) _baseFov = cam.fieldOfView;
            if (_fovPunchRoutine != null) Tween.Stop(_fovPunchRoutine);
            float baseFov = _baseFov;
            _fovPunchRoutine = Tween.Float(baseFov + delta, baseFov, seconds, Tween.Ease.OutCubic,
                                           v => { if (cam != null) cam.fieldOfView = v; });
        }

        // "Hitstop" cosmético (sem mexer no timeScale — ver header): tranco SECO de câmera +
        // soco de FOV fundo + vibração média, dosado por <paramref name="intensity"/> (0..1).
        // É o substituto do freeze: o jogador SENTE o impacto sem nenhuma janela de timeScale
        // que pudesse aninhar com o slow-mo de morte ou travar a luta a 8-10× nos testes.
        private void ImpactPunch(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity <= 0f) return;
            Tween.ShakeCamera(_impactShake * intensity, _impactShakeSeconds);
            PunchCameraFov(_impactFovPunch * intensity, 0.18f);
            Core.Haptics.Medium();
        }
    }
}
