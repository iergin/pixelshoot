using UnityEngine;
using PixelShoot.Game;

namespace PixelShoot.Data
{
    /// <summary>
    /// One-time promotional offer. Visible to the player only while
    /// (a) less than <see cref="VisibleForDays"/> have passed since their first
    /// app launch, and (b) they haven't made any other purchase yet. After
    /// purchasing, or after the window expires, IsAvailable flips to false
    /// permanently.
    /// </summary>
    [CreateAssetMenu(fileName = "StarterOffer", menuName = "PixelShoot/Shop/Starter Offer (one-time, time-limited)")]
    public class StarterOffer : ShopOffer
    {
        [Tooltip("How many days the offer stays visible from the player's first launch.")]
        [SerializeField, Min(1)] private int visibleForDays = 7;

        public int VisibleForDays => visibleForDays;

        public override bool IsAvailable
        {
            get
            {
                if (PlayerWallet.HasPurchased(OfferId)) return false;             // bought already
                if (!PlayerWallet.IsWithinFirstDays(visibleForDays)) return false; // window closed
                return true;
            }
        }

        public override void OnPurchased()
        {
            PlayerWallet.MarkPurchased(OfferId);
            PlayerWallet.MarkAnyPurchaseMade();
            if (GrantedCoins > 0) PlayerWallet.Add(GrantedCoins);
            Debug.Log($"[Shop] Starter offer '{OfferId}' purchased. +{GrantedCoins} coins.");
        }
    }
}
