using System;
using System.Collections.Generic;
using PixelShoot.Data;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Abstraction over the Unity IAP plugin. When the package is installed and the
    /// PIXELSHOOT_IAP scripting define is set, UnityIAPService is used; otherwise
    /// NullIAPService just fakes a successful purchase so the wallet flow can be
    /// tested without store integration.
    /// </summary>
    public interface IIAPService
    {
        bool IsReady { get; }
        void Initialize(IReadOnlyList<ProductRegistration> products);
        void Purchase(string productId, Action<bool> onComplete);
        string GetLocalizedPrice(string productId);
    }

    /// <summary>Tuple of (Unity IAP product id, consumable/non-consumable type) used at catalog registration time.</summary>
    public readonly struct ProductRegistration
    {
        public readonly string ProductId;
        public readonly ShopProductType Type;
        public ProductRegistration(string productId, ShopProductType type) { ProductId = productId; Type = type; }
    }
}
