using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Game;

namespace PixelShoot.Data
{
    /// <summary>
    /// A bundle purchase (e.g. "Starter Bundle") that grants several rewards at once: coins
    /// (<see cref="ShopOffer.GrantedCoins"/>), optional No-Ads, optional timed unlimited lives, and any
    /// number of boosters. Author one asset per bundle and drop it on a <see cref="ShopOfferButton"/>.
    ///
    /// <para><see cref="repeatable"/> off = one-time (NonConsumable, hides after buying); on = can be
    /// bought again (Consumable, e.g. a coins-only value pack).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BundleOffer", menuName = "PixelShoot/Shop/Bundle Offer")]
    public class BundleOffer : ShopOffer
    {
        [System.Serializable]
        public struct BoosterGrant
        {
            public BoosterData booster;
            [Min(1)] public int amount;
        }

        [Header("Bundle contents (coins come from Granted Coins on the base)")]
        [Tooltip("Grant permanent No-Ads as part of this bundle.")]
        [SerializeField] private bool grantNoAds = false;
        [Tooltip("Minutes of unlimited lives to grant (0 = none). 720 = 12h.")]
        [SerializeField, Min(0)] private double unlimitedMinutes = 0;
        [Tooltip("Boosters granted by this bundle (each with an amount).")]
        [SerializeField] private BoosterGrant[] boosters;

        [Header("Powerups (start-of-level bombs / paint)")]
        [Tooltip("Bomb powerups granted (start-of-level bombs, selected in the PlayPopup).")]
        [SerializeField, Min(0)] private int bombPowerups = 0;
        [Tooltip("Paint powerups granted (start-of-level free paint, selected in the PlayPopup).")]
        [SerializeField, Min(0)] private int paintPowerups = 0;

        [Header("Purchase rule")]
        [Tooltip("On = repeatable (Consumable). Off = one-time, hides after purchase (NonConsumable).")]
        [SerializeField] private bool repeatable = false;

        public override bool IsAvailable => repeatable || !PlayerWallet.HasPurchased(OfferId);

        public override void OnPurchased()
        {
            PlayerWallet.MarkAnyPurchaseMade();
            if (!repeatable) PlayerWallet.MarkPurchased(OfferId);

            if (GrantedCoins > 0) PlayerWallet.Add(GrantedCoins);

            if (grantNoAds && !PlayerWallet.HasNoAds)
            {
                PlayerWallet.MarkNoAdsBought();
                AdsManager.SuppressAdsAfterNoAdsPurchase();
            }

            if (unlimitedMinutes > 0) PlayerLives.GrantUnlimited(unlimitedMinutes);

            if (boosters != null)
                foreach (var g in boosters)
                    if (g.booster != null && g.amount > 0) PlayerBoosters.Add(g.booster.Id, g.amount);

            if (bombPowerups > 0)  PlayerPowerups.Add(PowerupType.Bomb, bombPowerups);
            if (paintPowerups > 0) PlayerPowerups.Add(PowerupType.Paint, paintPowerups);

            Debug.Log($"[Shop] Bundle '{OfferId}' purchased — +{GrantedCoins} coins" +
                      $"{(grantNoAds ? ", NoAds" : "")}" +
                      $"{(unlimitedMinutes > 0 ? $", {unlimitedMinutes}min unlimited" : "")}" +
                      $"{(boosters != null && boosters.Length > 0 ? $", {boosters.Length} booster type(s)" : "")}.");
        }
    }
}
