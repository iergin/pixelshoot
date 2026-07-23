using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Data;
using PixelShoot.UI;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Holds the list of available offers, talks to <see cref="IIAPService"/>, and routes successful
    /// purchases to <see cref="ShopOffer.OnPurchased"/>. This is the persistent catalog/IAP manager;
    /// the shop UI itself is a <see cref="ShopPopup"/> opened through <see cref="PopupService"/>.
    ///
    /// <para>Lives in the MainMenu scene. Shop state is in PlayerPrefs, so a fresh instance each time
    /// the menu loads is fine.</para>
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Catalog")]
        [SerializeField] private List<ShopOffer> offers = new List<ShopOffer>();

        [Header("UI")]
        [Tooltip("Optional convenience button that opens the shop popup. Wired automatically in Awake. " +
                 "You can also open the shop from anywhere via ShopManager.Instance.OpenShop().")]
        [SerializeField] private Button openShopButton;

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

            if (openShopButton != null)
            {
                openShopButton.onClick.RemoveAllListeners();
                openShopButton.onClick.AddListener(OpenShop);
            }
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
                onComplete?.Invoke(false);
                return;
            }
            iap.Purchase(offer.ProductId, success =>
            {
                if (success) offer.OnPurchased();
                onComplete?.Invoke(success);
            });
        }
    }
}
