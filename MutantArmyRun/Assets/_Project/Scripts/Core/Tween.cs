using System;
using System.Collections;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Utilitário estático de tween por corrotina — ZERO dependência externa (regra dura:
    /// sem DOTween/sem pacote novo). Um único runner oculto (DontDestroyOnLoad) hospeda as
    /// corrotinas; todo tween é null-safe contra alvo destruído no meio (cena trocou, pool
    /// reciclou) e roda por padrão em tempo UNSCALED — juice continua vivo durante o slow
    /// motion canônico do golpe final (doc 12 §3.1).
    /// ShakeCamera perturba a ROTAÇÃO da câmera principal: o CameraRig (doc 12 §4.12) só
    /// escreve position no LateUpdate, então o shake nunca briga com o follow.
    /// </summary>
    public static class Tween
    {
        public enum Ease
        {
            Linear,
            OutCubic,
            OutBack,
            OutElastic
        }

        // ------------------------------------------------------------------ runner

        private sealed class TweenRunner : MonoBehaviour { }

        private static TweenRunner s_runner;

        private static TweenRunner Runner
        {
            get
            {
                if (s_runner == null)
                {
                    var go = new GameObject("[TweenRunner]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    s_runner = go.AddComponent<TweenRunner>();
                }
                return s_runner;
            }
        }

        /// <summary>Cancela um tween retornado por qualquer método daqui. Null-safe.</summary>
        public static void Stop(Coroutine routine)
        {
            if (routine != null && s_runner != null) s_runner.StopCoroutine(routine);
        }

        // ------------------------------------------------------------------ easings

        public static float Evaluate(Ease ease, float k)
        {
            k = Mathf.Clamp01(k);
            switch (ease)
            {
                case Ease.OutCubic:
                {
                    float inv = 1f - k;
                    return 1f - inv * inv * inv;
                }
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float p = k - 1f;
                    return 1f + c3 * p * p * p + c1 * p * p;
                }
                case Ease.OutElastic:
                {
                    if (k <= 0f) return 0f;
                    if (k >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * k) * Mathf.Sin((k * 10f - 0.75f) * c4) + 1f;
                }
                default:
                    return k;
            }
        }

        // ------------------------------------------------------------------ genérico

        /// <summary>
        /// Anima um float de <paramref name="from"/> a <paramref name="to"/> chamando
        /// onUpdate por frame — base de FOV punch, Volume.weight, alpha etc.
        /// </summary>
        public static Coroutine Float(float from, float to, float seconds, Ease ease,
                                      Action<float> onUpdate, bool unscaled = true, Action onComplete = null)
        {
            if (onUpdate == null) return null;
            return Runner.StartCoroutine(FloatRoutine(from, to, seconds, ease, onUpdate, unscaled, onComplete));
        }

        private static IEnumerator FloatRoutine(float from, float to, float seconds, Ease ease,
                                                Action<float> onUpdate, bool unscaled, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                onUpdate(Mathf.LerpUnclamped(from, to, Evaluate(ease, k)));
                yield return null;
            }
            onUpdate(to);
            if (onComplete != null) onComplete();
        }

        // ------------------------------------------------------------------ transform

        /// <summary>
        /// Pop de aparição: escala 0 → escala ATUAL com OutBack (overshoot). Captura a
        /// escala no início — respeita contra-escala de rótulos (GateView) e tamanhos custom.
        /// </summary>
        public static Coroutine ScalePop(Transform target, float seconds = 0.35f, bool unscaled = true)
        {
            if (target == null) return null;
            return Runner.StartCoroutine(ScalePopRoutine(target, seconds, unscaled));
        }

        private static IEnumerator ScalePopRoutine(Transform target, float seconds, bool unscaled)
        {
            Vector3 baseScale = target.localScale;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (target == null) yield break;   // alvo morreu no meio (pool/cena)
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                target.localScale = baseScale * Evaluate(Ease.OutBack, k);
                yield return null;
            }
            if (target != null) target.localScale = baseScale;
        }

        /// <summary>Punch: escala atual → ×(1+punch) → volta, com decaimento elástico.</summary>
        public static Coroutine PunchScale(Transform target, float punch = 0.25f,
                                           float seconds = 0.25f, bool unscaled = true)
        {
            if (target == null) return null;
            return Runner.StartCoroutine(PunchScaleRoutine(target, punch, seconds, unscaled));
        }

        private static IEnumerator PunchScaleRoutine(Transform target, float punch, float seconds, bool unscaled)
        {
            Vector3 baseScale = target.localScale;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (target == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                // envelope: sobe rápido, decai senoidal — punch clássico de contador de HUD
                float strength = punch * (1f - k) * Mathf.Sin(k * Mathf.PI * 2.5f + Mathf.PI * 0.4f);
                target.localScale = baseScale * (1f + Mathf.Max(0f, strength));
                yield return null;
            }
            if (target != null) target.localScale = baseScale;
        }

        /// <summary>Move até <paramref name="to"/> (espaço de mundo) com easing.</summary>
        public static Coroutine MoveTo(Transform target, Vector3 to, float seconds,
                                       Ease ease = Ease.OutCubic, bool unscaled = true, Action onComplete = null)
        {
            if (target == null) return null;
            return Runner.StartCoroutine(MoveToRoutine(target, to, seconds, ease, unscaled, onComplete));
        }

        private static IEnumerator MoveToRoutine(Transform target, Vector3 to, float seconds,
                                                 Ease ease, bool unscaled, Action onComplete)
        {
            Vector3 from = target.position;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (target == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                target.position = Vector3.LerpUnclamped(from, to, Evaluate(ease, k));
                yield return null;
            }
            if (target != null)
            {
                target.position = to;
                if (onComplete != null) onComplete();
            }
        }

        // ------------------------------------------------------------------ shake de câmera

        // Modelo de "trauma" acumulável: shakes simultâneos somam amplitude em vez de
        // disputar a rotação — um único loop aplica offset sobre a rotação base e a
        // restaura ao terminar. CameraRig nunca escreve rotação (doc 12 §4.12).
        private static float s_shakeAmplitude;      // graus
        private static float s_shakeUntil;          // Time.unscaledTime limite
        private static bool s_shakeLoopAlive;

        /// <summary>
        /// Shake da câmera principal: <paramref name="amplitude"/> em GRAUS de perturbação
        /// rotacional, por <paramref name="seconds"/>. Chamadas concorrentes acumulam.
        /// </summary>
        public static void ShakeCamera(float amplitude, float seconds)
        {
            if (amplitude <= 0f || seconds <= 0f) return;
            s_shakeAmplitude = Mathf.Min(6f, s_shakeAmplitude + amplitude);   // teto anti-vertigem
            s_shakeUntil = Mathf.Max(s_shakeUntil, Time.unscaledTime + seconds);
            if (!s_shakeLoopAlive) Runner.StartCoroutine(ShakeLoop());
        }

        private static IEnumerator ShakeLoop()
        {
            s_shakeLoopAlive = true;
            Camera cam = Camera.main;
            if (cam == null)
            {
                s_shakeLoopAlive = false;
                s_shakeAmplitude = 0f;
                yield break;
            }

            Transform t = cam.transform;
            Quaternion baseRotation = t.localRotation;
            float seed = UnityEngine.Random.value * 100f;

            while (Time.unscaledTime < s_shakeUntil)
            {
                if (t == null)
                {
                    s_shakeLoopAlive = false;
                    s_shakeAmplitude = 0f;
                    yield break;
                }
                float remaining01 = Mathf.Clamp01((s_shakeUntil - Time.unscaledTime) / 0.5f);
                float a = s_shakeAmplitude * remaining01;   // decaimento ao fim da janela
                float time = Time.unscaledTime * 28f;
                float pitch = (Mathf.PerlinNoise(seed, time) * 2f - 1f) * a;
                float roll = (Mathf.PerlinNoise(seed + 17f, time) * 2f - 1f) * a;
                t.localRotation = baseRotation * Quaternion.Euler(pitch, 0f, roll);
                yield return null;
            }

            if (t != null) t.localRotation = baseRotation;
            s_shakeAmplitude = 0f;
            s_shakeLoopAlive = false;
        }
    }
}
