using UnityEngine;
using PixelShoot.Game;
using PixelShoot.UI;

namespace PixelShoot.Data
{
    /// <summary>
    /// Vanilla one-time-purchase coin pack. Once bought, IsAvailable flips
    /// permanently to false via PlayerWallet's purchase ledger.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicOffer", menuName = "PixelShoot/Shop/Basic Offer (one-time)")]
    public class BasicOffer : ShopOffer
    {
        public override bool IsAvailable => !PlayerWallet.HasPurchased(OfferId);

        public override void OnPurchased()
        {
            PlayerWallet.MarkPurchased(OfferId);
            PlayerWallet.MarkAnyPurchaseMade();
            RewardFlow.Grant(new RewardBundle().AddCoins(GrantedCoins));
            Debug.Log($"[Shop] Basic offer '{OfferId}' purchased. +{GrantedCoins} coins.");
        }
    }
}
