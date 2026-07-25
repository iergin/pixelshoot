using UnityEngine;
using PixelShoot.Game;

namespace PixelShoot.Data
{
    /// <summary>
    /// A timed "unlimited lives" pack. Buying it grants (or extends) an unlimited-lives period of
    /// <see cref="unlimitedMinutes"/> minutes — during which levels cost no life and the HUD shows ∞.
    /// Repeatable: it never marks itself owned, so it can be bought again to stack more time.
    /// </summary>
    [CreateAssetMenu(fileName = "UnlimitedLivesOffer", menuName = "PixelShoot/Shop/Unlimited Lives Offer")]
    public class UnlimitedLivesOffer : ShopOffer
    {
        [Tooltip("Duration of unlimited lives granted per purchase, in minutes (720 = 12 hours).")]
        [Min(1)] public double unlimitedMinutes = 720;

        // Consumable → always buyable (each purchase adds more time).
        public override bool IsAvailable => true;

        public override void OnPurchased()
        {
            PlayerWallet.MarkAnyPurchaseMade();
            PlayerLives.GrantUnlimited(unlimitedMinutes);
            if (GrantedCoins > 0) PlayerWallet.Add(GrantedCoins); // optional bundled coins
            Debug.Log($"[Shop] Unlimited lives offer '{OfferId}' purchased. +{unlimitedMinutes} min unlimited.");
        }
    }
}
