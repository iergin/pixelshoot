using System;
using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Decides when to actually show an interstitial. The cadence is "show the
    /// next ad only after <see cref="InterstitialConfig.LevelsBetweenAds"/> more
    /// level-end events HAVE PASSED SINCE THE LAST AD CLOSED". The counter resets
    /// inside the ad-close callback, never at <c>Show()</c> — that way a failed
    /// or skipped show doesn't burn the cooldown.
    /// </summary>
    public class InterstitialController : MonoBehaviour
    {
        private const string CounterKey = "PixelShoot.LevelsSinceInterstitial";

        [SerializeField] private InterstitialConfig config;

        /// <summary>True while an interstitial we triggered is on-screen and not yet closed.</summary>
        private bool waitingForCurrentAdToClose;

        public event Action OnInterstitialClosed;

        /// <summary>Fired right before the player's VERY FIRST interstitial would show. A subscriber
        /// (the No Ads promo) can present its offer first and MUST invoke the supplied callback when
        /// the player dismisses it — that lets the ad proceed (or, if No Ads was bought meanwhile,
        /// the ad is skipped). With no subscriber the ad shows immediately, as before.</summary>
        public event Action<Action> OnBeforeFirstInterstitial;

        private static int LevelsSince
        {
            get => PlayerPrefs.GetInt(CounterKey, 0);
            set { PlayerPrefs.SetInt(CounterKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Call when ANY level ends (win or lose).</summary>
        public void NotifyLevelEnded()
        {
            // NoAds purchase suppresses interstitials entirely.
            if (PlayerWallet.HasNoAds)
            {
                Debug.Log("[Interstitial] Skipped — NoAds purchased.");
                return;
            }
            // An ad is still on-screen → don't count this end event toward the next show.
            if (waitingForCurrentAdToClose)
            {
                Debug.Log("[Interstitial] Skipped — previous ad still open.");
                return;
            }

            if (config == null) return;

            int currentLevel = PlayerProgress.DisplayLevel;
            if (currentLevel < config.StartLevel)
            {
                Debug.Log($"[Interstitial] Skipped — current level {currentLevel} below startLevel {config.StartLevel}.");
                return;
            }

            int interval = Mathf.Max(1, config.LevelsBetweenAds);
            int newCount = LevelsSince + 1;
            LevelsSince = newCount;

            if (newCount < interval)
            {
                Debug.Log($"[Interstitial] {newCount}/{interval} levels until next ad.");
                return;
            }

            if (AdsManager.Service == null || !AdsManager.Service.IsInterstitialReady)
            {
                Debug.Log("[Interstitial] Threshold reached but no ad loaded; keeping counter armed.");
                return;
            }

            waitingForCurrentAdToClose = true;

            // Before the VERY FIRST interstitial, let the No Ads promo have a turn. The subscriber
            // shows its offer and calls ShowInterstitialNow when the player dismisses it (or buys
            // No Ads, in which case the ad is skipped). No subscriber → show the ad right away.
            if (!PlayerWallet.HasSeenFirstAd && OnBeforeFirstInterstitial != null)
            {
                Debug.Log("[Interstitial] First interstitial — offering the No Ads promo BEFORE the ad.");
                OnBeforeFirstInterstitial.Invoke(ShowInterstitialNow);
                return;
            }

            Debug.Log($"[Interstitial] Showing — {newCount} levels since last ad, interval={interval} at level {currentLevel}.");
            ShowInterstitialNow();
        }

        /// <summary>Actually shows the interstitial — called directly, or via the pre-ad promo's
        /// callback. Skips the ad if No Ads was purchased in the promo, or if the ad went unready.</summary>
        private void ShowInterstitialNow()
        {
            if (PlayerWallet.HasNoAds)
            {
                Debug.Log("[Interstitial] No Ads bought in the promo — skipping the ad.");
                LevelsSince = 0;
                waitingForCurrentAdToClose = false;
                return;
            }
            if (AdsManager.Service == null || !AdsManager.Service.IsInterstitialReady)
            {
                Debug.Log("[Interstitial] Ad no longer ready after the promo — keeping counter armed.");
                waitingForCurrentAdToClose = false;
                return;
            }
            AdsManager.Service.ShowInterstitial(HandleInterstitialClosed);
        }

        private void HandleInterstitialClosed()
        {
            Debug.Log("[Interstitial] Ad closed — cooldown counter reset, next cadence starts now.");
            LevelsSince = 0;
            waitingForCurrentAdToClose = false;
            PlayerWallet.MarkFirstAdSeen();
            OnInterstitialClosed?.Invoke();
        }

        public void NotifyRewardedWatched()
        {
            // Engaged players get the cooldown reset as a courtesy.
            Debug.Log("[Interstitial] Rewarded watched — resetting cooldown counter.");
            LevelsSince = 0;
            PlayerWallet.MarkFirstAdSeen();
        }
    }
}
