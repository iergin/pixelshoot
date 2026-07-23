using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// The "quit this level" confirmation flow. Pressing Quit opens a chain of acknowledgement
    /// popups; each popup's X advances the chain, and the final X actually leaves to the main menu:
    ///
    /// <list type="number">
    /// <item><b>Streak popup</b> — "you'll lose your N streak" (shown only if there's a streak). X → next.</item>
    /// <item><b>Life popup</b> — "you'll lose 1 life" (the life spent entering the level is forfeit). X → quit.</item>
    /// </list>
    ///
    /// Quitting resets the streak. The life was already spent at level start, so there's nothing to
    /// deduct — the popup just informs. Any assigned <see cref="cancelButtons"/> (a "stay" button or
    /// backdrop) close the flow without quitting.
    /// </summary>
    public class QuitFlowController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameController gameController;
        [Tooltip("Quit button in the level HUD — opens the flow.")]
        [SerializeField] private Button quitButton;

        [Header("Streak-loss popup")]
        [SerializeField] private GameObject streakPopup;
        [Tooltip("X on the streak popup — advances to the life popup.")]
        [SerializeField] private Button streakPopupClose;
        [SerializeField] private TMP_Text streakPopupLabel;
        [SerializeField] private string streakFormat = "You'll lose your {0} streak!";

        [Header("Life-loss popup")]
        [SerializeField] private GameObject lifePopup;
        [Tooltip("X on the life popup — actually quits to the main menu.")]
        [SerializeField] private Button lifePopupClose;

        [Header("Stay in level (optional)")]
        [Tooltip("Buttons/backdrops that close the flow WITHOUT quitting (e.g. a 'Stay' button).")]
        [SerializeField] private Button[] cancelButtons;

        private void Awake()
        {
            if (quitButton != null)       { quitButton.onClick.RemoveAllListeners();       quitButton.onClick.AddListener(BeginQuitFlow); }
            if (streakPopupClose != null) { streakPopupClose.onClick.RemoveAllListeners(); streakPopupClose.onClick.AddListener(OnStreakClosed); }
            if (lifePopupClose != null)   { lifePopupClose.onClick.RemoveAllListeners();   lifePopupClose.onClick.AddListener(OnLifeClosed); }
            if (cancelButtons != null)
                foreach (var b in cancelButtons)
                    if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(Cancel); }
            HideAll();
        }

        /// <summary>Start the quit acknowledgement chain (skip the streak popup if there's none).
        /// PUBLIC so it can be triggered from the HUD Quit button OR the fail panel's exit button —
        /// wire either one to this (via the button's OnClick, or an assigned quitButton).</summary>
        public void BeginQuitFlow()
        {
            if (PlayerStreak.Current > 0) ShowStreakPopup();
            else ShowLifePopup();
        }

        private void ShowStreakPopup()
        {
            if (streakPopupLabel != null) streakPopupLabel.text = string.Format(streakFormat, PlayerStreak.Current);
            if (lifePopup != null) lifePopup.SetActive(false);
            if (streakPopup != null) streakPopup.SetActive(true);
        }

        private void OnStreakClosed() => ShowLifePopup();

        private void ShowLifePopup()
        {
            if (streakPopup != null) streakPopup.SetActive(false);
            if (lifePopup != null) lifePopup.SetActive(true);
        }

        private void OnLifeClosed() => ConfirmQuit();

        private void ConfirmQuit()
        {
            HideAll();
            PlayerStreak.Reset(); // quitting breaks the streak (the entered-level life is already spent)
            // Two-scene: go to the menu scene. Single-scene fallback: reload → the menu comes back.
            if (SceneFlow.Instance != null) SceneFlow.Instance.LoadMainMenu();
            else if (gameController != null) gameController.ReloadScene();
            else Debug.LogWarning("[QuitFlow] No SceneFlow and no GameController — can't return to the menu.");
        }

        /// <summary>Close the flow and stay in the level (wired to a Stay button / backdrop).</summary>
        public void Cancel() => HideAll();

        private void HideAll()
        {
            if (streakPopup != null) streakPopup.SetActive(false);
            if (lifePopup != null) lifePopup.SetActive(false);
        }
    }
}
