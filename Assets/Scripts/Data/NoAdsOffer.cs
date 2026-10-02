using UnityEngine;
using PixelShoot.Game;
using PixelShoot.UI;

namespace PixelShoot.Data
{
    /// <summary>
    /// One-time "Remove Ads" purchase. Once purchased, AdsManager skips
    /// interstitial and banner ads for the rest of the player's life.
    ///
    /// <para><b>Visibility</b>: this offer is rendered by the promo controller
    /// at two specific triggers (after first ad seen, second-session
    /// post-first-level). The shop UI itself can still list it if the
    /// designer chooses — IsAvailable just gates the purchase button.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "NoAdsOffer", menuName = "PixelShoot/Shop/No Ads (one-time)")]
    public class NoAdsOffer : ShopOffer
    {
        public override bool IsAvailable => !PlayerWallet.HasNoAds;

        public override void OnPurchased()
        {
            PlayerWallet.MarkPurchased(OfferId);
            PlayerWallet.MarkAnyPurchaseMade();
            // RewardBundle.Apply() sets the No-Ads flag AND suppresses the banner (interstitials are
            // gated through InterstitialController, which already checks PlayerWallet.HasNoAds), then
            // the claim popup flies a No-Ads icon to the Play button.
            RewardFlow.Grant(new RewardBundle().AddNoAds());
            Debug.Log("[Shop] NoAds purchased — all future interstitial / banner ads suppressed.");
        }
    }
}
