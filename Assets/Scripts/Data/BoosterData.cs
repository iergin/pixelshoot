using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>What a booster does when used. Extend as new boosters are added.</summary>
    public enum BoosterType
    {
        ConveyorCapacity, // +Amount conveyor slots
        Custom1,
        Custom2,
        Custom3,
    }

    /// <summary>
    /// One booster definition. Holds its identity (id / name / description / icon), its
    /// gameplay effect (type + amount), and its purchase options (coins / ad). Create via
    /// Create ▸ PixelShoot ▸ Booster; one asset per booster.
    /// </summary>
    [CreateAssetMenu(fileName = "Booster", menuName = "PixelShoot/Booster")]
    public class BoosterData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "conveyor_capacity";
        [SerializeField] private string displayName = "Extra Lane";
        [SerializeField, TextArea] private string description = "Adds an extra conveyor slot.";
        [SerializeField] private Sprite icon;

        [Header("Effect")]
        [SerializeField] private BoosterType type = BoosterType.ConveyorCapacity;
        [Tooltip("Effect magnitude — e.g. how many conveyor slots to add.")]
        [SerializeField, Min(1)] private int amount = 1;

        [Header("Purchase")]
        [Tooltip("Coin price. The 'buy with coins' button uses this.")]
        [SerializeField, Min(0)] private int coinCost = 500;
        [Tooltip("Show the 'buy with coins' button in the purchase popup.")]
        [SerializeField] private bool coinPurchaseEnabled = true;
        [Tooltip("Show the 'watch ad' button in the purchase popup.")]
        [SerializeField] private bool adPurchaseEnabled = true;
        [Tooltip("How many boosters one ad / one purchase grants.")]
        [SerializeField, Min(1)] private int grantAmount = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public BoosterType Type => type;
        public int Amount => amount;
        public int CoinCost => coinCost;
        public bool CoinPurchaseEnabled => coinPurchaseEnabled;
        public bool AdPurchaseEnabled => adPurchaseEnabled;
        public int GrantAmount => grantAmount;
    }
}
