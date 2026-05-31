using System;
using UnityEngine;

namespace PixelShoot.Shop
{
    public class NullIAPService : IIAPService
    {
        public bool IsReady => true;

        public void Initialize(string[] productIds)
        {
            Debug.Log($"[IAP/Null] Initialized for {productIds?.Length ?? 0} products — no real store.");
        }

        public void Purchase(string productId, Action<bool> onComplete)
        {
            Debug.Log($"[IAP/Null] Pretending to purchase '{productId}' → success.");
            onComplete?.Invoke(true);
        }

        public string GetLocalizedPrice(string productId) => "$0.99";
    }
}
