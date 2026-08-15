using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// One powerup slot in the PlayPopup's "Select Powerups" UI (Bomb or Paint). Shows the owned count
    /// in a badge; a selected slot shows a different background; when you own none it shows a "+" and
    /// tapping opens the shop. Tapping an owned slot toggles it on/off for the next level. Both slots
    /// (bomb + paint) can be selected at once — each is independent.
    ///
    /// <para>State is read/written through <see cref="PlayerPowerups"/>, so it survives to the Game
    /// scene where <see cref="StreakGiftController"/> consumes + applies the selected powerups at level
    /// start. Drop one of these on each powerup button inside the PlayPopup prefab.</para>
    /// </summary>
    public class PowerupSelectSlot : MonoBehaviour
    {
        [SerializeField] private PowerupType type;
        [Tooltip("The whole slot button. Owned → toggles selection; not owned → opens the shop.")]
        [SerializeField] private Button button;

        [Header("Selected state (background sprite swap)")]
        [Tooltip("Background Image whose sprite changes between selected / unselected.")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite unselectedSprite;

        [Header("Count / buy")]
        [Tooltip("Red count badge shown when you own at least one. Hidden at 0.")]
        [SerializeField] private GameObject countBadge;
        [SerializeField] private TMP_Text countLabel;
        [Tooltip("'+' badge shown when you own NONE (tap opens the shop).")]
        [SerializeField] private GameObject addBadge;

        private void Awake()
        {
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(OnClick); }
        }

        private void OnEnable()
        {
            PlayerPowerups.OnChanged += OnPowerupChanged;
            PlayerPowerups.OnSelectionChanged += OnSelectionChanged;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerPowerups.OnChanged -= OnPowerupChanged;
            PlayerPowerups.OnSelectionChanged -= OnSelectionChanged;
        }

        private void OnPowerupChanged(PowerupType t, int _)   { if (t == type) Refresh(); }
        private void OnSelectionChanged(PowerupType t, bool _) { if (t == type) Refresh(); }

        private bool locked;

        /// <summary>Lock the slot: the '+' (buy) is hidden and the button is non-interactable, so the
        /// player can't add/select this powerup until its feature unlocks.</summary>
        public void SetLocked(bool value)
        {
            locked = value;
            if (button != null) button.interactable = !value;
            Refresh();
        }

        private void OnClick()
        {
            if (locked) return; // locked feature — ignore taps

            int owned = PlayerPowerups.Owned(type);
            if (owned > 0)
            {
                bool nowSelected = !PlayerPowerups.IsSelected(type);
                PlayerPowerups.SetSelected(type, nowSelected); // toggle
                PixelShoot.Analytics.AnalyticsEvents.TrackPowerupSelect(type.ToString(), nowSelected);
            }
            else
                OpenShop(); // none owned → go buy
        }

        /// <summary>Open the shop stacked over the PlayPopup (wire a general 'powerups' button here too).</summary>
        public void OpenShop()
        {
            PixelShoot.Analytics.AnalyticsEvents.TrackShopOpen("powerup_slot");
            if (PopupService.Instance != null) PopupService.Instance.CreateOnTop<ShopPopup>();
        }

        private void Refresh()
        {
            int owned = PlayerPowerups.Owned(type);
            bool selected = owned > 0 && PlayerPowerups.IsSelected(type);

            if (countBadge != null) countBadge.SetActive(owned > 0);
            if (countLabel != null) countLabel.text = owned.ToString();
            if (addBadge   != null) addBadge.SetActive(!locked && owned <= 0); // no '+' while locked

            if (backgroundImage != null)
            {
                var s = selected ? selectedSprite : unselectedSprite;
                if (s != null) backgroundImage.sprite = s;
            }
        }
    }
}
