using System;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Orquestração de ads (doc 12 §4.8). O SDK mora atrás do IAdsProvider (Null no MVP;
    /// MockAdsProvider simula ads em dev para testar DOBRAR/REVIVE — troca de componente
    /// no prefab [Services], decisão de wiring da Onda 4);
    /// a POLÍTICA de interstitial mora no Domain (InterstitialPolicy) — este manager só
    /// junta estado + parâmetros de Remote Config e delega.
    ///
    /// Estado de pacing (CANON §11) vem do save. Como Services não referencia Meta
    /// (doc 12 §2.3), a leitura tem dois modos:
    /// - LIGADO (caminho de produção): o Init faz BindSaveState com a INSTÂNCIA viva do
    ///   save publicada no blackboard do GameBootstrap (SaveData é Domain — visível dos
    ///   dois lados; Save roda antes na ordem §3.3) e o manager passa a ler e zerar o
    ///   pacing direto no save (doc 12 §4.8);
    /// - ESPELHO (fallback sem bind): adsRemoved vem do blackboard do GameBootstrap e os
    ///   contadores são reconstruídos dos eventos de fim de fase — as MESMAS transições
    ///   que o SaveSystem.RecordLevelEnd aplica. Contadores nascem zerados no boot, o que
    ///   só ATRASA interstitial (nunca viola o "máx. 1 a cada 3" nem o "nunca após 2
    ///   derrotas": com gap ≥ 3 já houve fim-de-fase em sessão suficiente para o espelho
    ///   igualar o save nos campos que bloqueiam).
    /// </summary>
    public class AdsManager : MonoBehaviour, IInitializable
    {
        public static AdsManager Instance { get; private set; }

        private IAdsProvider _provider;

        // Modo ligado (autoritativo)
        private SaveData _save;
        private Action _markSaveDirty;

        // Modo espelho (fallback)
        private int _mirrorHighestLevelCleared;
        private int _mirrorLevelsSinceInterstitial;
        private int _mirrorConsecutiveDefeats;

        private int _sessionInterstitialCount;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable.</summary>
        public void Init()
        {
            Init(ResolveProvider());
        }

        /// <summary>Overload com provider explícito — testes injetam um fake determinístico.</summary>
        public void Init(IAdsProvider provider)
        {
            Instance = this;
            _provider = provider;

            // Modo LIGADO: o SaveSystem (1º da ordem §3.3) já publicou a instância viva do
            // save no blackboard — pacing lê/zera direto no save (doc 12 §4.8).
            if (GameBootstrap.Current != null)
            {
                BindSaveState(GameBootstrap.Current.Save, GameBootstrap.Current.MarkSaveDirty);
                // Hooks de rewarded no blackboard do Core: a UI (ResultScreen/Revive) não
                // referencia Services (doc 12 §2.3) — consome ads por aqui.
                GameBootstrap.Current.RewardedAdReady = () => IsRewardedReady;
                GameBootstrap.Current.ShowRewardedAd = ShowRewarded;
            }

            // "UI/Ads/Analytics reagem" ao fim de fase (doc 12 §4.1): o pacing roda por
            // evento — Gameplay/UI nunca chamam Services diretamente (§2.3).
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnLevelFinished += HandleLevelFinished;
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelFinished -= HandleLevelFinished;
        }

        /// <summary>
        /// Liga o manager ao estado persistente — chamado pelo próprio Init via blackboard
        /// do GameBootstrap (produção) ou diretamente por testes/tooling. SaveData é classe:
        /// a referência é a mesma instância viva do SaveSystem.
        /// </summary>
        public void BindSaveState(SaveData saveData, Action markDirty)
        {
            _save = saveData;
            _markSaveDirty = markDirty;
        }

        /// <summary>UI esconde o botão de rewarded quando false (nunca botão morto, doc 12 §7.3).</summary>
        public bool IsRewardedReady => _provider != null && _provider.IsRewardedReady;

        /// <summary>
        /// Placements canônicos em <see cref="AdPlacement"/> (CANON §11). O callback SEMPRE
        /// responde: true = recompensa concedida; false = indisponível/abandonado.
        /// </summary>
        public void ShowRewarded(string placement, Action<bool> onResult)
        {
            if (!IsRewardedReady)
            {
                if (onResult != null) onResult(false);
                return;
            }
            if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.LogRewardedShown(placement);
            _provider.ShowRewarded(placement, granted =>
            {
                if (granted && AnalyticsManager.Instance != null)
                    AnalyticsManager.Instance.LogRewardedCompleted(placement);
                if (onResult != null) onResult(granted);
            });
        }

        /// <summary>
        /// CANON §11: fase ≥ 6 · máx. 1 a cada 3 fases · NUNCA após 2 derrotas seguidas ·
        /// desligado por Remover Anúncios · 100% operável por Remote Config. A decisão é
        /// do Domain.InterstitialPolicy — testada sem cena.
        /// </summary>
        public void MaybeShowInterstitial()
        {
            if (_provider == null) return;

            bool adsRemoved = _save != null
                ? _save.adsRemoved
                : GameBootstrap.Current != null && GameBootstrap.Current.AdsRemoved;
            int highestLevelCleared = _save != null ? _save.highestLevelCleared : _mirrorHighestLevelCleared;
            int levelsSince = _save != null ? _save.levelsSinceInterstitial : _mirrorLevelsSinceInterstitial;
            int consecutiveDefeats = _save != null ? _save.consecutiveDefeats : _mirrorConsecutiveDefeats;

            int minLevel = GetRcInt(RcKeys.InterMinLevel, 6);
            int levelGap = GetRcInt(RcKeys.InterLevelGap, 3);

            if (!InterstitialPolicy.ShouldShow(adsRemoved, highestLevelCleared, levelsSince,
                                               consecutiveDefeats, minLevel, levelGap))
                return;

            // Pacing zera ANTES de exibir — callback de fechamento de SDK não é confiável.
            if (_save != null)
            {
                _save.levelsSinceInterstitial = 0;
                if (_markSaveDirty != null) _markSaveDirty();
            }
            else
            {
                _mirrorLevelsSinceInterstitial = 0;
            }

            _sessionInterstitialCount++;
            _provider.ShowInterstitial();
            if (AnalyticsManager.Instance != null)
                AnalyticsManager.Instance.LogInterstitialShown(levelsSince, _sessionInterstitialCount);
        }

        /// <summary>
        /// Espelha as MESMAS transições do SaveSystem.RecordLevelEnd. Em modo ligado o
        /// save já foi atualizado neste frame (LevelEndRecorder roda ANTES do Raise —
        /// doc 12 §4.1), então aqui só decide o interstitial.
        /// </summary>
        private void HandleLevelFinished(LevelResult r)
        {
            if (_save == null)
            {
                if (r.won)
                {
                    if (r.levelIndex > _mirrorHighestLevelCleared) _mirrorHighestLevelCleared = r.levelIndex;
                    _mirrorConsecutiveDefeats = 0;
                }
                else
                {
                    _mirrorConsecutiveDefeats++;
                }
                _mirrorLevelsSinceInterstitial++;
            }
            MaybeShowInterstitial();
        }

        private static int GetRcInt(string key, int fallback)
            => RemoteConfigManager.Instance != null
                ? RemoteConfigManager.Instance.GetInt(key, fallback)
                : fallback;

        private static IAdsProvider ResolveProvider()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IAdsProvider>(true)
                : null;
        }
    }
}
