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
        [Tooltip("Optional cannons for PURCHASED powerups: Cannon lobs paint stickmen, BombCannon lobs " +
                 "bombs (DOJump). If assigned, the purchased powerups are DELIVERED by the cannons instead " +
                 "of the off-screen run / instant pop. Leave empty to keep the old delivery.")]
        [SerializeField] private PixelShoot.Boosters.PowerupCannonCoordinator cannons;
        [Tooltip("Fixed amounts one purchased powerup grants (bombs / paints). Used when the player " +
                 "selected a powerup for this level in the PlayPopup.")]
        [SerializeField] private PowerupsConfig powerupsConfig;
        [Tooltip("Delay after gameplay begins before the streak bombs pop in.")]
        [SerializeField, Min(0f)] private float bombDelay = 0.4f;
        [Tooltip("Delay before the paint runners start — set past the bombs so paint skips the just-placed bombs.")]
        [SerializeField, Min(0f)] private float paintDelay = 1.2f;
        [Tooltip("Extra delay before the PURCHASED-POWERUP bombs/paint activate, so they read as a " +
                 "separate second beat AFTER the streak gift (not applied together).")]
        [SerializeField, Min(0f)] private float powerupDelay = 1.0f;

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
            // Streak gift (from StreakConfig) and purchased powerups (from PowerupsConfig) are separate
            // effects with separate amounts, applied on their own timing with a delay between them.
            int streakBombs  = PlayerStreak.RewardBombs;
            int streakPaints = PlayerStreak.RewardPaints;

            int puBombs = 0, puPaints = 0;
            if (powerupsConfig != null)
            {
                if (PlayerPowerups.IsSelected(PowerupType.Bomb) && PlayerPowerups.TryConsume(PowerupType.Bomb))
                    puBombs = powerupsConfig.bombsPerPowerup;
                if (PlayerPowerups.IsSelected(PowerupType.Paint) && PlayerPowerups.TryConsume(PowerupType.Paint))
                    puPaints = powerupsConfig.paintsPerPowerup;
            }
            PlayerPowerups.ClearSelections();

            Debug.Log($"[StreakGift] streak → {streakBombs} bomb / {streakPaints} paint; " +
                      $"powerups → {puBombs} bomb / {puPaints} paint (delay {powerupDelay}s). " +
                      $"cannons={(cannons != null ? "ASSIGNED → cannon delivery" : "NULL → OLD delivery (off-screen run / pop)")}.");

            int totalBombs  = streakBombs + puBombs;
            int totalPaints = streakPaints + puPaints;

            if (cannons != null && (totalBombs > 0 || totalPaints > 0))
            {
                // CANNON delivery for the WHOLE gift (streak + purchased powerups). Pick targets NOW
                // (reserves paint boxes + spends their shots so the bullet budget stays balanced); the
                // cannons lob them after the delay — paint via Cannon, bombs via BombCannon (→ MakeBomb).
                var paintTargets = (totalPaints > 0 && fill != null) ? fill.PickStreakPaintTargets(totalPaints) : null;
                var bombTargets  = (totalBombs  > 0 && grid != null) ? grid.CollectRandomBombTargets(totalBombs)  : null;
                DOVirtual.DelayedCall(bombDelay, () =>
                    cannons.FireBoth(paintTargets, bombTargets, grid,
                        onBombLanded: b => { if (b != null) b.MakeBomb(); }));
            }
            else
            {
                // Classic delivery: streak gift first, purchased powerups a beat later.
                ApplyGift(streakBombs, streakPaints, bombDelay, paintDelay);
                ApplyGift(puBombs, puPaints, bombDelay + powerupDelay, paintDelay + powerupDelay);
            }
        }

        // Place bombs + run paint with the given start delays (bombs before paint so paint skips them).
        private void ApplyGift(int bombs, int paints, float bombAt, float paintAt)
        {
            if (bombs > 0 && grid != null)
                DOVirtual.DelayedCall(bombAt, () => { if (grid != null) grid.PlaceStreakBombs(bombs); });

            if (paints > 0 && fill != null)
                DOVirtual.DelayedCall(paintAt, () => { if (fill != null) fill.StreakPaint(paints); }); // unlinked-safe
        }
    }
}
