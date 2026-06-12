using System;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// IAP sem SDK (doc 12 §7.4): o RevenueCat entra pós-instalação do Unity atrás deste
    /// manager, sem tocar nos consumidores. No MVP o manager é a fonte de verdade dos
    /// ENTITLEMENTS lidos do save (no_ads / season_pass) e do estado da Oferta Inicial
    /// (48 h desde firstLaunchUnixUtc — CANON §11); compra real exige SDK e responde false.
    /// Estado persistente entra por BindSaveState (SaveData é Domain — visível dos dois
    /// lados da fronteira Meta/Services); sem bind, cai no blackboard do GameBootstrap.
    /// </summary>
    public class IAPManager : MonoBehaviour, IInitializable
    {
        public static IAPManager Instance { get; private set; }

        // Entitlements canônicos do RevenueCat (doc 12 §7.4).
        public const string EntitlementNoAds = "no_ads";
        public const string EntitlementSeasonPass = "season_pass";

        // Ids de produto da taxonomia (doc 11 §4.6) — sem string mágica espalhada.
        public const string ProductRemoveAds = "remove_ads_499";
        public const string ProductStarterOffer = "starter_offer_299";
        public const string ProductSeasonPass = "season_pass_699";

        // Estados da Oferta Inicial persistidos em SaveData.starterOfferState (doc 12 §4.7).
        private const string OfferEligible = "eligible";
        private const string OfferShown = "shown";
        private const string OfferPurchased = "purchased";
        private const string OfferExpired = "expired";
        private const long StarterOfferWindowSeconds = 48L * 60L * 60L;   // 48 h (CANON §11)

        private SaveData _save;
        private Action _markSaveDirty;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable — não bloqueia.</summary>
        public void Init()
        {
            Instance = this;
            // Entitlements e Oferta Inicial leem o save vivo do blackboard (Save roda
            // antes na ordem §3.3); sem bootstrap (teste), o bind fica a cargo do teste.
            if (GameBootstrap.Current != null)
            {
                BindSaveState(GameBootstrap.Current.Save, GameBootstrap.Current.MarkSaveDirty);

                // Publica os hooks de IAP no blackboard: a UI (Loja/Passe) lê ownership e dispara a
                // compra por aqui — UI não referencia Services (doc 12 §2.3). Com provider Null o
                // Purchase responde false e a tela mantém o selo "EM BREVE" (doc 12 §7.4).
                GameBootstrap.Current.SeasonPassOwned = () => HasSeasonPass;
                GameBootstrap.Current.PurchaseProduct = Purchase;
            }
        }

        /// <summary>Liga o manager ao estado persistente (mesma instância viva do SaveSystem).</summary>
        public void BindSaveState(SaveData saveData, Action markDirty)
        {
            _save = saveData;
            _markSaveDirty = markDirty;
        }

        // ---- Entitlements ----

        public bool HasNoAds
        {
            get
            {
                if (_save != null) return _save.adsRemoved;
                return GameBootstrap.Current != null && GameBootstrap.Current.AdsRemoved;
            }
        }

        public bool HasSeasonPass
        {
            get
            {
                if (_save == null || !_save.seasonPassActive) return false;
                // Expiração 0 = sem data registrada (concessão local de teste): vale até sync real.
                return _save.seasonPassExpiryUnix == 0
                    || _save.seasonPassExpiryUnix > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        public bool HasEntitlement(string entitlementId)
        {
            switch (entitlementId)
            {
                case EntitlementNoAds: return HasNoAds;
                case EntitlementSeasonPass: return HasSeasonPass;
                default: return false;
            }
        }

        // ---- Oferta Inicial (CANON §11: US$ 2,99, 1×, primeiras 48 h) ----

        public bool IsStarterOfferAvailable()
        {
            if (_save == null) return false;
            RefreshStarterOfferState();
            return _save.starterOfferState == OfferEligible || _save.starterOfferState == OfferShown;
        }

        public void MarkStarterOfferShown()
        {
            if (_save == null || _save.starterOfferState != OfferEligible) return;
            _save.starterOfferState = OfferShown;
            MarkDirty();
        }

        private void RefreshStarterOfferState()
        {
            if (_save.starterOfferState != OfferEligible && _save.starterOfferState != OfferShown) return;
            long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _save.firstLaunchUnixUtc;
            if (elapsed > StarterOfferWindowSeconds)
            {
                _save.starterOfferState = OfferExpired;
                MarkDirty();
            }
        }

        // ---- Compra / restore (stubs sem SDK) ----

        /// <summary>
        /// Sem SDK: loga o início do funil e responde false — a loja mostra estado
        /// "indisponível/reconectando" (doc 12 §7.4), nunca um botão que finge comprar.
        /// </summary>
        public void Purchase(string productId, float priceUsd, string sourceScreen, Action<bool> onResult)
        {
            if (AnalyticsManager.Instance != null)
                AnalyticsManager.Instance.LogPurchaseStarted(productId, priceUsd, sourceScreen);
            if (onResult != null) onResult(false);
        }

        public void RestorePurchases(Action<bool> onResult)
        {
            if (onResult != null) onResult(false);     // sem SDK não há recibos para restaurar
        }

        /// <summary>
        /// Concessão local de entitlement — usada pelos cheats de editor e pelos testes,
        /// e pelo fluxo real de compra quando o SDK entrar (após validação de recibo).
        /// As 200 gemas do Remover Anúncios (CANON §11) são concedidas pelo fluxo de
        /// compra via EconomySystem.Earn, não aqui — entitlement é flag, não carteira.
        /// </summary>
        public void GrantEntitlement(string entitlementId)
        {
            if (_save == null) return;
            switch (entitlementId)
            {
                case EntitlementNoAds:
                    _save.adsRemoved = true;
                    if (GameBootstrap.Current != null) GameBootstrap.Current.AdsRemoved = true;
                    MarkDirty();
                    break;
                case EntitlementSeasonPass:
                    _save.seasonPassActive = true;
                    _save.seasonPassExpiryUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
                    MarkDirty();
                    break;
            }
        }

        private void MarkDirty()
        {
            if (_markSaveDirty != null) _markSaveDirty();
        }
    }
}
