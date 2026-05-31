using System;
using UnityEngine;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Drop-in stub used when no real ads SDK is present. Pretends every ad
    /// "succeeded" so the game flow can be tested end-to-end (rewards, interstitial
    /// counters, etc.) without integrating the SDK.
    /// </summary>
    public class NullAdsService : IAdsService
    {
        public bool IsInterstitialReady => true;
        public bool IsRewardedReady => true;

        public void Initialize() => Debug.Log("[Ads/Null] Initialized — no real SDK, all ads will fake-succeed.");

        public void ShowInterstitial(Action onClosed)
        {
            Debug.Log("[Ads/Null] Interstitial 'shown'.");
            onClosed?.Invoke();
        }

        public void ShowRewarded(Action onRewarded, Action onClosed)
        {
            Debug.Log("[Ads/Null] Rewarded 'shown' — granting reward.");
            onRewarded?.Invoke();
            onClosed?.Invoke();
        }
    }
}
