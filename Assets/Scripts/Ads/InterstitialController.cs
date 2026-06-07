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

            Debug.Log($"[Interstitial] Showing — {newCount} levels since last ad, interval={interval} at level {currentLevel}.");
            waitingForCurrentAdToClose = true;
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
