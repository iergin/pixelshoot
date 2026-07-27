using UnityEngine;
using DG.Tweening;
using PixelShoot.Game;
using PixelShoot.Grid;
using PixelShoot.Boosters;

namespace PixelShoot.UI
{
    /// <summary>
    /// Applies the start-of-level STREAK gifts once the level is built (subscribes to
    /// <see cref="GameController.OnLevelReady"/> — a GAME-scene signal, so it keeps working with the
    /// menu in a separate scene). Gift size scales with <see cref="PlayerStreak"/>:
    ///   • bombs  = <see cref="PlayerStreak.RewardBombs"/> (0/3/6/9)
    ///   • paints = <see cref="PlayerStreak.RewardPaints"/> (0/5/15/25)
    /// </summary>
    public class StreakGiftController : MonoBehaviour
    {
        [Tooltip("The Game scene's GameController — we trigger the gifts on its OnLevelReady (so this " +
                 "works with the menu in a separate scene).")]
        [SerializeField] private GameController gameController;
        [SerializeField] private GridController grid;
        [Tooltip("FillColor controller reused to run the paint stickmen in from off-screen.")]
        [SerializeField] private FillColorController fill;
        [Tooltip("Fixed amounts one purchased powerup grants (bombs / paints). Used when the player " +
                 "selected a powerup for this level in the PlayPopup.")]
        [SerializeField] private PowerupsConfig powerupsConfig;
        [Tooltip("Delay after gameplay begins before the streak bombs pop in.")]
        [SerializeField, Min(0f)] private float bombDelay = 0.4f;
        [Tooltip("Delay before the paint runners start — set past the bombs so paint skips the just-placed bombs.")]
        [SerializeField, Min(0f)] private float paintDelay = 1.2f;

        [Tooltip("Extra seconds after the paint before logging the settled balance (runners must land first).")]
        [SerializeField, Min(0f)] private float paintSettleLog = 4f;

        private void OnEnable()
        {
            if (gameController != null) gameController.OnLevelReady += HandleLevelReady;
            if (grid != null) grid.OnGridCleared += LogFinalBalance;
        }

        private void OnDisable()
        {
            if (gameController != null) gameController.OnLevelReady -= HandleLevelReady;
            if (grid != null) grid.OnGridCleared -= LogFinalBalance;
        }

        private void LogFinalBalance() =>
            Debug.Log($"[STREAKBAL] GRID CLEARED — leftover shots on shooters = {PixelShoot.Shooters.ShooterColumn.TotalShots()} (should be 0).");

        private void HandleLevelReady()
        {
            int bombs  = PlayerStreak.RewardBombs;
            int paints = PlayerStreak.RewardPaints;

            // Purchased powerups the player selected in the PlayPopup for THIS level: consume + add on
            // top of the streak gift, then clear the selections so they don't carry over.
            if (powerupsConfig != null)
            {
                if (PlayerPowerups.IsSelected(PowerupType.Bomb) && PlayerPowerups.TryConsume(PowerupType.Bomb))
                    bombs += powerupsConfig.bombsPerPowerup;
                if (PlayerPowerups.IsSelected(PowerupType.Paint) && PlayerPowerups.TryConsume(PowerupType.Paint))
                    paints += powerupsConfig.paintsPerPowerup;
            }
            PlayerPowerups.ClearSelections();

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
