using System;

namespace PixelShoot.Ads
{
    /// <summary>Position the banner is anchored to on screen.</summary>
    public enum BannerPosition { Top, Bottom }

    /// <summary>
    /// Abstraction over the ads SDK so the rest of the game compiles even when
    /// no SDK is installed. The default <see cref="NullAdsService"/> just logs;
    /// when the Google Mobile Ads plugin is imported and PIXELSHOOT_ADMOB is
    /// defined, AdMobAdsService is used instead.
    /// </summary>
    public interface IAdsService
    {
        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }
        bool IsBannerVisible { get; }

        void Initialize();

        /// <summary>Show an interstitial. Callback is fired regardless of success/cancellation.</summary>
        void ShowInterstitial(Action onClosed);

        /// <summary>Show a rewarded ad. onRewarded fires only when the player earned the reward; onClosed always fires when the ad goes away.</summary>
        void ShowRewarded(Action onRewarded, Action onClosed);

        /// <summary>Make the banner visible at the requested position. Creates / reloads it lazily if needed.</summary>
        void ShowBanner(BannerPosition position);

        /// <summary>Hide the banner without destroying it (cheap toggle).</summary>
        void HideBanner();
    }
}
