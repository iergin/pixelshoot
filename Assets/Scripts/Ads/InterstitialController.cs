using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Tracks "levels since last interstitial" and decides when to actually show one
    /// based on the active <see cref="InterstitialConfig"/>. Persists the counter in
    /// PlayerPrefs so a closed-app player doesn't get hit immediately on relaunch.
    ///
    /// Wire-up: hook <see cref="NotifyLevelEnded"/> to BOTH GameController.OnLevelWon
    /// and OnLevelFailed — every level result counts toward the interval. Call
    /// <see cref="NotifyRewardedWatched"/> after a successful rewarded ad to reset
    /// the cooldown counter.
    /// </summary>
    public class InterstitialController : MonoBehaviour
    {
        private const string CounterKey = "PixelShoot.LevelsSinceInterstitial";

        [SerializeField] private InterstitialConfig config;

        private static int LevelsSince
        {
            get => PlayerPrefs.GetInt(CounterKey, 0);
            set { PlayerPrefs.SetInt(CounterKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Call when ANY level ends (win or lose). Single counter handles both.</summary>
        public void NotifyLevelEnded()
        {
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
            LevelsSince = 0;
            AdsManager.Service.ShowInterstitial(null);
        }

        public void NotifyRewardedWatched()
        {
            // Engaged players get the cooldown reset as a courtesy.
            Debug.Log("[Interstitial] Rewarded watched — resetting cooldown counter.");
            LevelsSince = 0;
        }
    }
}
