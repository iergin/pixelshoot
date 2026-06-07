using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Shop
{
    public class NullIAPService : IIAPService
    {
        public bool IsReady => true;

        public void Initialize(IReadOnlyList<ProductRegistration> products)
        {
            int n = products != null ? products.Count : 0;
            if (n == 0) { Debug.Log("[IAP/Null] Initialized — no products."); return; }
            var lines = new List<string>(n);
            for (int i = 0; i < n; i++) lines.Add($"  • {products[i].ProductId} [{products[i].Type}]");
            Debug.Log($"[IAP/Null] Initialized for {n} products — no real store.\n" + string.Join("\n", lines));
        }

        public void Purchase(string productId, Action<bool> onComplete)
        {
            Debug.Log($"[IAP/Null] Pretending to purchase '{productId}' → success.");
            onComplete?.Invoke(true);
        }

        public string GetLocalizedPrice(string productId) => "$0.99";
    }
}
