namespace MutantArmy.Domain
{
    /// <summary>
    /// Política de interstitial do CANON §11, extraída do AdsManager (doc 12 §4.8) como
    /// função pura para ser testável: fase >= 6 · máx. 1 a cada 3 fases · NUNCA após
    /// 2 derrotas seguidas · desligada por "Remover Anúncios". Os defaults de minLevel e
    /// levelGap são os mesmos das chaves de Remote Config "inter_min_level"/"inter_level_gap" —
    /// o AdsManager passa os valores do RC por aqui.
    /// </summary>
    public static class InterstitialPolicy
    {
        public static bool ShouldShow(bool adsRemoved, int highestLevelCleared, int levelsSinceInterstitial,
                                      int consecutiveDefeats, int minLevel = 6, int levelGap = 3)
            => !adsRemoved && highestLevelCleared >= minLevel
               && levelsSinceInterstitial >= levelGap && consecutiveDefeats < 2;
    }
}
