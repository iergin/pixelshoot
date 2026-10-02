using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// One on-screen booster button (bottom bar). Shows the booster icon + owned count.
    /// If the booster is locked (player's level below its unlock level) it shows a lock
    /// icon; tapping toggles an "unlocks at level N" info object (coordinated by the manager
    /// so only one is open, and any outside tap closes it). Unlocked → uses / buys it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoosterButton : MonoBehaviour
    {
        [SerializeField] private BoosterData booster;
        [SerializeField] private BoosterManager manager;
        [SerializeField] private BoosterTutorialController tutorial;
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [Tooltip("Icon sprite when the booster is ACTIVE (unlocked AND owned count > 0). " +
                 "Optional — falls back to the BoosterData icon if empty.")]
        [SerializeField] private Sprite activeIcon;
        [Tooltip("Icon sprite when the booster is INACTIVE (locked, or owned count = 0). " +
                 "Optional — falls back to the BoosterData icon if empty.")]
        [SerializeField] private Sprite inactiveIcon;
        [Tooltip("Shows the owned count (e.g. 'x3'). {0} = count.")]
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private string countFormat = "x{0}";
        [Tooltip("Optional badge shown when unlocked but the player owns none.")]
        [SerializeField] private GameObject buyBadge;
        [Tooltip("Optional world START point for this booster's fly particle. If null, the BoosterManager's default start is used.")]
        [SerializeField] private Transform flyStartPoint;

        [Header("Lock")]
        [Tooltip("Lock overlay shown while the booster is locked.")]
        [SerializeField] private GameObject lockIcon;
        [Tooltip("The 'unlocks at level N' object inside the button, toggled when a locked button is tapped.")]
        [SerializeField] private GameObject unlockInfo;
        [Tooltip("Optional label inside unlockInfo. {0} = unlock level.")]
        [SerializeField] private TMP_Text unlockLevelLabel;
        [SerializeField] private string unlockLevelFormat = "Lv {0}";

        [Header("Glow tutorial")]
        [Tooltip("Glow shown on this button the first time a GlowButton-style tutorial booster unlocks. Cleared once the button is pressed.")]
        [SerializeField] private GameObject tutorialGlow;

        public bool IsLocked => booster != null && !booster.IsUnlockedAtLevel(PlayerProgress.DisplayLevel);

        // The lightweight "just glow the button" tutorial: this booster unlocks now, uses the
        // GlowButton style, and hasn't been shown yet.
        private bool WantsGlowTutorial =>
            booster != null && booster.HasTutorial &&
            booster.TutorialStyle == BoosterTutorialStyle.GlowButton &&
            !IsLocked &&
            PlayerProgress.DisplayLevel == booster.UnlockLevel &&
            !PlayerBoosters.IsTutorialShown(booster.Id);

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(OnClick); }
            // Grant the one-time free boosters the very first time this booster is seen.
            if (booster != null) PlayerBoosters.GrantDefaultOnce(booster.Id, booster.DefaultFreeAmount);
        }

        private void Start()
        {
            // Kick off the one-time tutorial if this is the unlock level for this booster.
            if (tutorial != null && booster != null) tutorial.CheckFor(this, booster);
        }

        private void OnEnable()
        {
            PlayerBoosters.OnChanged += OnBoostersChanged;
            Refresh();
        }

        private void OnDisable() => PlayerBoosters.OnChanged -= OnBoostersChanged;

        private void OnBoostersChanged(string id, int _)
        {
            if (booster != null && id == booster.Id) Refresh();
        }

        private void OnClick()
        {
            if (booster == null || manager == null) return;
            // An interactive booster (Claw / FillColor) is running → ignore all booster taps.
            if (BoosterProcess.Active) return;
            // During its (spotlight) tutorial, the tap uses the booster FOR FREE and ends it.
            if (tutorial != null && tutorial.IsActiveFor(booster))
            {
                tutorial.CompleteWithUse(this, flyStartPoint);
                return;
            }
            // Glow-style tutorial: first press just clears the glow, then uses normally.
            if (WantsGlowTutorial)
            {
                PlayerBoosters.MarkTutorialShown(booster.Id);
                if (tutorialGlow != null) tutorialGlow.SetActive(false);
            }
            if (IsLocked) manager.ToggleUnlockInfo(this);   // locked → show/hide the unlock hint
            else          manager.RequestBooster(booster, flyStartPoint);
        }

        // Swap the icon between the active / inactive sprite. Each falls back to the BoosterData icon
        // if not assigned in the inspector.
        private void SetIcon(bool active)
        {
            if (iconImage == null) return;
            Sprite s = active ? activeIcon : inactiveIcon;
            if (s == null) s = booster != null ? booster.Icon : null;
            if (s != null) iconImage.sprite = s;
        }

        /// <summary>Show/hide this button's "unlocks at level N" object (driven by the manager).</summary>
        public void SetUnlockInfoVisible(bool visible)
        {
            if (unlockInfo != null) unlockInfo.SetActive(visible);
        }

        private void Refresh()
        {
            if (booster == null) return;
            bool locked = IsLocked;
            int count = PlayerBoosters.Count(booster.Id);

            // Active icon when the booster is usable (unlocked + owned), inactive otherwise.
            SetIcon(!locked && count > 0);

            if (lockIcon != null) lockIcon.SetActive(locked);
            if (countLabel != null)
            {
                countLabel.text = string.Format(countFormat, count);
                // Locked → hide the count label's PARENT (the whole count container), not just the label.
                var parent = countLabel.transform.parent;
                if (parent != null) parent.gameObject.SetActive(!locked);
                else countLabel.gameObject.SetActive(!locked);
            }
            if (buyBadge != null) buyBadge.SetActive(!locked && count <= 0);
            if (unlockLevelLabel != null) unlockLevelLabel.text = string.Format(unlockLevelFormat, booster.UnlockLevel);

            // Unlocked buttons never show the unlock hint.
            if (!locked && unlockInfo != null) unlockInfo.SetActive(false);

            // Glow the button while its one-time GlowButton tutorial is pending.
            if (tutorialGlow != null) tutorialGlow.SetActive(WantsGlowTutorial);
        }
    }
}
