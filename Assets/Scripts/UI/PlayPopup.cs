using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Pre-level popup shown when the player presses Start on the main menu: which level they're
    /// about to play and their current streak. Its Play button begins the level via
    /// <see cref="MainMenuController.StartGame"/> (which spends the life + hands off to the Game
    /// scene). Closing returns to the menu without starting.
    ///
    /// <para>Opened with <c>PopupService.Instance.Create&lt;PlayPopup&gt;(p =&gt; p.Bind(menu))</c> —
    /// the <see cref="Bind"/> callback supplies the menu reference, which a serialized field could
    /// not (the popup is spawned in the persistent InitializeScene, the menu lives in MenuScene).</para>
    /// </summary>
    public class PlayPopup : BasePopup
    {
        [Header("Play")]
        [SerializeField] private Button playButton;

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

        /// <summary>Supply the menu controller whose StartGame() begins the level.</summary>
        public void Bind(MainMenuController menuController) => menu = menuController;

        protected override void OnInit()
        {
            if (playButton != null) { playButton.onClick.RemoveAllListeners(); playButton.onClick.AddListener(OnPlay); }

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
            // Close ourselves first; StartGame then spends the life and either hands off to the Game
            // scene, or (out of lives) queues the Out-Of-Lives popup, which opens once we've closed.
            Close();
            if (menu != null) menu.StartGame();
            else Debug.LogWarning("[PlayPopup] Not bound to a MainMenuController — can't start the level.");
        }
    }
}
