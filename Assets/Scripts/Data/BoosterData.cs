using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>What a booster does when used. Extend as new boosters are added.</summary>
    public enum BoosterType
    {
        ConveyorCapacity, // +Amount conveyor slots (instant)
        Claw,             // interactive: pick any column shooter and pull it onto the conveyor
        Custom2,
        Custom3,
    }

    /// <summary>How the one-time tutorial is presented.</summary>
    public enum BoosterTutorialStyle
    {
        Spotlight,  // darken everything, keep the button + target lit, show a panel
        GlowButton, // just make the booster button glow (no dark overlay)
    }

    /// <summary>Boosters whose "use" opens an interactive mode instead of an instant effect.</summary>
    public static class BoosterTypeExtensions
    {
        public static bool IsInteractive(this BoosterType t) => t == BoosterType.Claw;
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

        [Header("Unlock")]
        [Tooltip("Booster is locked until the player reaches this (1-based) level. 1 = always unlocked.")]
        [SerializeField, Min(1)] private int unlockLevel = 1;

        [Header("Onboarding")]
        [Tooltip("Free boosters granted once, the first time the player ever sees this booster.")]
        [SerializeField, Min(0)] private int defaultFreeAmount = 0;
        [Tooltip("Show a one-time forced tutorial on the level this booster unlocks.")]
        [SerializeField] private bool hasTutorial = false;
        [Tooltip("How the tutorial is shown: Spotlight (dark overlay + panel) or GlowButton (just glow the button).")]
        [SerializeField] private BoosterTutorialStyle tutorialStyle = BoosterTutorialStyle.Spotlight;
        [Tooltip("Text shown in the tutorial panel.")]
        [SerializeField, TextArea] private string tutorialText = "Tap to use this booster!";

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
        public int UnlockLevel => unlockLevel;
        public bool IsUnlockedAtLevel(int displayLevel) => displayLevel >= unlockLevel;
        public int DefaultFreeAmount => defaultFreeAmount;
        public bool HasTutorial => hasTutorial;
        public BoosterTutorialStyle TutorialStyle => tutorialStyle;
        public bool IsInteractive => type.IsInteractive();
        public string TutorialText => tutorialText;
        public int CoinCost => coinCost;
        public bool CoinPurchaseEnabled => coinPurchaseEnabled;
        public bool AdPurchaseEnabled => adPurchaseEnabled;
        public int GrantAmount => grantAmount;
    }
}
