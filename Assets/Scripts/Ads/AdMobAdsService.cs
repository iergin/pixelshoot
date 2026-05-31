#if PIXELSHOOT_ADMOB
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Real implementation backed by Google's official AdMob plugin. Compiled
    /// only when the user adds the PIXELSHOOT_ADMOB scripting define AFTER
    /// importing the GoogleMobileAds .unitypackage. Until then, NullAdsService
    /// is used and this file is invisible to the compiler.
    /// </summary>
    public class AdMobAdsService : IAdsService
    {
        // TODO: Replace with the real ad unit IDs from AdMob console.
#if UNITY_ANDROID
        private const string InterstitialUnitId = "ca-app-pub-3940256099942544/1033173712"; // Google's TEST id
        private const string RewardedUnitId     = "ca-app-pub-3940256099942544/5224354917"; // Google's TEST id
#elif UNITY_IPHONE
        private const string InterstitialUnitId = "ca-app-pub-3940256099942544/4411468910";
        private const string RewardedUnitId     = "ca-app-pub-3940256099942544/1712485313";
#else
        private const string InterstitialUnitId = "unused";
        private const string RewardedUnitId     = "unused";
#endif

        private InterstitialAd interstitial;
        private RewardedAd rewarded;

        public bool IsInterstitialReady => interstitial != null && interstitial.CanShowAd();
        public bool IsRewardedReady     => rewarded != null && rewarded.CanShowAd();

        public void Initialize()
        {
            MobileAds.Initialize(_ =>
            {
                Debug.Log("[Ads/AdMob] SDK initialised.");
                LoadInterstitial();
                LoadRewarded();
            });
        }

        private void LoadInterstitial()
        {
            var req = new AdRequest();
            InterstitialAd.Load(InterstitialUnitId, req, (ad, err) =>
            {
                if (err != null || ad == null)
                {
                    Debug.LogWarning($"[Ads/AdMob] Interstitial load failed: {err}");
                    return;
                }
                interstitial = ad;
                interstitial.OnAdFullScreenContentClosed += LoadInterstitial; // chain reload
            });
        }

        private void LoadRewarded()
        {
            var req = new AdRequest();
            RewardedAd.Load(RewardedUnitId, req, (ad, err) =>
            {
                if (err != null || ad == null)
                {
                    Debug.LogWarning($"[Ads/AdMob] Rewarded load failed: {err}");
                    return;
                }
                rewarded = ad;
                rewarded.OnAdFullScreenContentClosed += LoadRewarded;
            });
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsInterstitialReady) { onClosed?.Invoke(); return; }
            // Fire once-only so a reload doesn't re-trigger the same callback.
            EventHandler<EventArgs> handler = null;
            handler = (s, e) =>
            {
                interstitial.OnAdFullScreenContentClosed -= LoadInterstitial; // detach chain
                interstitial.OnAdFullScreenContentClosed += LoadInterstitial; // re-attach for next load
                onClosed?.Invoke();
            };
            // Note: the AdMob v9+ API uses Action, not EventHandler; adjust signature if SDK version differs.
            interstitial.OnAdFullScreenContentClosed += () => onClosed?.Invoke();
            interstitial.Show();
        }

        public void ShowRewarded(Action onRewarded, Action onClosed)
        {
            if (!IsRewardedReady) { onClosed?.Invoke(); return; }
            rewarded.OnAdFullScreenContentClosed += () => onClosed?.Invoke();
            rewarded.Show(_ => onRewarded?.Invoke());
        }
    }
}
#endif
