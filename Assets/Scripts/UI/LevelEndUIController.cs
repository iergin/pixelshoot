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
        [Header("Fail panel")]
        [SerializeField] private GameObject failPanel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button playOnButton;
        [Tooltip("Optional. If set, shows the revive cost as 'Play On (N)' so the player knows the price.")]
        [SerializeField] private TMP_Text playOnCostLabel;
        [Tooltip("Format for the revive-cost label. {0} = cost in coins.")]
        [SerializeField] private string playOnCostFormat = "Play On ({0})";
        [Tooltip("Optional root shown on fail ONLY when the player has a streak at risk (hidden if streak is 0).")]
        [SerializeField] private GameObject streakLossRoot;
        [Tooltip("Optional label warning the streak will be lost. {0} = current streak count.")]
        [SerializeField] private TMP_Text streakLossLabel;
        [SerializeField] private string streakLossFormat = "You'll lose your {0} streak!";

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
            if (gameController == null) return;
            int cost = gameController.ReviveCost;
            bool canAfford = gameController.CanAffordRevive;

            if (playOnButton != null) playOnButton.interactable = canAfford;
            if (playOnCostLabel != null) playOnCostLabel.text = string.Format(playOnCostFormat, cost);
        }

        private void HookButtons()
        {
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveAllListeners();
                nextLevelButton.onClick.AddListener(OnNextLevel);
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestart);
            }
            if (playOnButton != null)
            {
                playOnButton.onClick.RemoveAllListeners();
                playOnButton.onClick.AddListener(OnPlayOn);
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
            if (failPanel != null) failPanel.SetActive(false);
            // Don't pop the panel yet — play the celebration first, THEN show it.
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

            // "You earned" text — the base win reward (matches what LevelLoader paid).
            int reward = coinsConfig != null ? coinsConfig.LevelWinReward : 0;
            if (rewardLabel != null) rewardLabel.text = string.Format(rewardFormat, reward);

            // Set up the 2× button. Re-enabled each win in case the previous win consumed it.
            doubleCoinsClaimed = false;
            if (doubleCoinsButton != null) doubleCoinsButton.interactable = true;
            if (doubleCoinsLabel != null && coinsConfig != null)
                doubleCoinsLabel.text = string.Format(doubleCoinsFormat, coinsConfig.LevelWinReward * 2);

            // Fire the interstitial gate now (with the panel), not over the celebration.
            Interstitial?.NotifyLevelEnded();
        }

        private void HandleFailed()
        {
            if (successPanel != null) successPanel.SetActive(false);
            // Interstitial counter ticks for losses too — the player saw a level result.
            Interstitial?.NotifyLevelEnded();
            // Fail now runs through the chained FailFlowPopup (continue → streak/life warnings → try again).
            if (PopupService.Instance != null)
                PopupService.Instance.Create<FailFlowPopup>(p => p.SetMode(FailFlowPopup.Mode.Fail));
            else if (failPanel != null)
                failPanel.SetActive(true); // single-scene fallback: old panel
        }

        // Warn (only if there's a streak to lose) that failing out will break it. The streak isn't
        // reset here — it's only lost if the player leaves via Restart/Quit; Play On preserves it.
        private void ShowStreakAtRisk()
        {
            int streak = PlayerStreak.Current;
            if (streakLossLabel != null && streak > 0)
                streakLossLabel.text = string.Format(streakLossFormat, streak);
            if (streakLossRoot != null) streakLossRoot.SetActive(streak > 0);
        }

        public void HideAll()
        {
            if (successPanel != null) successPanel.SetActive(false);
            if (failPanel != null) failPanel.SetActive(false);
        }

        private void OnNextLevel()
        {
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

        private void OnPlayOn()
        {
            if (gameController == null) return;
            // PlayOn now spends coins and returns false if the player can't afford it.
            // Only hide the fail panel when it actually succeeded.
            if (gameController.PlayOn() && failPanel != null) failPanel.SetActive(false);
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
                    PlayerWallet.Add(coinsConfig.LevelWinReward); // first reward already paid by LevelLoader
                    // Reflect the doubled total on the reward text.
                    if (rewardLabel != null) rewardLabel.text = string.Format(rewardFormat, coinsConfig.LevelWinReward * 2);
                    Interstitial?.NotifyRewardedWatched();
                    Debug.Log($"[LevelEndUI] 2× reward claimed (+{coinsConfig.LevelWinReward}).");
                },
                onClosed: null);
        }
    }
}
