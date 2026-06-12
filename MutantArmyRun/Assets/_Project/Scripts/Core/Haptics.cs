using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Vibração tátil do game feel (doc 14 §5) — hooks PREPARADOS mas no-op fora de mobile.
    /// O projeto roda em build Windows/headless (autoteste do DevScreenshotRig); chamar
    /// <c>Handheld.Vibrate</c> num PC é silencioso, mas o pré-processador garante que o
    /// código mobile só exista em Android/iOS, mantendo o desktop 100% inerte e os testes
    /// determinísticos. Respeita a preferência <c>SaveData.hapticsOn</c> lida do blackboard
    /// do Core (GameBootstrap.Current.Save) — Core não conhece Meta/SaveSystem (§2.3), mas o
    /// SaveData é Domain, visível daqui. Intensidade é cosmética: o Unity built-in só liga/
    /// desliga, então diferenciamos "leve" vs "forte" pela duração no Android (vibrador real)
    /// e caímos no Vibrate único em iOS.
    /// </summary>
    public static class Haptics
    {
        public enum Strength
        {
            Light,   // hit/feedback pequeno
            Medium,  // portal forte / multiplicação grande
            Heavy    // virada de fase do boss / golpe final
        }

        /// <summary>Vibração curta de feedback (hit no boss, pop). No-op fora de mobile/desabilitada.</summary>
        public static void Light() => Vibrate(Strength.Light);

        /// <summary>Vibração média (portal forte, multiplicação ×3/×5, mutação).</summary>
        public static void Medium() => Vibrate(Strength.Medium);

        /// <summary>Vibração forte (virada de fase do boss, golpe final).</summary>
        public static void Heavy() => Vibrate(Strength.Heavy);

        public static void Vibrate(Strength strength)
        {
            if (!Enabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrate(strength);
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS via Unity built-in só faz a vibração padrão do sistema; o tap háptico fino
            // (UIImpactFeedbackGenerator) exigiria plugin nativo — fora do MVP. Single buzz.
            Handheld.Vibrate();
#else
            // Desktop/editor/headless: hook inerte. Mantido para o call-site ficar pronto —
            // trocar por plugin nativo no futuro não toca em quem chama (JuiceController).
            _ = strength;
#endif
        }

        // Preferência do jogador + plataforma mobile: o gate único de "pode vibrar?".
        private static bool Enabled
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                GameBootstrap root = GameBootstrap.Current;
                // Sem save publicado ainda (boot muito cedo): assume ligado — default canônico.
                return root == null || root.Save == null || root.Save.hapticsOn;
#else
                return false;
#endif
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: o AndroidJavaObject do Vibrator aceita duração em ms — é assim que
        // diferenciamos as intensidades sem amplitude (que exige API 26+ e VibrationEffect).
        private static AndroidJavaObject s_vibrator;

        private static void AndroidVibrate(Strength strength)
        {
            long ms = strength == Strength.Heavy ? 60L : strength == Strength.Medium ? 30L : 12L;
            try
            {
                if (s_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        s_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }
                if (s_vibrator != null) s_vibrator.Call("vibrate", ms);
            }
            catch
            {
                // dispositivo sem vibrador / permissão ausente: degrada para o built-in
                Handheld.Vibrate();
            }
        }
#endif
    }
}
