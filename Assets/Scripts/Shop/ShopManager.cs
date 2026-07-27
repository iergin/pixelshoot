using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;
using PixelShoot.UI;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Holds the list of available offers, talks to <see cref="IIAPService"/>, and routes successful
    /// purchases to <see cref="ShopOffer.OnPurchased"/>. This is the persistent catalog/IAP manager;
    /// the shop UI itself is a <see cref="ShopPopup"/> opened through <see cref="PopupService"/>.
    ///
    /// <para>Lives in the persistent <b>InitializeScene</b> so the shop can be opened AND purchases
    /// made from BOTH the MainMenu and the Game scene (e.g. when a booster runs out mid-level). Reach
    /// it from anywhere via <see cref="Instance"/>.OpenShop() — a serialized cross-scene button ref
    /// would not survive, so opener buttons in each scene call OpenShop() themselves.</para>
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Catalog")]
        [SerializeField] private List<ShopOffer> offers = new List<ShopOffer>();

        private IIAPService iap;

        public IReadOnlyList<ShopOffer> Offers => offers;

        /// <summary>True while the shop popup is on screen.</summary>
        public bool IsOpen => PopupService.Instance != null && PopupService.Instance.IsOpen<ShopPopup>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[ShopManager] Duplicate instance on '{name}' — destroying.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

#if PIXELSHOOT_IAP
            iap = new UnityIAPService();
#else
            iap = new NullIAPService();
#endif
            var products = new List<ProductRegistration>();
            foreach (var o in offers)
                if (o != null && !string.IsNullOrEmpty(o.ProductId))
                    products.Add(new ProductRegistration(o.ProductId, o.ProductType));
            iap.Initialize(products);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Open the shop popup (queued top-level). Callable from anywhere.</summary>
        public void OpenShop()
        {
            if (PopupService.Instance != null) PopupService.Instance.Create<ShopPopup>();
            else Debug.LogWarning("[ShopManager] OpenShop: no PopupService (is InitializeScene loaded?).");
        }

        public string GetLocalizedPrice(ShopOffer offer)
            => offer != null && iap != null ? iap.GetLocalizedPrice(offer.ProductId) : "—";

        public void BuyOffer(ShopOffer offer, System.Action<bool> onComplete = null)
        {
            if (offer == null) { onComplete?.Invoke(false); return; }
            if (!offer.IsAvailable)
            {
                Debug.LogWarning($"[Shop] Offer '{offer.OfferId}' not available (already purchased / locked).");
                onComplete?.Invoke(false);
                return;
            }
            if (iap == null || !iap.IsReady)
            {
                Debug.LogWarning("[Shop] IAP service not ready.");
                ShowResult(false);
                onComplete?.Invoke(false);
                return;
            }

            // Store round-trip can take a moment → show a waiting popup over the shop until it returns.
            var waiting = PopupService.Instance != null
                ? PopupService.Instance.CreateOnTop<PurchaseWaitingPopup>()
                : null;

            iap.Purchase(offer.ProductId, success =>
            {
                if (success) offer.OnPurchased();
                if (waiting != null) waiting.Close(); // dismiss the waiting popup (no user close button)
                ShowResult(success);                       // success → Success popup, fail/cancel → Fail popup
                onComplete?.Invoke(success);
            });
        }

        // Close the waiting popup (if any) then stack the success / fail popup over the shop.
        private static void ShowResult(bool success)
        {
            if (PopupService.Instance == null) return;
            if (success) PopupService.Instance.CreateOnTop<PurchaseSuccessPopup>();
            else         PopupService.Instance.CreateOnTop<PurchaseFailPopup>();
        }
    }
}
