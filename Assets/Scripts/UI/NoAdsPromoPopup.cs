namespace PixelShoot.UI
{
    /// <summary>
    /// The "No Ads" promo popup. Its buy action is a <see cref="ShopOfferButton"/> row (offerId
    /// <c>no_ads</c>) that drives itself off ShopManager.Instance, so this class is just a
    /// <see cref="BasePopup"/> host. Opened by
    /// <c>PopupService.Instance.Create&lt;NoAdsPromoPopup&gt;()</c> — see
    /// PixelShoot.Shop.NoAdsPromoController, which decides WHEN to show it.
    /// </summary>
    public class NoAdsPromoPopup : BasePopup
    {
    }
}
