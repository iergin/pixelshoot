namespace PixelShoot.UI
{
    /// <summary>
    /// The Starter Pack promo popup (offered once after No Ads is bought). Its buy action is a
    /// <see cref="ShopOfferButton"/> row (offerId <c>coins_5000_starter</c>) that drives itself off
    /// ShopManager.Instance, so this class is just a <see cref="BasePopup"/> host. Opened by
    /// <c>PopupService.Instance.Create&lt;StarterPopup&gt;()</c> — see
    /// PixelShoot.Shop.NoAdsPromoController.
    /// </summary>
    public class StarterPopup : BasePopup
    {
    }
}
