namespace PixelShoot.UI
{
    /// <summary>
    /// Shown after an IAP purchase FAILS or is CANCELLED (the waiting popup is closed first). Give it
    /// an OK / close button wired via BasePopup's Close Buttons. Opened by
    /// <see cref="PixelShoot.Shop.ShopManager"/> on purchase failure.
    /// </summary>
    public class PurchaseFailPopup : BasePopup
    {
    }
}
