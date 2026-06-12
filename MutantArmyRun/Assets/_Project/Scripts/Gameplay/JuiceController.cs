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
    /// Orquestrador de JUICE da corrida (doc 09 §4 / doc 14 §7) — 100% cosmético, assina o
    /// bus (GameEvents) e NUNCA muta estado de jogo. Reações:
    /// · portal consumido → ScalePop no rótulo + flash no frame + burst tintado (VFXManager);
    /// · multiplicação ×2+ → cascata de pops com stagger sobre a formação;
    /// · estouro de Supply → burst dourado no centróide (as moedas até o HUD são do
    ///   FloatingTextSpawner, camada UI);
    /// · dano no boss → micro shake + flash vermelho (MaterialPropertyBlock — nunca
    ///   instancia material) + JuiceEvents p/ o SFX (Services não é visível daqui, §2.3);
    /// · fase de vida do boss → shake forte + zoom punch de FOV;
    /// · vitória → confete; derrota → dessaturação rápida (Volume.weight 0→1).
    /// Dano no boss é detectado por POLLING do BossRuntime.Hp: o combate agregado roda a
    /// 10 Hz contínuo (doc 12 §4.4) — não existe (nem deve existir) 1 evento por hit; a
    /// regra "UI sem polling" (§3.2) é sobre UI/dados, não sobre leitura cosmética local.
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

        private bool _subscribedToGameManager;
        private BossRuntime _trackedBoss;
        private float _lastBossHp;
        private float _lastBossPulseTime;
        private readonly List<Renderer> _bossRenderers = new List<Renderer>(8);
        private MaterialPropertyBlock _mpb;
        private Coroutine _bossFlashRoutine;
        private Coroutine _fovPunchRoutine;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
            GameEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            TrySubscribeGameManager();
        }

        private void OnDisable()
        {
            // bus estático sobrevive a cenas — sempre limpar (doc 12 §3.2)
            GameEvents.OnGateConsumed -= HandleGateConsumed;
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            if (GameManager.Instance != null)
                GameManager.Instance.StateEntered -= HandleStateEntered;
            _subscribedToGameManager = false;
            StopAllCoroutines();
            _bossFlashRoutine = null;
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

            if (gate.gateType == GateType.Multiply && gate.value >= 2f)
                StartCoroutine(CascadePops(result.newCount));
        }

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

        private void HandleBossPhaseChanged(BossPhase phase)
        {
            Tween.ShakeCamera(2.5f, 0.5f);   // shake FORTE: virada de fase é evento maiúsculo
            PunchCameraFov(-7f, 0.5f);
        }

        private void HandleLevelFinished(LevelResult result)
        {
            if (result.won)
            {
                if (VFXManager.Instance != null) VFXManager.Instance.PlayConfetti();
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
            // retry/nova corrida na MESMA cena (soft reset §4.11): cor volta ao normal
            if (state == GameState.Running || state == GameState.BossScout)
            {
                ResetDesaturation();
                _trackedBoss = null;
                ClearBossOverrides();
                _bossRenderers.Clear();
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

            if (boss.Hp < _lastBossHp - 0.01f
                && Time.unscaledTime - _lastBossPulseTime >= _bossPulseInterval)
            {
                _lastBossPulseTime = Time.unscaledTime;
                Tween.ShakeCamera(0.5f, 0.12f);   // micro shake do hit
                if (_bossFlashRoutine != null) StopCoroutine(_bossFlashRoutine);
                _bossFlashRoutine = StartCoroutine(FlashBossRed());
                JuiceEvents.RaiseBossHitPulse(BossViewPosition());
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

        // flash vermelho via MPB: pulso emissivo + base color — SetPropertyBlock(null) no
        // fim devolve o material intocado (nunca instancia material, doc 12 §6.4)
        private IEnumerator FlashBossRed()
        {
            if (_bossRenderers.Count == 0) yield break;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            float elapsed = 0f;
            while (elapsed < _bossFlashSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(elapsed / _bossFlashSeconds);
                var flash = Color.Lerp(Color.white, new Color(1f, 0.1f, 0.1f), 0.5f);
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, Color.Lerp(Color.white, flash, k));
                _mpb.SetColor(ColorId, Color.Lerp(Color.white, flash, k));
                _mpb.SetColor(EmissionColorId, new Color(2f * k, 0.1f * k, 0.1f * k));
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
    }
}
