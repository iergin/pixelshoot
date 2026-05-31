using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [Header("Fail panel")]
        [SerializeField] private GameObject failPanel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button playOnButton;
        [Tooltip("Optional. If set, shows the revive cost as 'Play On (N)' so the player knows the price.")]
        [SerializeField] private TMP_Text playOnCostLabel;
        [Tooltip("Format for the revive-cost label. {0} = cost in coins.")]
        [SerializeField] private string playOnCostFormat = "Play On ({0})";

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
        }

        private void HandleWon()
        {
            if (failPanel != null) failPanel.SetActive(false);
            if (successPanel != null) successPanel.SetActive(true);
        }

        private void HandleFailed()
        {
            if (successPanel != null) successPanel.SetActive(false);
            if (failPanel != null) failPanel.SetActive(true);
            UpdatePlayOnButton();
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
            if (gameController != null) gameController.ReloadScene();
        }

        private void OnPlayOn()
        {
            if (gameController == null) return;
            // PlayOn now spends coins and returns false if the player can't afford it.
            // Only hide the fail panel when it actually succeeded.
            if (gameController.PlayOn() && failPanel != null) failPanel.SetActive(false);
        }
    }
}
