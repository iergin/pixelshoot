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

        [Header("Streak lock (feature gated by level)")]
        [Tooltip("Unlock level comes from StreakConfig.unlockLevel (single source of truth). Whole streak " +
                 "UI (bar + gift preview) — shown only once the streak is unlocked.")]
        [SerializeField] private GameObject streakRoot;
        [Tooltip("Lock overlay shown while the streak is still locked.")]
        [SerializeField] private GameObject streakLockRoot;
        [Tooltip("Label on the lock showing the unlock level. {0} = the level number.")]
        [SerializeField] private TMP_Text streakUnlockLabel;
        [SerializeField] private string streakUnlockFormat = "Level {0}";

        [Header("Streak tutorial (first unlock)")]
        [Tooltip("Shown ONCE — the first time this popup opens with the streak just unlocked. Tap anywhere to close.")]
        [SerializeField] private GameObject streakTutorial;
        [Tooltip("Full-screen (transparent) button over everything that catches a tap ANYWHERE to dismiss the tutorial.")]
        [SerializeField] private Button streakTutorialTapCatcher;

        private const string StreakTutorialShownKey = "PixelShoot.StreakTutorialShown";

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

            ApplyStreakLock();
        }

        // Streak feature is gated until streakUnlockLevel: below it, show the lock (with the unlock level)
        // and hide the streak UI; at/after it, show the streak and — the FIRST time — the tutorial.
        private void ApplyStreakLock()
        {
            int unlockLevel = PlayerStreak.UnlockLevel; // from StreakConfig
            bool unlocked = PlayerProgress.DisplayLevel >= unlockLevel;

            if (streakRoot != null) streakRoot.SetActive(unlocked);
            if (streakLockRoot != null) streakLockRoot.SetActive(!unlocked);

            if (unlocked)
            {
                ShowStreakBar();
                ShowGiftPreview();
                MaybeShowStreakTutorial();
            }
            else
            {
                if (streakUnlockLabel != null) streakUnlockLabel.text = string.Format(streakUnlockFormat, unlockLevel);
                if (giftRoot != null) giftRoot.SetActive(false);              // no gift preview while locked
                if (streakTutorial != null) streakTutorial.SetActive(false);
                if (streakTutorialTapCatcher != null) streakTutorialTapCatcher.gameObject.SetActive(false);
            }
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

        // Show the streak tutorial the FIRST time the streak is unlocked; a tap anywhere (the full-screen
        // catcher) dismisses it and marks it seen so it never shows again.
        private void MaybeShowStreakTutorial()
        {
            if (streakTutorial == null) return;

            if (PlayerPrefs.GetInt(StreakTutorialShownKey, 0) == 1)
            {
                streakTutorial.SetActive(false);
                if (streakTutorialTapCatcher != null) streakTutorialTapCatcher.gameObject.SetActive(false);
                return;
            }

            streakTutorial.SetActive(true);
            if (streakTutorialTapCatcher != null)
            {
                streakTutorialTapCatcher.gameObject.SetActive(true);
                streakTutorialTapCatcher.onClick.RemoveAllListeners();
                streakTutorialTapCatcher.onClick.AddListener(DismissStreakTutorial);
            }
        }

        private void DismissStreakTutorial()
        {
            PlayerPrefs.SetInt(StreakTutorialShownKey, 1);
            PlayerPrefs.Save();
            if (streakTutorial != null) streakTutorial.SetActive(false);
            if (streakTutorialTapCatcher != null) streakTutorialTapCatcher.gameObject.SetActive(false);
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
