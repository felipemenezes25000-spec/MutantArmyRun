using UnityEditor;
using UnityEngine;
using MutantArmy.Domain;
using MutantArmy.Meta;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Cheats de QA (doc 12 §8). Regra dura: cheats usam os MESMOS funis de mutação do
    /// jogo — moedas entram por EconomySystem.Earn, NUNCA escrevendo no save direto
    /// (cheat que bypassa o funil vira fonte de bug que o jogo real não tem).
    /// </summary>
    public static class CheatsMenu
    {
        private const string MenuCoins = "MAR Tools/Cheats/Dar 10k Moedas";
        private const string MenuGems = "MAR Tools/Cheats/Dar 100 Gemas";
        private const string MenuXp = "MAR Tools/Cheats/Dar 500 XP";

        [MenuItem(MenuCoins)]
        private static void GiveCoins()
        {
            GiveCurrency(CurrencyType.Coin, 10000);
        }

        [MenuItem(MenuCoins, true)]
        private static bool GiveCoinsValidate()
        {
            return Application.isPlaying;
        }

        [MenuItem(MenuGems)]
        private static void GiveGems()
        {
            GiveCurrency(CurrencyType.Gem, 100);
        }

        [MenuItem(MenuGems, true)]
        private static bool GiveGemsValidate()
        {
            return Application.isPlaying;
        }

        [MenuItem(MenuXp)]
        private static void GiveXp()
        {
            GiveCurrency(CurrencyType.Xp, 500);
        }

        [MenuItem(MenuXp, true)]
        private static bool GiveXpValidate()
        {
            return Application.isPlaying;
        }

        private static void GiveCurrency(CurrencyType type, long amount)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: cheats só funcionam em play mode (o funil de economia precisa estar vivo).");
                return;
            }
            if (EconomySystem.Instance == null)
            {
                Debug.LogWarning("MAR Tools: EconomySystem ainda não inicializado pelo bootstrap (doc 12 §3.3).");
                return;
            }
            EconomySystem.Instance.Earn(type, amount, "cheat");
            Debug.Log("MAR Tools: cheat aplicado — +" + amount + " " + type + " via EconomySystem.Earn.");
        }
    }
}
