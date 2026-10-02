using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// The "No Ads" promo popup. Its buy action is a <see cref="ShopOfferButton"/> row (offerId
    /// <c>no_ads</c>) that drives itself off ShopManager.Instance, so this class only hosts the
    /// <see cref="BasePopup"/> and <b>closes itself once No-Ads is actually bought</b> (via
    /// <see cref="PlayerWallet.OnNoAdsChanged"/>) — so buying from inside dismisses it. Opened by
    /// <c>PopupService.Instance.Create&lt;NoAdsPromoPopup&gt;()</c> — see
    /// PixelShoot.Shop.NoAdsPromoController, which decides WHEN to show it.
    /// </summary>
    public class NoAdsPromoPopup : BasePopup
    {
        protected override void OnInit()
        {
            PlayerWallet.OnNoAdsChanged += HandleNoAdsChanged;
        }

        private void HandleNoAdsChanged()
        {
            if (PlayerWallet.HasNoAds) Close(); // purchased (here or elsewhere) → dismiss the promo
        }

        protected override void OnDestroy()
        {
            PlayerWallet.OnNoAdsChanged -= HandleNoAdsChanged;
            base.OnDestroy();
        }
    }
}
