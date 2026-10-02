using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Ads;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Shows / hides the Level Success and Level Fail panels and wires their buttons
    /// to the right GameController actions. Hook the panels and buttons in the
    /// inspector or via SampleSceneBuilder; this controller only contains glue.
    /// </summary>
    public class LevelEndUIController : MonoBehaviour
    {
        [SerializeField] private GameController gameController;
        [Header("Success panel")]
        [SerializeField] private GameObject successPanel;
        [SerializeField] private Button nextLevelButton;
        [Tooltip("After completing THIS level, every following level's Next Level returns to the MENU " +
                 "instead of loading the next level directly (levels up to & including this one go " +
                 "straight through). e.g. 3 → levels 1-3 direct, level 4+ always via the menu. 0 = never.")]
        [SerializeField, Min(0)] private int menuAfterLevel = 3;
        [Tooltip("Optional. Shows the coins earned this level, e.g. '+20'. {0} = amount. Updates to the doubled value if the 2× ad is watched.")]
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private string rewardFormat = "+{0}";
        [Tooltip("Optional 'Watch ad for 2× coins' button on the success panel.")]
        [SerializeField] private Button doubleCoinsButton;
        [Tooltip("Optional. Shows the would-be reward, e.g. 'Get 40 coins'. {0} = doubled reward.")]
        [SerializeField] private TMP_Text doubleCoinsLabel;
        [SerializeField] private string doubleCoinsFormat = "Get {0}";

        [Header("Success sequence (win → confetti → camera → panel)")]
        [Tooltip("Confetti burst played the instant the level is won.")]
        [SerializeField] private ParticleSystem confetti;
        [Tooltip("Seconds to let the confetti play before blending to the success camera.")]
        [SerializeField] private float confettiLeadTime = 0.4f;
        [Tooltip("Cinemachine success vcam GameObject — activated to blend the camera. Give it a higher Priority than the gameplay vcam.")]
        [SerializeField] private GameObject successCamera;
        [Tooltip("Seconds to wait for the camera blend to finish before showing the success panel (match the CinemachineBrain blend time).")]
        [SerializeField] private float cameraTransitionDuration = 1f;

        [Header("Ads")]
        [Tooltip("Optional same-scene reference; in the two-scene setup it lives in InitializeScene, " +
                 "so we fall back to InterstitialController.Instance.")]
        [SerializeField] private InterstitialController interstitial;
        private InterstitialController Interstitial => interstitial != null ? interstitial : InterstitialController.Instance;
        [SerializeField] private CoinsConfig coinsConfig;
  

        public void Bind(GameController gc)
        {
            UnsubscribeGame();
            gameController = gc;
            SubscribeGame();
        }

        private void Awake()
        {
            HideAll();
            HookButtons();
        }

        private void OnEnable()
        {
            SubscribeGame();
            PlayerWallet.OnBalanceChanged += HandleBalanceChanged;
        }
        private void OnDisable()
        {
            UnsubscribeGame();
            PlayerWallet.OnBalanceChanged -= HandleBalanceChanged;
        }

        private void SubscribeGame()
        {
            if (gameController == null) return;
            gameController.OnLevelWon += HandleWon;
            gameController.OnLevelFailed += HandleFailed;
            gameController.OnPlayOnDenied += HandlePlayOnDenied;
        }

        private void UnsubscribeGame()
        {
            if (gameController == null) return;
            gameController.OnLevelWon -= HandleWon;
            gameController.OnLevelFailed -= HandleFailed;
            gameController.OnPlayOnDenied -= HandlePlayOnDenied;
        }

        private void HandleBalanceChanged(int _) => UpdatePlayOnButton();

        private void HandlePlayOnDenied()
        {
            // Wallet shake / SFX hook could go here. For now we just refresh the button
            // so the disabled state reflects the (unchanged) balance.
            UpdatePlayOnButton();
        }

        private void UpdatePlayOnButton()
        {

        }

        private void HookButtons()
        {
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveAllListeners();
                nextLevelButton.onClick.AddListener(OnNextLevel);
            }
            if (doubleCoinsButton != null)
            {
                doubleCoinsButton.onClick.RemoveAllListeners();
                doubleCoinsButton.onClick.AddListener(OnDoubleCoins);
            }
        }

        private bool doubleCoinsClaimed;

        private void HandleWon()
        {
            StopAllCoroutines();
            StartCoroutine(SuccessSequence());
        }

        /// <summary>Win celebration: confetti burst → blend to the success camera → then
        /// (once the blend finishes) show the success panel.</summary>
        private IEnumerator SuccessSequence()
        {
            // 1) Confetti.
            if (confetti != null)
            {
                if (!confetti.gameObject.activeSelf) confetti.gameObject.SetActive(true);
                confetti.Play(true);
            }
            if (confettiLeadTime > 0f) yield return new WaitForSecondsRealtime(confettiLeadTime);

            // 2) Switch to the success Cinemachine camera (higher priority → brain blends).
            if (successCamera != null) successCamera.SetActive(true);
            if (cameraTransitionDuration > 0f) yield return new WaitForSecondsRealtime(cameraTransitionDuration);

            // 3) Camera settled → reveal the success panel.
            ShowSuccessPanel();
        }

        private void ShowSuccessPanel()
        {
            if (successPanel != null) successPanel.SetActive(true);

            // "You earned" text — the actual reward paid (base × difficulty multiplier).
            int reward = LevelLoader.LastWinReward;
            if (rewardLabel != null) rewardLabel.text = string.Format(rewardFormat, reward);

            // Set up the 2× button. Re-enabled each win in case the previous win consumed it.
            doubleCoinsClaimed = false;
            if (doubleCoinsButton != null) doubleCoinsButton.interactable = true;
            if (doubleCoinsLabel != null)
                doubleCoinsLabel.text = string.Format(doubleCoinsFormat, reward * 2);

            // NOTE: the interstitial no longer fires here — it would cover the success panel. It's now
            // triggered when the player presses Next Level (OnNextLevel), like the fail flow.
        }

        private void HandleFailed()
        {
            if (successPanel != null) successPanel.SetActive(false);
            // NOTE: the interstitial no longer fires here — it would cover the fail popup. It's now
            // triggered at the END of the fail chain (FailFlowPopup.Finish, after all X taps).
            if (PopupService.Instance != null)
                PopupService.Instance.Create<FailFlowPopup>(p => p.SetMode(FailFlowPopup.Mode.Fail));
        }
        public void HideAll()
        {
            if (successPanel != null) successPanel.SetActive(false);
        }

        private void OnNextLevel()
        {
            // Interstitial fires now (after Next Level, not over the success panel). The reload runs
            // in the callback — ONLY after the ad closes — so the level never rebuilds under the ad
            // (which left buses unclickable).
            if (Interstitial != null) Interstitial.NotifyLevelEnded(ProceedToNext);
            else ProceedToNext();
        }

        private void ProceedToNext()
        {
            // Once past menuAfterLevel, EVERY level returns to the menu instead of loading the next
            // one directly. PlayerProgress already advanced on the win, so LevelIndex == the 1-based
            // number of the level just completed.
            int completed = PlayerProgress.LevelIndex;
            if (menuAfterLevel > 0 && completed > menuAfterLevel)
            {
                if (SceneFlow.Instance != null) { SceneFlow.Instance.LoadMainMenu(); return; }
            }
            if (gameController != null) gameController.ReloadScene();
        }

        private void OnRestart()
        {
            if (gameController == null) return;

            // Restarting = abandoning this (failed) attempt → the streak is broken.
            PlayerStreak.Reset();

            // A restart is a fresh attempt, so it costs a life.
            if (PlayerLives.TryConsumeForLevelStart())
            {
                // Has a life → replay the level directly. Two-scene: this reloads just the Game
                // scene (SceneFlow.ReloadGame) — the menu is never involved, so no auto-start flag.
                gameController.ReloadScene();
            }
            else
            {
                // No lives → go home and surface the out-of-lives popup there.
                MainMenuController.PendingOutOfLives = true;
                if (SceneFlow.Instance != null) SceneFlow.Instance.LoadMainMenu();
                else gameController.ReloadScene();
            }
        }
        
        /// <summary>
        /// "Watch ad for 2× coins" handler. On rewarded success, grants ONE additional
        /// reward on top of the one LevelLoader already paid out, so total = 2×.
        /// Disables the button afterwards so it can't be re-claimed for the same win.
        /// Also tells the interstitial controller to reset its cooldown.
        /// </summary>
        private void OnDoubleCoins()
        {
            if (doubleCoinsClaimed || coinsConfig == null) return;
            if (AdsManager.Service == null || !AdsManager.Service.IsRewardedReady)
            {
                Debug.Log("[LevelEndUI] Rewarded ad not ready.");
                return;
            }
            AdsManager.Service.ShowRewarded(
                onRewarded: () =>
                {
                    doubleCoinsClaimed = true;
                    if (doubleCoinsButton != null) doubleCoinsButton.interactable = false;
                    int reward = LevelLoader.LastWinReward; // the actual paid amount (× difficulty)
                    PlayerWallet.Add(reward, "level_win_double"); // grant the same again → total 2× (first was paid by LevelLoader)
                    if (rewardLabel != null) rewardLabel.text = string.Format(rewardFormat, reward * 2);
                    Interstitial?.NotifyRewardedWatched();
                    Debug.Log($"[LevelEndUI] 2× reward claimed (+{reward}).");
                },
                onClosed: null);
        }
    }
}
