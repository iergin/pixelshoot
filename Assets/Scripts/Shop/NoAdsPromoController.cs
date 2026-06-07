using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Drives the post-onboarding promotional popups. There are two trigger
    /// points; what shows up at each depends on whether NoAds has been bought.
    ///
    /// <list type="bullet">
    /// <item><b>Trigger 1</b> — right after the player closes their FIRST
    /// interstitial. Always shows the NoAds promo (NoAds isn't yet purchased
    /// because the trigger requires an ad to have just been shown).</item>
    /// <item><b>Trigger 2</b> — second session, immediately after they finish
    /// the first level of that session. Branches:
    /// <list type="bullet">
    /// <item>NoAds NOT purchased → shows the NoAds promo</item>
    /// <item>NoAds purchased → shows the Starter Pack promo</item>
    /// </list></item>
    /// </list>
    ///
    /// <para>Each panel only fires once per player — flags persisted to PlayerPrefs.</para>
    /// </summary>
    public class NoAdsPromoController : MonoBehaviour
    {
        private const string T1ShownKey         = "PixelShoot.NoAdsPromo.T1Shown";
        private const string T2NoAdsShownKey    = "PixelShoot.NoAdsPromo.T2Shown";
        private const string T2StarterShownKey  = "PixelShoot.StarterPromo.T2Shown";

        [Header("Panels")]
        [Tooltip("NoAds promo — shown at Trigger 1 always, and at Trigger 2 if NoAds isn't already bought.")]
        [SerializeField] private GameObject promoPanel;
        [Tooltip("Starter Pack promo — shown at Trigger 2 IF NoAds has been bought.")]
        [SerializeField] private GameObject starterPromoPanel;

        [Header("Sources")]
        [Tooltip("InterstitialController whose OnInterstitialClosed event we subscribe to for the first-ad trigger.")]
        [SerializeField] private InterstitialController interstitial;
        [Tooltip("GameController whose OnLevelWon we subscribe to for the post-first-level trigger.")]
        [SerializeField] private GameController gameController;

        private bool t1Shown;
        private bool t2NoAdsShown;
        private bool t2StarterShown;

        private void Awake()
        {
            t1Shown        = PlayerPrefs.GetInt(T1ShownKey, 0) == 1;
            t2NoAdsShown   = PlayerPrefs.GetInt(T2NoAdsShownKey, 0) == 1;
            t2StarterShown = PlayerPrefs.GetInt(T2StarterShownKey, 0) == 1;
            if (promoPanel        != null) promoPanel.SetActive(false);
            if (starterPromoPanel != null) starterPromoPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (interstitial != null) interstitial.OnInterstitialClosed += OnFirstAdSeenMaybe;
            if (gameController != null) gameController.OnLevelWon       += OnLevelWonMaybe;
        }

        private void OnDisable()
        {
            if (interstitial != null) interstitial.OnInterstitialClosed -= OnFirstAdSeenMaybe;
            if (gameController != null) gameController.OnLevelWon       -= OnLevelWonMaybe;
        }

        // ─── Trigger 1: first interstitial closed ───────────────────────────
        private void OnFirstAdSeenMaybe()
        {
            if (PlayerWallet.HasNoAds) return; // shouldn't happen — NoAds suppresses ads
            if (t1Shown) return;
            ShowNoAdsPanel();
            t1Shown = true;
            PlayerPrefs.SetInt(T1ShownKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[NoAdsPromo] Trigger 1 fired (first ad seen) → NoAds panel.");
        }

        // ─── Trigger 2: session 2, first level done ─────────────────────────
        private void OnLevelWonMaybe()
        {
            if (PlayerWallet.SessionCount < 2) return;
            if (!PlayerWallet.FirstLevelDoneThisSession) return;

            if (PlayerWallet.HasNoAds)
            {
                // NoAds bought — show starter promo (once).
                if (t2StarterShown) return;
                ShowStarterPanel();
                t2StarterShown = true;
                PlayerPrefs.SetInt(T2StarterShownKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[NoAdsPromo] Trigger 2 fired (NoAds owned) → Starter Pack panel.");
            }
            else
            {
                // NoAds not bought — push the NoAds promo.
                if (t2NoAdsShown) return;
                ShowNoAdsPanel();
                t2NoAdsShown = true;
                PlayerPrefs.SetInt(T2NoAdsShownKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[NoAdsPromo] Trigger 2 fired (NoAds not owned) → NoAds panel.");
            }
        }

        // ─── Show / close helpers ───────────────────────────────────────────
        public void ShowNoAdsPanel()
        {
            if (promoPanel == null) { Debug.LogWarning("[NoAdsPromo] No NoAds panel assigned."); return; }
            promoPanel.SetActive(true);
        }

        public void ShowStarterPanel()
        {
            if (starterPromoPanel == null) { Debug.LogWarning("[NoAdsPromo] No Starter panel assigned."); return; }
            starterPromoPanel.SetActive(true);
        }

        public void ClosePanels()
        {
            if (promoPanel        != null) promoPanel.SetActive(false);
            if (starterPromoPanel != null) starterPromoPanel.SetActive(false);
        }
    }
}
