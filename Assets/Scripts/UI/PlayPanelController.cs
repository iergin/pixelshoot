using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// The pre-level panel shown when the player presses Start on the main menu. It displays which
    /// level they're about to play and their current streak, and its Play button is what actually
    /// begins the level (via <see cref="MainMenuController.StartGame"/> — which spends the life and
    /// reveals gameplay). Close returns to the menu without starting.
    ///
    /// <para>If a <see cref="UiPanel"/> is assigned it drives open/close through that (animated,
    /// manager-tracked). Otherwise it falls back to toggling <see cref="panel"/> directly.</para>
    /// </summary>
    public class PlayPanelController : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The menu controller whose StartGame() actually begins the level.")]
        [SerializeField] private MainMenuController mainMenu;
        [Tooltip("Preferred: the UiPanel on the play panel. If set, open/close route through it " +
                 "(RequestOpen/RequestClose) — do NOT also toggle the panel with SetActive.")]
        [SerializeField] private UiPanel uiPanel;
        [Tooltip("Fallback panel root, used only when no UiPanel is assigned.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button playButton;
        [Tooltip("Optional back/close button — returns to the menu without starting. (Or use the " +
                 "UiPanel's own Close Buttons instead.)")]
        [SerializeField] private Button closeButton;

        [Header("Level")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private string levelFormat = "Level {0}";

        [Header("Streak")]
        [Tooltip("Optional root hidden when the streak is 0 (nothing to brag about yet).")]
        [SerializeField] private GameObject streakRoot;
        [SerializeField] private TMP_Text streakLabel;
        [SerializeField] private string streakFormat = "Streak: {0}";

        private void Awake()
        {
            if (playButton != null) { playButton.onClick.RemoveAllListeners(); playButton.onClick.AddListener(OnPlay); }
            if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Close); }
            // Only manage visibility ourselves in the no-UiPanel fallback; the UiPanel hides itself.
            if (uiPanel == null && panel != null) panel.SetActive(false);
        }

        /// <summary>Refresh the level / streak readout and show the panel.</summary>
        public void Open()
        {
            if (levelLabel != null) levelLabel.text = string.Format(levelFormat, PlayerProgress.DisplayLevel);

            int streak = PlayerStreak.Current;
            if (streakLabel != null) streakLabel.text = string.Format(streakFormat, streak);
            if (streakRoot != null) streakRoot.SetActive(streak > 0);

            if (uiPanel != null) uiPanel.RequestOpen();       // animated + manager-tracked
            else if (panel != null) panel.SetActive(true);
        }

        public void Close()
        {
            if (uiPanel != null) uiPanel.RequestClose();
            else if (panel != null) panel.SetActive(false);
        }

        private void OnPlay()
        {
            Close();
            // StartGame spends the life + reveals gameplay (or shows the out-of-lives popup).
            if (mainMenu != null) mainMenu.StartGame();
            else Debug.LogWarning("[PlayPanel] MainMenuController not assigned — can't start the level.");
        }
    }
}
