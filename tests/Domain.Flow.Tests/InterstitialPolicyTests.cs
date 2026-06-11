using MutantArmy.Domain;
using Xunit;

namespace Domain.Flow.Tests
{
    public class InterstitialPolicyTests
    {
        // CANON §11: interstitial só a partir da fase 6 · máx. 1 a cada 3 fases ·
        // NUNCA após 2 derrotas seguidas · desligado com Remover Anúncios.

        [Fact]
        public void CasoLiberado_TodasAsCondicoesOk_RetornaTrue()
        {
            Assert.True(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 6,
                levelsSinceInterstitial: 3,
                consecutiveDefeats: 0));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void NuncaAntesDaFase6_MesmoComGapEnorme(int highestLevelCleared)
        {
            Assert.False(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: highestLevelCleared,
                levelsSinceInterstitial: 99,
                consecutiveDefeats: 0));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void NuncaComGapMenorQue3Fases(int levelsSinceInterstitial)
        {
            Assert.False(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 10,
                levelsSinceInterstitial: levelsSinceInterstitial,
                consecutiveDefeats: 0));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(10)]
        public void NuncaApos2DerrotasSeguidas(int consecutiveDefeats)
        {
            // Jogador frustrado não vê interstitial — regra inegociável do CANON §11.
            Assert.False(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 10,
                levelsSinceInterstitial: 5,
                consecutiveDefeats: consecutiveDefeats));
        }

        [Fact]
        public void UmaDerrotaSo_NaoBloqueia()
        {
            Assert.True(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 10,
                levelsSinceInterstitial: 5,
                consecutiveDefeats: 1));
        }

        [Fact]
        public void NuncaComAdsRemoved_MesmoComTudoLiberado()
        {
            // IAP "Remover Anúncios" (CANON §11) desliga interstitial incondicionalmente.
            Assert.False(InterstitialPolicy.ShouldShow(
                adsRemoved: true,
                highestLevelCleared: 100,
                levelsSinceInterstitial: 100,
                consecutiveDefeats: 0));
        }

        [Fact]
        public void ParametrosRemoteConfig_MinLevelSubstituivel()
        {
            // Frequência 100% controlada por Remote Config (CANON §11): minLevel vira 10.
            Assert.False(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 8,
                levelsSinceInterstitial: 5,
                consecutiveDefeats: 0,
                minLevel: 10));

            Assert.True(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 10,
                levelsSinceInterstitial: 5,
                consecutiveDefeats: 0,
                minLevel: 10));
        }

        [Fact]
        public void ParametrosRemoteConfig_LevelGapSubstituivel()
        {
            Assert.False(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 10,
                levelsSinceInterstitial: 4,
                consecutiveDefeats: 0,
                levelGap: 5));

            Assert.True(InterstitialPolicy.ShouldShow(
                adsRemoved: false,
                highestLevelCleared: 10,
                levelsSinceInterstitial: 5,
                consecutiveDefeats: 0,
                levelGap: 5));
        }

        [Fact]
        public void Fronteiras_Defaults_Fase6EGap3_SaoExatamenteOLimiteLiberado()
        {
            // Fase 5 + gap 3 → bloqueado; fase 6 + gap 2 → bloqueado; fase 6 + gap 3 → liberado.
            Assert.False(InterstitialPolicy.ShouldShow(false, 5, 3, 0));
            Assert.False(InterstitialPolicy.ShouldShow(false, 6, 2, 0));
            Assert.True(InterstitialPolicy.ShouldShow(false, 6, 3, 0));
        }
    }
}
