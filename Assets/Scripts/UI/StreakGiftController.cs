using UnityEngine;
using DG.Tweening;
using PixelShoot.Game;
using PixelShoot.Grid;
using PixelShoot.Boosters;

namespace PixelShoot.UI
{
    /// <summary>
    /// Applies the start-of-level STREAK gifts the moment gameplay begins (subscribes to
    /// <see cref="MainMenuController.OnGameStarted"/> so the gifts land AFTER the player is looking
    /// at the grid, not while the menu is up). Gift size scales with <see cref="PlayerStreak"/>:
    ///   • bombs  = <see cref="PlayerStreak.RewardBombs"/> (0/3/6/9)
    ///   • paints = <see cref="PlayerStreak.RewardPaints"/> (0/5/15/25) — wired in a later phase.
    /// </summary>
    public class StreakGiftController : MonoBehaviour
    {
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private GridController grid;
        [Tooltip("FillColor controller reused to run the paint stickmen in from off-screen.")]
        [SerializeField] private FillColorController fill;
        [Tooltip("Delay after gameplay begins before the streak bombs pop in.")]
        [SerializeField, Min(0f)] private float bombDelay = 0.4f;
        [Tooltip("Delay before the paint runners start — set past the bombs so paint skips the just-placed bombs.")]
        [SerializeField, Min(0f)] private float paintDelay = 1.2f;

        private void OnEnable()  { if (mainMenu != null) mainMenu.OnGameStarted += HandleGameStarted; }
        private void OnDisable() { if (mainMenu != null) mainMenu.OnGameStarted -= HandleGameStarted; }

        private void HandleGameStarted()
        {
            int bombs  = PlayerStreak.RewardBombs;
            int paints = PlayerStreak.RewardPaints;
            Debug.Log($"[StreakGift] Level started — streak {PlayerStreak.Current} → {bombs} bomb(s), {paints} paint(s).");

            if (bombs > 0 && grid != null)
                DOVirtual.DelayedCall(bombDelay, () => { if (grid != null) grid.PlaceStreakBombs(bombs); });

            if (paints > 0 && fill != null)
                DOVirtual.DelayedCall(paintDelay, () =>
                {
                    if (fill != null) fill.StreakPaint(paints); // unlinked-safe: never strands a linked bus
                });
        }
    }
}
