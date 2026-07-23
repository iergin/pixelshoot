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
        private const string NoAdsCountKey       = "PixelShoot.NoAdsPromo.ShownCount";
        private const string NoAdsLastSessionKey = "PixelShoot.NoAdsPromo.LastSession";
        private const string StarterShownKey     = "PixelShoot.StarterPromo.Shown";

        // Offer ids that the promo gates check against — must match the asset offerId values.
        private const string NoAdsOfferId    = "no_ads";
        private const string StarterOfferId  = "coins_5000_starter";

        [Header("Panels")]
        [Tooltip("NoAds promo — shown after the first ad and then up to maxNoAdsShows-1 more times.")]
        [SerializeField] private GameObject promoPanel;
        [Tooltip("Starter Pack promo — shown once after NoAds has been bought.")]
        [SerializeField] private GameObject starterPromoPanel;
        [Header("Panel routing (optional)")]
        [Tooltip("If set, promos open through the global UiPanelManager in QUEUE mode — they wait for any open panel to close instead of overlapping it.")]
        [SerializeField] private PixelShoot.UI.UiPanel noAdsUiPanel;
        [SerializeField] private PixelShoot.UI.UiPanel starterUiPanel;

        [Header("Tuning")]
        [Tooltip("Total number of times the NoAds promo is shown to a non-purchaser (1 = first ad only; 3 = first ad + 2 more).")]
        [SerializeField, Min(1)] private int maxNoAdsShows = 3;

        [Header("Menu button")]
        [Tooltip("The menu's 'No Ads' button. Owned + wired HERE (not by MainMenuController) so no " +
                 "button is double-listened. Opens the No Ads promo panel on click.")]
        [SerializeField] private UnityEngine.UI.Button openButton;

        [Header("Sources")]
        [Tooltip("InterstitialController whose OnInterstitialClosed event we subscribe to for the first-ad trigger.")]
        [SerializeField] private InterstitialController interstitial;
        [Tooltip("GameController whose OnLevelWon we subscribe to for the per-session trigger.")]
        [SerializeField] private GameController gameController;

        private int noAdsShownCount;
        private int noAdsLastSession;
        private bool starterShown;

        private void Awake()
        {
            noAdsShownCount  = PlayerPrefs.GetInt(NoAdsCountKey, 0);
            noAdsLastSession = PlayerPrefs.GetInt(NoAdsLastSessionKey, 0);
            starterShown     = PlayerPrefs.GetInt(StarterShownKey, 0) == 1;
            if (promoPanel        != null) promoPanel.SetActive(false);
            if (starterPromoPanel != null) starterPromoPanel.SetActive(false);

            // Own our menu button (mirrors ShopManager / SettingsController).
            if (openButton != null)
            {
                openButton.onClick.RemoveAllListeners();
                openButton.onClick.AddListener(ShowNoAdsPanel);
            }
        }

        private Action pendingProceedToAd;

        private void OnEnable()
        {
            if (interstitial != null)
            {
                interstitial.OnBeforeFirstInterstitial += HandleBeforeFirstAd;
                interstitial.OnInterstitialClosed      += OnFirstAdSeenMaybe; // fallback only
            }
            if (gameController != null) gameController.OnLevelWon += OnLevelWonMaybe;
        }

        private void OnDisable()
        {
            if (interstitial != null)
            {
                interstitial.OnBeforeFirstInterstitial -= HandleBeforeFirstAd;
                interstitial.OnInterstitialClosed      -= OnFirstAdSeenMaybe;
            }
            if (gameController != null) gameController.OnLevelWon -= OnLevelWonMaybe;
            if (noAdsUiPanel != null) noAdsUiPanel.OnClosed -= OnPreAdPromoClosed;
        }

        private bool NoAdsOwned => PlayerWallet.HasNoAds || PlayerWallet.HasPurchased(NoAdsOfferId);

        // ─── Show 1: BEFORE the player's first interstitial ─────────────────
        // The No Ads offer gets a turn ahead of the very first ad. When the player dismisses the
        // promo we call proceed() to let the ad play; if they bought No Ads, the interstitial
        // controller skips the ad. Needs a UiPanel (its OnClosed drives proceed) to gate the ad.
        private void HandleBeforeFirstAd(Action proceed)
        {
            if (NoAdsOwned) { proceed?.Invoke(); return; }
            if (noAdsUiPanel == null)
            {
                // Can't await a plain GameObject's close to re-trigger the ad → let the ad play
                // now; the after-ad fallback still surfaces the promo so it isn't lost.
                Debug.Log("[NoAdsPromo] No UiPanel to gate the pre-ad promo → ad plays, promo shown after.");
                proceed?.Invoke();
                return;
            }
            pendingProceedToAd = proceed;
            noAdsUiPanel.OnClosed -= OnPreAdPromoClosed;
            noAdsUiPanel.OnClosed += OnPreAdPromoClosed;
            DoShowNoAds(PlayerWallet.SessionCount);
            Debug.Log("[NoAdsPromo] Show 1 fired BEFORE the first ad → NoAds panel.");
        }

        private void OnPreAdPromoClosed()
        {
            if (noAdsUiPanel != null) noAdsUiPanel.OnClosed -= OnPreAdPromoClosed;
            var proceed = pendingProceedToAd;
            pendingProceedToAd = null;
            proceed?.Invoke(); // promo dismissed → let the interstitial play (or be skipped if bought)
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
        private void OnLevelWonMaybe()
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

        // ─── Show / close helpers ───────────────────────────────────────────
        // Promos open in QUEUE mode (replaceCurrent: false): if a panel is already up,
        // they wait for it to close instead of popping over it.
        public void ShowNoAdsPanel()
        {
            if (noAdsUiPanel != null) { noAdsUiPanel.RequestOpen(replaceCurrent: false); return; }
            if (promoPanel == null) { Debug.LogWarning("[NoAdsPromo] No NoAds panel assigned."); return; }
            promoPanel.SetActive(true);
        }

        public void ShowStarterPanel()
        {
            if (starterUiPanel != null) { starterUiPanel.RequestOpen(replaceCurrent: false); return; }
            if (starterPromoPanel == null) { Debug.LogWarning("[NoAdsPromo] No Starter panel assigned."); return; }
            starterPromoPanel.SetActive(true);
        }

        public void ClosePanels()
        {
            if (noAdsUiPanel   != null) noAdsUiPanel.RequestClose();
            if (starterUiPanel != null) starterUiPanel.RequestClose();
            if (promoPanel        != null) promoPanel.SetActive(false);
            if (starterPromoPanel != null) starterPromoPanel.SetActive(false);
        }
    }
}
