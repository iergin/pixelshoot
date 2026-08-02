using UnityEngine;
using PixelShoot.Game;
using PixelShoot.UI;

namespace PixelShoot.Data
{
    /// <summary>
    /// Standard coin pack — repeatable consumable. Player can buy it any number
    /// of times. Each purchase grants <see cref="ShopOffer.GrantedCoins"/> coins
    /// and marks "first purchase" on the wallet so promotional starter offers
    /// can disappear.
    /// </summary>
    [CreateAssetMenu(fileName = "CoinPack", menuName = "PixelShoot/Shop/Coin Pack (repeatable)")]
    public class RepeatableCoinOffer : ShopOffer
    {
        public override bool IsAvailable => true; // always purchasable

        public override void OnPurchased()
        {
            PlayerWallet.MarkAnyPurchaseMade();
            RewardFlow.Grant(new RewardBundle().AddCoins(GrantedCoins));
            Debug.Log($"[Shop] Coin pack '{OfferId}' purchased. +{GrantedCoins} coins.");
        }
    }
}
