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

        [Tooltip("Extra seconds after the paint before logging the settled balance (runners must land first).")]
        [SerializeField, Min(0f)] private float paintSettleLog = 4f;

        private void OnEnable()
        {
            if (mainMenu != null) mainMenu.OnGameStarted += HandleGameStarted;
            if (grid != null) grid.OnGridCleared += LogFinalBalance;
        }

        private void OnDisable()
        {
            if (mainMenu != null) mainMenu.OnGameStarted -= HandleGameStarted;
            if (grid != null) grid.OnGridCleared -= LogFinalBalance;
        }

        private void LogFinalBalance() =>
            Debug.Log($"[STREAKBAL] GRID CLEARED — leftover shots on shooters = {PixelShoot.Shooters.ShooterColumn.TotalShots()} (should be 0).");

        private void HandleGameStarted()
        {
            int bombs  = PlayerStreak.RewardBombs;
            int paints = PlayerStreak.RewardPaints;
            Debug.Log($"[STREAKBAL] Level started — streak {PlayerStreak.Current} → {bombs} bomb(s), {paints} paint(s). " +
                      $"aliveBoxes={(grid != null ? grid.AliveCount : -1)}, totalShots={PixelShoot.Shooters.ShooterColumn.TotalShots()} (should be equal).");

            if (bombs > 0 && grid != null)
                DOVirtual.DelayedCall(bombDelay, () =>
                {
                    if (grid == null) return;
                    grid.PlaceStreakBombs(bombs);
                    Debug.Log($"[STREAKBAL] After bombs placed — aliveBoxes={grid.AliveCount}, totalShots={PixelShoot.Shooters.ShooterColumn.TotalShots()} (bombs don't change counts until detonated).");
                });

            if (paints > 0 && fill != null)
                DOVirtual.DelayedCall(paintDelay, () =>
                {
                    if (fill == null) return;
                    fill.StreakPaint(paints); // unlinked-safe: never strands a linked bus
                    Debug.Log($"[STREAKBAL] Paint fired — aliveBoxes={(grid != null ? grid.AliveCount : -1)}, totalShots={PixelShoot.Shooters.ShooterColumn.TotalShots()} " +
                              "(shots drop NOW, boxes clear when the runners land — see the settled line next).");

                    // The runners land over the next second or two; log the SETTLED numbers, which
                    // are the ones that must match.
                    DOVirtual.DelayedCall(paintSettleLog, () =>
                        Debug.Log($"[STREAKBAL] Paint settled — aliveBoxes={(grid != null ? grid.AliveCount : -1)}, " +
                                  $"totalShots={PixelShoot.Shooters.ShooterColumn.TotalShots()} (THESE must be EQUAL)."));
                });
        }
    }
}
