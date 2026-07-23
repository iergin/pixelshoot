using System;
using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Drives the post-onboarding promotional popups.
    ///
    /// <para><b>NoAds promo</b> is shown up to <see cref="maxNoAdsShows"/> times total
    /// (default 3 = "right after the first ad, then twice more") to players who haven't
    /// bought it:
    /// <list type="bullet">
    /// <item><b>Show 1</b> — right after the player closes their FIRST interstitial.</item>
    /// <item><b>Shows 2…N</b> — once per NEW session, on the first level cleared that
    /// session, until the cap is reached or NoAds is purchased.</item>
    /// </list></para>
    ///
    /// <para><b>Starter Pack promo</b> — once, in a later session's first level, IF NoAds
    /// has been bought and the starter wasn't already grabbed from the shop.</para>
    ///
    /// <para>State (show count, last session, starter flag) persisted to PlayerPrefs.</para>
    /// </summary>
    public class NoAdsPromoController : MonoBehaviour
    {
        /// <summary>The one persistent promo core (lives in InitializeScene). Per-scene
        /// <see cref="NoAdsPromoTrigger"/>s (menu button, game win) call it through this.</summary>
        public static NoAdsPromoController Instance { get; private set; }

        private const string NoAdsCountKey       = "PixelShoot.NoAdsPromo.ShownCount";
        private const string NoAdsLastSessionKey = "PixelShoot.NoAdsPromo.LastSession";
        private const string StarterShownKey     = "PixelShoot.StarterPromo.Shown";

        // Offer ids that the promo gates check against — must match the asset offerId values.
        private const string NoAdsOfferId    = "no_ads";
        private const string StarterOfferId  = "coins_5000_starter";

        [Header("Tuning")]
        [Tooltip("Total number of times the NoAds promo is shown to a non-purchaser (1 = first ad only; 3 = first ad + 2 more).")]
        [SerializeField, Min(1)] private int maxNoAdsShows = 3;

        [Header("Sources")]
        [Tooltip("InterstitialController (same InitializeScene) for the pre-/post-first-ad triggers. " +
                 "Falls back to InterstitialController.Instance. The MENU BUTTON and the PER-SESSION " +
                 "WIN trigger live on NoAdsPromoTrigger components in their own scenes.")]
        [SerializeField] private InterstitialController interstitial;

        private int noAdsShownCount;
        private int noAdsLastSession;
        private bool starterShown;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            noAdsShownCount  = PlayerPrefs.GetInt(NoAdsCountKey, 0);
            noAdsLastSession = PlayerPrefs.GetInt(NoAdsLastSessionKey, 0);
            starterShown     = PlayerPrefs.GetInt(StarterShownKey, 0) == 1;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // Resolved once so subscribe/unsubscribe use the SAME instance. In the two-scene setup the
        // InterstitialController lives in InitializeScene, so we fall back to its static Instance.
        private InterstitialController itl;

        private void OnEnable()
        {
            itl = interstitial != null ? interstitial : InterstitialController.Instance;
            if (itl != null)
            {
                itl.OnBeforeFirstInterstitial += HandleBeforeFirstAd;
                itl.OnInterstitialClosed      += OnFirstAdSeenMaybe; // fallback only
            }
        }

        private void OnDisable()
        {
            if (itl != null)
            {
                itl.OnBeforeFirstInterstitial -= HandleBeforeFirstAd;
                itl.OnInterstitialClosed      -= OnFirstAdSeenMaybe;
            }
        }

        private bool NoAdsOwned => PlayerWallet.HasNoAds || PlayerWallet.HasPurchased(NoAdsOfferId);

        // ─── Show 1: BEFORE the player's first interstitial ─────────────────
        // The No Ads offer would ideally get a turn ahead of the very first ad, but a plain
        // GameObject panel can't be awaited to re-trigger the ad on close, so we let the ad play now
        // and surface the promo right after (OnFirstAdSeenMaybe). When migrated to a MessagePopup,
        // gate the ad by re-invoking proceed() from the popup's Closed event instead.
        private void HandleBeforeFirstAd(Action proceed)
        {
            proceed?.Invoke();
        }

        // ─── Fallback Show 1: after the first ad, only if the pre-ad hook didn't run ──
        private void OnFirstAdSeenMaybe()
        {
            if (NoAdsOwned) return;
            if (noAdsShownCount >= 1) return; // pre-ad path already showed it → don't double up
            DoShowNoAds(PlayerWallet.SessionCount);
            Debug.Log("[NoAdsPromo] Show 1 fired (fallback, after first ad) → NoAds panel.");
        }

        // ─── Shows 2…N: once per new session, first level cleared ───────────
        // Called by a NoAdsPromoTrigger in the Game scene on GameController.OnLevelWon.
        public void NotifyLevelWon()
        {
            int session = PlayerWallet.SessionCount;

            if (NoAdsOwned)
            {
                // NoAds bought → offer the starter pack once (if not already grabbed).
                if (PlayerWallet.HasPurchased(StarterOfferId)) return;
                if (starterShown) return;
                if (session < 2) return;
                ShowStarterPanel();
                starterShown = true;
                PlayerPrefs.SetInt(StarterShownKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[NoAdsPromo] Starter promo fired (NoAds owned) → Starter Pack panel.");
                return;
            }

            // NoAds not bought → the additional shows.
            if (noAdsShownCount == 0) return;              // wait for show #1 (first ad) to start the sequence
            if (noAdsShownCount >= maxNoAdsShows) return;  // cap reached
            if (session <= noAdsLastSession) return;       // already shown this (or an earlier) session — one per session
            DoShowNoAds(session);
            Debug.Log($"[NoAdsPromo] Show {noAdsShownCount} fired (session {session}) → NoAds panel.");
        }

        private void DoShowNoAds(int session)
        {
            ShowNoAdsPanel();
            noAdsShownCount++;
            noAdsLastSession = session;
            PlayerPrefs.SetInt(NoAdsCountKey, noAdsShownCount);
            PlayerPrefs.SetInt(NoAdsLastSessionKey, noAdsLastSession);
            PlayerPrefs.Save();
        }

        // ─── Show helpers (open the popups through PopupService) ─────────────
        public void ShowNoAdsPanel()
        {
            if (PixelShoot.UI.PopupService.Instance != null)
                PixelShoot.UI.PopupService.Instance.Create<PixelShoot.UI.NoAdsPromoPopup>();
            else Debug.LogWarning("[NoAdsPromo] No PopupService — cannot show the NoAds popup.");
        }

        public void ShowStarterPanel()
        {
            if (PixelShoot.UI.PopupService.Instance != null)
                PixelShoot.UI.PopupService.Instance.Create<PixelShoot.UI.StarterPopup>();
            else Debug.LogWarning("[NoAdsPromo] No PopupService — cannot show the Starter popup.");
        }
    }
}
