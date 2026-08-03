using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Whether the IAP product can be bought repeatedly (Consumable, e.g. coin packs)
    /// or only once and restored across devices (NonConsumable, e.g. NoAds / Starter Pack).
    /// Mirrors the Unity IAP <c>ProductType</c> but lives in our own data layer so
    /// ShopOffer compiles even without the Purchasing package.
    /// </summary>
    public enum ShopProductType
    {
        Consumable    = 0,
        NonConsumable = 1,
    }

    /// <summary>
    /// Base ScriptableObject for any shop offer. Designers can subclass for
    /// fancier reward bundles; <see cref="BasicOffer"/> covers the standard
    /// "give this many coins, can only be bought once" case.
    /// </summary>
    public abstract class ShopOffer : ScriptableObject
    {
        [Tooltip("Stable id used in PlayerPrefs (purchase history). Must be unique.")]
        [SerializeField] private string offerId;
        [Tooltip("Unity IAP product id registered in the Cloud dashboard / inspector.")]
        [SerializeField] private string productId;
        [Tooltip("Player-facing display name (e.g. 'Starter Pack').")]
        [SerializeField] private string displayName;
        [Tooltip("Coins paid out on a successful purchase.")]
        [SerializeField, Min(0)] private int grantedCoins;
        [Tooltip("Consumable = can be bought again (coin packs). NonConsumable = one-time, restorable across devices (NoAds, Starter).")]
        [SerializeField] private ShopProductType productType = ShopProductType.Consumable;
        [Tooltip("When purchased, REMOVE this offer's row from the shop entirely (next time the shop is " +
                 "opened AND immediately if it's open). Use for one-time bundles / No-Ads. Leave OFF to keep " +
                 "the row with an 'OWNED' overlay. No effect on repeatable offers (they're never 'purchased').")]
        [SerializeField] private bool hideWhenPurchased = false;
        [Tooltip("Hide this offer once the player OWNS No-Ads (even if THIS offer wasn't the one bought). " +
                 "Tick for bundles that include No-Ads — pointless to sell once No-Ads is already owned. " +
                 "Hides immediately if the shop is open, and on next open.")]
        [SerializeField] private bool hideWhenNoAdsOwned = false;

        public string OfferId => offerId;
        public string ProductId => productId;
        public string DisplayName => displayName;
        public int GrantedCoins => grantedCoins;
        public ShopProductType ProductType => productType;
        public bool HideWhenPurchased => hideWhenPurchased;
        public bool HideWhenNoAdsOwned => hideWhenNoAdsOwned;

        /// <summary>True if the player can purchase this offer right now. Override for one-time / cooldown rules.</summary>
        public abstract bool IsAvailable { get; }

        /// <summary>Called by ShopManager after a successful purchase to apply the reward and update state.</summary>
        public abstract void OnPurchased();
    }
}
