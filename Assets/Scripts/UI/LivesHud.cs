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
        [Tooltip("Shown as the life COUNT while unlimited lives are active (default = infinity sign).")]
        [SerializeField] private string unlimitedText = "∞";

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
            // Unlimited period → show ∞ as the count and the remaining time on the timer.
            if (PlayerLives.IsUnlimited)
            {
                if (livesLabel != null) livesLabel.text = unlimitedText;
                if (timerLabel != null) timerLabel.text = FormatDuration(PlayerLives.SecondsUntilUnlimitedEnds());
                return;
            }

            int lives = PlayerLives.Lives;
            if (livesLabel != null) livesLabel.text = string.Format(livesFormat, lives, PlayerLives.MaxLives);
            if (timerLabel == null) return;

            if (lives >= PlayerLives.MaxLives)
            {
                timerLabel.text = fullText;
                return;
            }
            timerLabel.text = FormatDuration(PlayerLives.SecondsUntilNextLife());
        }

        // mm:ss, or h:mm:ss once there's an hour or more left (for the unlimited countdown).
        private static string FormatDuration(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        }
    }
}
