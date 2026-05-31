using System;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Abstraction over the ads SDK so the rest of the game compiles even when
    /// no SDK is installed. The default <see cref="NullAdsService"/> just logs;
    /// when the Google Mobile Ads plugin is imported and PIXELSHOOT_ADMOB is
    /// defined, AdMobAdsService is used instead.
    /// </summary>
    public interface IAdsService
    {
        /// <summary>Returns true once the SDK is initialised and an ad unit is loaded.</summary>
        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }

        void Initialize();

        /// <summary>Show an interstitial. Callback is fired regardless of success/cancellation.</summary>
        void ShowInterstitial(Action onClosed);

        /// <summary>Show a rewarded ad. onRewarded fires only when the player earned the reward; onClosed always fires when the ad goes away.</summary>
        void ShowRewarded(Action onRewarded, Action onClosed);
    }
}
