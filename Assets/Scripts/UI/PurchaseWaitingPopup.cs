namespace PixelShoot.UI
{
    /// <summary>
    /// Shown the moment an IAP purchase starts, while the store dialog / network round-trip is in
    /// flight. Dismissed PROGRAMMATICALLY by <see cref="PixelShoot.Shop.ShopManager"/> when the
    /// purchase completes — so give this prefab NO user close button (design it as a "connecting to
    /// store…" spinner). Opened via PopupService.CreateOnTop so it stacks over the shop.
    /// </summary>
    public class PurchaseWaitingPopup : BasePopup
    {
    }
}
