using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Conveyor;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Shown right after a booster is acquired (coin purchase or rewarded ad). Displays the
    /// booster's title / description / icon and a single "use" button. Tapping it consumes
    /// one booster and triggers the fly + effect through the <see cref="BoosterManager"/>.
    /// The conveyor stays paused while the panel is open.
    /// </summary>
    public class BoosterUsePanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;

        [Header("Content")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image iconImage;

        [Header("Button")]
        [SerializeField] private Button useButton;

        [Header("Links")]
        [SerializeField] private BoosterManager manager;
        [Tooltip("Conveyor paused while the panel is open.")]
        [SerializeField] private ConveyorController conveyor;

        private BoosterData current;
        private bool wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired) return;
            wired = true;
            if (useButton != null) { useButton.onClick.RemoveAllListeners(); useButton.onClick.AddListener(OnUse); }
        }

        /// <summary>Open the panel for a just-acquired booster.</summary>
        public void Open(BoosterData data)
        {
            if (data == null) return;
            Wire();
            current = data;

            if (titleLabel != null)       titleLabel.text = data.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = data.Description;
            if (iconImage != null) { iconImage.sprite = data.Icon; iconImage.enabled = data.Icon != null; }

            if (conveyor != null) conveyor.IsPaused = true;
            if (panel != null) panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (conveyor != null) conveyor.IsPaused = false;
            current = null;
        }

        // The single button: consume one and use it now.
        private void OnUse()
        {
            var data = current;
            Close();
            if (manager != null && data != null) manager.UseFromPanel(data);
        }
    }
}
