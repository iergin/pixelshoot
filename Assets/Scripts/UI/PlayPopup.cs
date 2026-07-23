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

        [Header("Streak")]
        [Tooltip("Optional root hidden when the streak is 0 (nothing to brag about yet).")]
        [SerializeField] private GameObject streakRoot;
        [SerializeField] private TMP_Text streakLabel;
        [SerializeField] private string streakFormat = "Streak: {0}";

        private MainMenuController menu;

        /// <summary>Supply the menu controller whose StartGame() begins the level.</summary>
        public void Bind(MainMenuController menuController) => menu = menuController;

        protected override void OnInit()
        {
            if (playButton != null) { playButton.onClick.RemoveAllListeners(); playButton.onClick.AddListener(OnPlay); }

            if (levelLabel != null) levelLabel.text = string.Format(levelFormat, PlayerProgress.DisplayLevel);

            int streak = PlayerStreak.Current;
            if (streakLabel != null) streakLabel.text = string.Format(streakFormat, streak);
            if (streakRoot != null) streakRoot.SetActive(streak > 0);
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
