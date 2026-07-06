using UnityEngine;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Shows the current life count and a mm:ss countdown to the next regenerated life
    /// (or a "full" label at the cap). Works with either a TMP_Text or a plain UI Text via
    /// the TMP field — plug in what you use. Refreshes once a second and on every change.
    /// </summary>
    public class LivesHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text livesLabel;
        [SerializeField] private TMP_Text timerLabel;
        [Tooltip("Format for the life count. {0} = current lives, {1} = max.")]
        [SerializeField] private string livesFormat = "{0}";
        [Tooltip("Shown on the timer label while at the cap.")]
        [SerializeField] private string fullText = "FULL";

        private float pollTimer;

        private void OnEnable()
        {
            PlayerLives.OnChanged += OnLivesChanged;
            Refresh();
        }

        private void OnDisable() => PlayerLives.OnChanged -= OnLivesChanged;

        private void OnLivesChanged(int _) => Refresh();

        private void Update()
        {
            // Tick the countdown ~once a second (unscaled so it runs while paused).
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer <= 0f) { pollTimer = 1f; Refresh(); }
        }

        private void Refresh()
        {
            int lives = PlayerLives.Lives;
            if (livesLabel != null) livesLabel.text = string.Format(livesFormat, lives, PlayerLives.MaxLives);
            if (timerLabel == null) return;

            if (lives >= PlayerLives.MaxLives)
            {
                timerLabel.text = fullText;
                return;
            }
            float s = PlayerLives.SecondsUntilNextLife();
            int m = Mathf.FloorToInt(s / 60f);
            int sec = Mathf.FloorToInt(s % 60f);
            timerLabel.text = $"{m:00}:{sec:00}";
        }
    }
}
