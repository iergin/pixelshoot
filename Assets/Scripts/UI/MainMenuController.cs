using System;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drives the main menu's flow: Start → PlayPanel/StartGame, lives gating, and handing off to
    /// the Game scene. The open/close ANIMATION is delegated to a <see cref="UiTransitionController"/> —
    /// this class only decides WHEN to play in/out, not how.
    ///
    /// <para>Two-scene setup: gameplay lives in its own <b>Game</b> scene. Start plays the menu-out
    /// animation, then asks <see cref="SceneFlow"/> to swap the menu scene for the Game scene — there
    /// is no in-scene "game panel" to reveal, so the menu never touches gameplay objects directly.</para>
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Roots")]
        [Tooltip("Whole menu canvas/root, deactivated after the start transition completes.")]
        [SerializeField] private GameObject menuRoot;

        [Header("Transition")]
        [Tooltip("Owns the animated groups and plays them in/out. Assign the UiTransitionController " +
                 "that holds the menu's button-parent groups.")]
        [SerializeField] private UiTransitionController transitions;

        [Header("Buttons")]
        [Tooltip("Starts the level flow.")]
        [SerializeField] private Button startButton;
        [Tooltip("Opens the Settings popup (SettingsPopup via PopupService). Optional.")]
        [SerializeField] private Button settingsButton;

        [Header("Pre-level popup")]
        [Tooltip("If on, Start opens a PlayPopup (level # + streak) whose Play button then calls " +
                 "StartGame. If off, Start launches the level directly.")]
        [SerializeField] private bool showPlayPopup = true;

        [Header("Start transition")]
        [Tooltip("Open the menu automatically on scene start.")]
        [SerializeField] private bool openOnStart = true;

        /// <summary>Fired when Start is pressed but the player has no lives left.</summary>
        public event Action OnOutOfLives;

        private bool starting;

        // Cross-reload intent set by Restart-with-no-lives (LevelEndUIController) before it sends the
        // player home: show the menu AND the out-of-lives popup once the MainMenu scene is back up.
        public static bool PendingOutOfLives;

        private void Awake()
        {
            WireButtons();
        }

        private void Start()
        {
            if (openOnStart) Open();

            // Restart-with-no-lives sent us home → surface the out-of-lives popup over the menu.
            if (PendingOutOfLives)
            {
                PendingOutOfLives = false;
                OpenOutOfLives();
            }
        }

        private static void OpenOutOfLives()
        {
            if (PopupService.Instance != null) PopupService.Instance.Create<OutOfLivesPopup>();
            else Debug.LogWarning("[MainMenu] No PopupService — cannot show the Out-Of-Lives popup.");
        }

        private void WireButtons()
        {
            Hook(startButton, "Start", OnStartPressed);
            Hook(settingsButton, "Settings", OpenSettings);
        }

        /// <summary>Open the Settings popup. Public so a NavigationBar / other button can call it too.</summary>
        public void OpenSettings()
        {
            if (PopupService.Instance != null) PopupService.Instance.Create<SettingsPopup>();
            else Debug.LogWarning("[MainMenu] No PopupService — cannot open the Settings popup.");
        }

        private static void Hook(Button b, string label, UnityEngine.Events.UnityAction action)
        {
            if (b == null) { Debug.LogWarning($"[MainMenu] '{label}' button is NOT assigned in the inspector."); return; }
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
            Debug.Log($"[MainMenu] Wired '{label}' onto button '{b.name}' (interactable={b.interactable}).");
        }

        // ── Open / close ──────────────────────────────────────────────────────
        public void Open()
        {
            if (menuRoot != null) menuRoot.SetActive(true);
            starting = false;
            transitions?.PlayIn();
        }

        /// <summary>Start button: open the pre-level PlayPopup (level # + streak) whose Play button
        /// then calls <see cref="StartGame"/>. Falls back to launching directly if the popup is off
        /// or no PopupService is present.</summary>
        private void OnStartPressed()
        {
            if (showPlayPopup && PopupService.Instance != null)
                PopupService.Instance.Create<PlayPopup>(p => p.Bind(this));
            else
                StartGame();
        }

        /// <summary>Actually begin the level: spend a life, play the menu-out animation, then swap to
        /// the Game scene. Called by the PlayPanel's Play button (or directly if no PlayPanel).</summary>
        public void StartGame()
        {
            if (starting) return;

            // A life is spent the instant the level starts — once committed it's gone even
            // if the player quits or backs out. Block the start (and show the out-of-lives
            // UI) when there are none left.
            if (!PlayerLives.TryConsumeForLevelStart())
            {
                Debug.Log("[MainMenu] Start blocked — out of lives. Opening Out-Of-Lives popup.");
                OpenOutOfLives();
                OnOutOfLives?.Invoke();
                return;
            }

            if (SceneFlow.Instance == null)
            {
                Debug.LogWarning("[MainMenu] SceneFlow.Instance is missing — is the InitializeScene loaded? " +
                                 "Cannot hand off to the Game scene.");
                return;
            }

            starting = true;
            // Play the menu out, THEN swap the menu scene for the Game scene (SceneFlow unloads this
            // whole scene once the animation finishes).
            if (transitions != null) transitions.PlayOut(SceneFlow.Instance.LoadGame);
            else SceneFlow.Instance.LoadGame();
        }
    }
}
