#if PIXELSHOOT_ADMOB
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Real implementation backed by Google's official AdMob plugin. Compiled
    /// only when the user adds the PIXELSHOOT_ADMOB scripting define AFTER
    /// importing the GoogleMobileAds .unitypackage.
    ///
    /// In the Unity editor the AdMob plugin shows a fake "placeholder ad" dialog
    /// when Show() is called — that's the dummy UI you're looking for. On device,
    /// the test IDs below render the real test ad unit; replace them with your
    /// own AdMob ad units for production.
    /// </summary>
    public class AdMobAdsService : IAdsService
    {
        // Google's official TEST ad unit IDs. Safe to use during development —
        // they always fill with a test creative and don't risk a policy strike.
#if UNITY_ANDROID
        private const string InterstitialUnitId = "ca-app-pub-1162079788089996/5004201231";
        private const string RewardedUnitId     = "ca-app-pub-1162079788089996/8053566633";
#elif UNITY_IPHONE
        private const string InterstitialUnitId = "ca-app-pub-1162079788089996/5004201231";
        private const string RewardedUnitId     = "ca-app-pub-1162079788089996/8053566633";
#else
        // Editor / Standalone uses the Android test id so the placeholder UI fires.
        private const string InterstitialUnitId = "ca-app-pub-1162079788089996/5004201231";
        private const string RewardedUnitId     = "ca-app-pub-1162079788089996/8053566633";
#endif

        private InterstitialAd interstitial;
        private RewardedAd rewarded;
        private Action pendingInterstitialClosed;
        private Action pendingRewardedClosed;
        private Action pendingRewardedReward;

        public bool IsInterstitialReady => interstitial != null && interstitial.CanShowAd();
        public bool IsRewardedReady     => rewarded != null && rewarded.CanShowAd();

        public void Initialize()
        {
            MobileAds.Initialize(status =>
            {
                Debug.Log("[Ads/AdMob] SDK initialised.");
                LoadInterstitial();
                LoadRewarded();
            });
        }

        // ── Interstitial ────────────────────────────────────────────────
        private void LoadInterstitial()
        {
            // Throw away the previous instance, if any, before requesting a new one.
            if (interstitial != null) { interstitial.Destroy(); interstitial = null; }

            var req = new AdRequest();
            InterstitialAd.Load(InterstitialUnitId, req, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads/AdMob] Interstitial load failed: {error}");
                    return;
                }
                interstitial = ad;
                HookInterstitialEvents(interstitial);
                Debug.Log("[Ads/AdMob] Interstitial loaded.");
            });
        }

        private void HookInterstitialEvents(InterstitialAd ad)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[Ads/AdMob] Interstitial closed; reloading.");
                pendingInterstitialClosed?.Invoke();
                pendingInterstitialClosed = null;
                LoadInterstitial();
            };
            ad.OnAdFullScreenContentFailed += err =>
            {
                Debug.LogWarning($"[Ads/AdMob] Interstitial show failed: {err}");
                pendingInterstitialClosed?.Invoke();
                pendingInterstitialClosed = null;
                LoadInterstitial();
            };
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsInterstitialReady)
            {
                Debug.LogWarning("[Ads/AdMob] Interstitial not ready; calling onClosed immediately.");
                onClosed?.Invoke();
                return;
            }
            pendingInterstitialClosed = onClosed;
            interstitial.Show();
        }

        // ── Rewarded ────────────────────────────────────────────────────
        private void LoadRewarded()
        {
            if (rewarded != null) { rewarded.Destroy(); rewarded = null; }

            var req = new AdRequest();
            RewardedAd.Load(RewardedUnitId, req, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads/AdMob] Rewarded load failed: {error}");
                    return;
                }
                rewarded = ad;
                HookRewardedEvents(rewarded);
                Debug.Log("[Ads/AdMob] Rewarded loaded.");
            });
        }

        private void HookRewardedEvents(RewardedAd ad)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[Ads/AdMob] Rewarded closed; reloading.");
                pendingRewardedClosed?.Invoke();
                pendingRewardedClosed = null;
                pendingRewardedReward = null;
                LoadRewarded();
            };
            ad.OnAdFullScreenContentFailed += err =>
            {
                Debug.LogWarning($"[Ads/AdMob] Rewarded show failed: {err}");
                pendingRewardedClosed?.Invoke();
                pendingRewardedClosed = null;
                pendingRewardedReward = null;
                LoadRewarded();
            };
        }

        public void ShowRewarded(Action onRewarded, Action onClosed)
        {
            if (!IsRewardedReady)
            {
                Debug.LogWarning("[Ads/AdMob] Rewarded not ready; calling onClosed immediately.");
                onClosed?.Invoke();
                return;
            }
            pendingRewardedClosed = onClosed;
            pendingRewardedReward = onRewarded;
            rewarded.Show(reward =>
            {
                Debug.Log($"[Ads/AdMob] Reward earned: {reward.Type} x {reward.Amount}");
                pendingRewardedReward?.Invoke();
                pendingRewardedReward = null;
            });
        }
    }
}
#endif
