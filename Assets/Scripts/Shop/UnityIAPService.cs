#if PIXELSHOOT_IAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Real IAP implementation backed by Unity Purchasing (`com.unity.purchasing`).
    /// Compiled only when PIXELSHOOT_IAP is defined AND the package is installed.
    /// </summary>
    public class UnityIAPService : IIAPService, IStoreListener
    {
        private IStoreController controller;
        private IExtensionProvider extensions;
        private readonly Dictionary<string, Action<bool>> pending = new Dictionary<string, Action<bool>>();

        public bool IsReady => controller != null;

        public void Initialize(string[] productIds)
        {
            if (controller != null) return;
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var pid in productIds)
                if (!string.IsNullOrEmpty(pid)) builder.AddProduct(pid, ProductType.NonConsumable);
            UnityPurchasing.Initialize(this, builder);
        }

        public void Purchase(string productId, Action<bool> onComplete)
        {
            if (!IsReady) { Debug.LogWarning("[IAP] Not initialised."); onComplete?.Invoke(false); return; }
            pending[productId] = onComplete;
            controller.InitiatePurchase(productId);
        }

        public string GetLocalizedPrice(string productId)
        {
            if (!IsReady) return "—";
            var product = controller.products.WithID(productId);
            return product != null && product.metadata != null ? product.metadata.localizedPriceString : "—";
        }

        // IStoreListener
        public void OnInitialized(IStoreController c, IExtensionProvider e) { controller = c; extensions = e; Debug.Log("[IAP] Initialised."); }
        public void OnInitializeFailed(InitializationFailureReason r) => Debug.LogError($"[IAP] Init failed: {r}");
        public void OnInitializeFailed(InitializationFailureReason r, string m) => Debug.LogError($"[IAP] Init failed: {r} {m}");
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogWarning($"[IAP] Purchase '{product?.definition?.id}' failed: {failureReason}");
            if (product != null && pending.TryGetValue(product.definition.id, out var cb))
            {
                pending.Remove(product.definition.id);
                cb?.Invoke(false);
            }
        }
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            string id = e.purchasedProduct.definition.id;
            Debug.Log($"[IAP] Purchase '{id}' complete.");
            if (pending.TryGetValue(id, out var cb))
            {
                pending.Remove(id);
                cb?.Invoke(true);
            }
            return PurchaseProcessingResult.Complete;
        }
    }
}
#endif
