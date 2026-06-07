using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Game;

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
            PlayerWallet.MarkNoAdsBought();
            PlayerWallet.MarkPurchased(OfferId);
            PlayerWallet.MarkAnyPurchaseMade();
            // Hide the banner immediately; interstitials are gated through
            // InterstitialController which already checks PlayerWallet.HasNoAds.
            AdsManager.SuppressAdsAfterNoAdsPurchase();
            Debug.Log("[Shop] NoAds purchased — all future interstitial / banner ads suppressed.");
        }
    }
}
