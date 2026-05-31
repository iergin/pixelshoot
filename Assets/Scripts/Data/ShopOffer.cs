using UnityEngine;

namespace PixelShoot.Data
{
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

        public string OfferId => offerId;
        public string ProductId => productId;
        public string DisplayName => displayName;
        public int GrantedCoins => grantedCoins;

        /// <summary>True if the player can purchase this offer right now. Override for one-time / cooldown rules.</summary>
        public abstract bool IsAvailable { get; }

        /// <summary>Called by ShopManager after a successful purchase to apply the reward and update state.</summary>
        public abstract void OnPurchased();
    }
}
