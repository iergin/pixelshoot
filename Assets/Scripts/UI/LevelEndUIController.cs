using UnityEngine;
using UnityEngine.UI;
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

        private void OnEnable() => SubscribeGame();
        private void OnDisable() => UnsubscribeGame();

        private void SubscribeGame()
        {
            if (gameController == null) return;
            gameController.OnLevelWon += HandleWon;
            gameController.OnLevelFailed += HandleFailed;
        }

        private void UnsubscribeGame()
        {
            if (gameController == null) return;
            gameController.OnLevelWon -= HandleWon;
            gameController.OnLevelFailed -= HandleFailed;
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
            gameController.PlayOn();
            if (failPanel != null) failPanel.SetActive(false);
        }
    }
}
