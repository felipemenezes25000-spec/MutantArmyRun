using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Utilidades de UI: safe area (resolvida 1× no root, doc 12 §4.13), fade por
    /// unscaled time e formatação compacta de contadores (doc 09 §4.2).
    /// </summary>
    public static class UIUtils
    {
        /// <summary>
        /// Ancora o RectTransform na safe area do device (notch/punch-hole).
        /// Chamado UMA vez no root pelo UIManager.Init — nunca por tela, nunca por frame.
        /// </summary>
        public static void ResizeToSafeArea(RectTransform root)
        {
            if (root == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            root.anchorMin = anchorMin;
            root.anchorMax = anchorMax;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Fade de CanvasGroup em unscaled time — coreografias de UI nunca dependem
        /// de timeScale (doc 12 §4.13: slow motion e resultado não podem travar a UI).
        /// </summary>
        public static IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float seconds, Action onDone)
        {
            if (group == null)
            {
                if (onDone != null) onDone();
                yield break;
            }

            group.alpha = from;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(t / seconds) : 1f;
                group.alpha = Mathf.Lerp(from, to, k);
                yield return null;
            }
            group.alpha = to;
            if (onDone != null) onDone();
        }

        /// <summary>
        /// Contador compacto do HUD (doc 09 §4.2): acima de 999 exibe "1,2K"
        /// (vírgula decimal PT). Escreve no StringBuilder cacheado — zero alloc de string.
        /// </summary>
        public static void AppendCompactCount(StringBuilder sb, int count)
        {
            if (sb == null) return;
            if (count < 1000)
            {
                sb.Append(count);
                return;
            }

            int thousands = count / 1000;
            int tenths = (count % 1000) / 100;
            sb.Append(thousands);
            if (tenths > 0)
            {
                sb.Append(',');
                sb.Append(tenths);
            }
            sb.Append('K');
        }
    }
}
