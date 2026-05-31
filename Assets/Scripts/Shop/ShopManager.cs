using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Holds the list of available offers, talks to <see cref="IIAPService"/>,
    /// and routes successful purchases to <see cref="ShopOffer.OnPurchased"/>.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [SerializeField] private List<ShopOffer> offers = new List<ShopOffer>();

        private IIAPService iap;

        public IReadOnlyList<ShopOffer> Offers => offers;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if PIXELSHOOT_IAP
            iap = new UnityIAPService();
#else
            iap = new NullIAPService();
#endif
            var pids = new List<string>();
            foreach (var o in offers) if (o != null && !string.IsNullOrEmpty(o.ProductId)) pids.Add(o.ProductId);
            iap.Initialize(pids.ToArray());
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
