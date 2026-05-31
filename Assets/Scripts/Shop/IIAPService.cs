using System;

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
        void Initialize(string[] productIds);
        void Purchase(string productId, Action<bool> onComplete);
        string GetLocalizedPrice(string productId);
    }
}
