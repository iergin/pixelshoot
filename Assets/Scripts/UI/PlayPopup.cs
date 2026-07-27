using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Pre-level popup (level # + streak + gift preview) used in BOTH contexts:
    /// <list type="bullet">
    /// <item><b>Menu</b> — opened via <c>Create&lt;PlayPopup&gt;(p =&gt; p.Bind(menu))</c>. Play calls
    /// <see cref="MainMenuController.StartGame"/>; X just closes back to the menu.</item>
    /// <item><b>In-game retry</b> — opened by <see cref="FailFlowPopup"/> at the end of the fail/quit
    /// chain, WITHOUT Bind. Play restarts the level (spend a life → reload; out of lives → OutOfLives);
    /// X leaves to the main menu.</item>
    /// </list>
    /// The mode is auto-detected: bound to a menu = menu mode, otherwise = retry mode.
    /// </summary>
    public class PlayPopup : BasePopup
    {
        [Header("Play")]
        [SerializeField] private Button playButton;
        [Tooltip("Optional label on the play button — swapped to Retry Text in in-game retry mode.")]
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private string playText = "Play";
        [SerializeField] private string retryText = "Try Again";
        [Tooltip("Optional X / close button. In retry mode it leaves to the main menu; in menu mode it just closes.")]
        [SerializeField] private Button closeButton;

        [Header("Level")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private string levelFormat = "Level {0}";

        [Header("Streak fill bar")]
        [Tooltip("Continuous streak bar. Set its Image Type to Filled (Horizontal); " +
                 "fillAmount = currentStreak / MaxRewardStreak.")]
        [SerializeField] private Image barFill;

        [Header("Streak gift preview (display only)")]
        [Tooltip("Optional root shown only when the streak grants a gift this run. The gifts themselves " +
                 "are applied in the GAME scene by StreakGiftController on level start — this is just the " +
                 "preview of what the current streak will grant, read from PlayerStreak.")]
        [SerializeField] private GameObject giftRoot;
        [Tooltip("Bomb reward count label. {0} = number of streak bombs.")]
        [SerializeField] private TMP_Text bombRewardLabel;
        [SerializeField] private string bombRewardFormat = "x{0}";
        [Tooltip("Paint reward count label. {0} = number of streak paints.")]
        [SerializeField] private TMP_Text paintRewardLabel;
        [SerializeField] private string paintRewardFormat = "x{0}";
        [Tooltip("Optional per-reward roots hidden when that reward is 0.")]
        [SerializeField] private GameObject bombRewardRoot;
        [SerializeField] private GameObject paintRewardRoot;

        private MainMenuController menu;

        // No menu bound → we're the in-game retry popup (opened by FailFlowPopup).
        private bool IsRetry => menu == null;

        /// <summary>Supply the menu controller whose StartGame() begins the level (menu mode).</summary>
        public void Bind(MainMenuController menuController) => menu = menuController;

        protected override void OnInit()
        {
            if (playButton != null)  { playButton.onClick.RemoveAllListeners();  playButton.onClick.AddListener(OnPlay); }
            if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(OnClose); }

            if (playLabel != null) playLabel.text = IsRetry ? retryText : playText;
            if (levelLabel != null) levelLabel.text = string.Format(levelFormat, PlayerProgress.DisplayLevel);

            ShowStreakBar();
            ShowGiftPreview();
        }

        // Fill the bar to currentStreak / MaxRewardStreak (capped full past the max).
        private void ShowStreakBar()
        {
            if (barFill == null) return;
            int max = PlayerStreak.MaxRewardStreak;
            int filled = Mathf.Clamp(PlayerStreak.Current, 0, max);
            barFill.fillAmount = max > 0 ? (float)filled / max : 0f;
        }

        // Preview what the current streak will grant when the level starts. The actual bombs/paint are
        // placed in the Game scene by StreakGiftController — here we only read the same PlayerStreak
        // values so the player sees their reward before pressing Play.
        private void ShowGiftPreview()
        {
            int bombs  = PlayerStreak.RewardBombs;
            int paints = PlayerStreak.RewardPaints;

            if (bombRewardLabel != null) bombRewardLabel.text = string.Format(bombRewardFormat, bombs);
            if (paintRewardLabel != null) paintRewardLabel.text = string.Format(paintRewardFormat, paints);
            if (bombRewardRoot != null) bombRewardRoot.SetActive(bombs > 0);
            if (paintRewardRoot != null) paintRewardRoot.SetActive(paints > 0);
            if (giftRoot != null) giftRoot.SetActive(bombs > 0 || paints > 0);
        }

        private void OnPlay()
        {
            Close();

            // Menu mode: spend a life + hand off to the Game scene (or the out-of-lives popup).
            if (menu != null) { menu.StartGame(); return; }

            // In-game retry: a fresh attempt costs a life (free while unlimited).
            if (PlayerLives.TryConsumeForLevelStart())
            {
                if (GameController.Instance != null) GameController.Instance.ReloadScene();
                else if (SceneFlow.Instance != null) SceneFlow.Instance.ReloadGame();
            }
            else if (PopupService.Instance != null)
            {
                PopupService.Instance.Create<OutOfLivesPopup>();
            }
        }

        private void OnClose()
        {
            Close();
            // In-game retry: X leaves to the main menu. Menu mode: X just closes (already home).
            if (IsRetry && SceneFlow.Instance != null) SceneFlow.Instance.LoadMainMenu();
        }
    }
}
