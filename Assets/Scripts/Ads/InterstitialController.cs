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
        /// <summary>Singleton so callers in OTHER scenes (Game's LevelEnd, Menu's No-Ads promo) can
        /// reach the one instance living in the persistent InitializeScene — a serialized reference
        /// can't cross scenes.</summary>
        public static InterstitialController Instance { get; private set; }

        private const string CounterKey = "PixelShoot.LevelsSinceInterstitial";

        [SerializeField] private InterstitialConfig config;

        /// <summary>True while an interstitial we triggered is on-screen and not yet closed.</summary>
        private bool waitingForCurrentAdToClose;

        /// <summary>Realtime (unscaled, ad-pause-proof) when the last interstitial CLOSED. -1 = none
        /// yet this session. The time cooldown is measured from here so watching the ad doesn't
        /// count toward it.</summary>
        private float lastAdClosedRealtime = -1f;

        public event Action OnInterstitialClosed;

        /// <summary>Fired right before the player's VERY FIRST interstitial would show. A subscriber
        /// (the No Ads promo) can present its offer first and MUST invoke the supplied callback when
        /// the player dismisses it — that lets the ad proceed (or, if No Ads was bought meanwhile,
        /// the ad is skipped). With no subscriber the ad shows immediately, as before.</summary>
        public event Action<Action> OnBeforeFirstInterstitial;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static int LevelsSince
        {
            get => PlayerPrefs.GetInt(CounterKey, 0);
            set { PlayerPrefs.SetInt(CounterKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        // Fired when the current NotifyLevelEnded resolves (ad closed, or no ad shown). Lets callers
        // (e.g. the next-level reload) run AFTER the ad instead of underneath it.
        private Action pendingResolved;
        private void ResolvePending()
        {
            var cb = pendingResolved;
            pendingResolved = null;
            cb?.Invoke();
        }

        /// <summary>Call when ANY level ends (win or lose). <paramref name="onResolved"/> is invoked
        /// when it's safe to continue — immediately if no ad shows, or after the interstitial (and any
        /// pre-ad promo) closes. Pass the next-level reload here so it never runs under the ad.</summary>
        public void NotifyLevelEnded(Action onResolved = null)
        {
            // NoAds purchase suppresses interstitials entirely.
            if (PlayerWallet.HasNoAds)      { Debug.Log("[Interstitial] Skipped — NoAds purchased."); onResolved?.Invoke(); return; }
            // An ad is still on-screen → don't count this end event toward the next show.
            if (waitingForCurrentAdToClose) { Debug.Log("[Interstitial] Skipped — previous ad still open."); onResolved?.Invoke(); return; }
            if (config == null)             { onResolved?.Invoke(); return; }

            int currentLevel = PlayerProgress.DisplayLevel;
            if (currentLevel < config.StartLevel)
            {
                Debug.Log($"[Interstitial] Skipped — current level {currentLevel} below startLevel {config.StartLevel}.");
                onResolved?.Invoke(); return;
            }

            int interval = Mathf.Max(1, config.LevelsBetweenAds);
            int newCount = LevelsSince + 1;
            LevelsSince = newCount;

            if (newCount < interval)
            {
                Debug.Log($"[Interstitial] {newCount}/{interval} levels until next ad.");
                onResolved?.Invoke(); return;
            }

            // Time cooldown — measured from when the LAST ad CLOSED.
            if (CooldownActive(out float remaining))
            {
                Debug.Log($"[Interstitial] Skipped — cooldown active, {remaining:F0}s left since the last ad closed.");
                onResolved?.Invoke(); return;
            }

            if (AdsManager.Service == null || !AdsManager.Service.IsInterstitialReady)
            {
                Debug.Log("[Interstitial] Threshold reached but no ad loaded; keeping counter armed.");
                onResolved?.Invoke(); return;
            }

            waitingForCurrentAdToClose = true;
            pendingResolved = onResolved; // resolved when the ad closes / is skipped

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
                ResolvePending();
                return;
            }
            if (AdsManager.Service == null || !AdsManager.Service.IsInterstitialReady)
            {
                Debug.Log("[Interstitial] Ad no longer ready after the promo — keeping counter armed.");
                waitingForCurrentAdToClose = false;
                ResolvePending();
                return;
            }
            AdsManager.Service.ShowInterstitial(HandleInterstitialClosed);
        }

        /// <summary>True if the time cooldown since the last ad closed hasn't elapsed yet.</summary>
        private bool CooldownActive(out float remaining)
        {
            remaining = 0f;
            if (config == null || config.CooldownSeconds <= 0f) return false;
            if (lastAdClosedRealtime < 0f) return false; // no ad has closed yet → no cooldown
            remaining = config.CooldownSeconds - (Time.realtimeSinceStartup - lastAdClosedRealtime);
            return remaining > 0f;
        }

        private void HandleInterstitialClosed()
        {
            // Cooldown starts NOW (ad closed), not when it opened — time watching the ad is free.
            lastAdClosedRealtime = Time.realtimeSinceStartup;
            Debug.Log("[Interstitial] Ad closed — level counter reset + cooldown timer started now.");
            LevelsSince = 0;
            waitingForCurrentAdToClose = false;
            PlayerWallet.MarkFirstAdSeen();
            OnInterstitialClosed?.Invoke();
            ResolvePending(); // now safe to continue (e.g. reload the next level)
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
