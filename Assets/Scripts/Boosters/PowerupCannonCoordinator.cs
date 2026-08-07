using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using PixelShoot.Grid;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Fires the two powerup cannons together when both powerups are granted: the <see cref="Cannon"/>
    /// (stickmen) and the <see cref="BombCannon"/> (bombs) start at (nearly) the same time, one a short
    /// beat before the other. By default the stickman cannon leads by <see cref="secondStartDelay"/>.
    /// Also exposes single-cannon fires for when only one powerup is granted.
    /// </summary>
    public class PowerupCannonCoordinator : MonoBehaviour
    {
        [SerializeField] private Cannon stickmanCannon;
        [SerializeField] private BombCannon bombCannon;
        [Tooltip("When BOTH cannons fire, the second one starts this many seconds after the first.")]
        [SerializeField, Min(0f)] private float secondStartDelay = 0.4f;
        [Tooltip("On = the stickman (Cannon) starts first, the BombCannon follows after the delay.")]
        [SerializeField] private bool stickmanFirst = true;

        /// <summary>Fire only the stickman cannon at the given targets.</summary>
        public void FireStickmen(IList<Box> targets, GridController grid, Action onDone = null)
        {
            if (stickmanCannon != null) stickmanCannon.Fire(targets, grid, onDone);
            else onDone?.Invoke();
        }

        /// <summary>Fire only the bomb cannon at the given targets.</summary>
        public void FireBombs(IList<Box> targets, GridController grid, Action<Box> onBombLanded, Action onDone = null)
        {
            if (bombCannon != null) bombCannon.Fire(targets, grid, onBombLanded, onDone);
            else onDone?.Invoke();
        }

        /// <summary>Fire BOTH cannons at once, offset by <see cref="secondStartDelay"/>. Either target
        /// list may be null/empty (that cannon simply doesn't fire).</summary>
        public void FireBoth(IList<Box> stickmanTargets, IList<Box> bombTargets, GridController grid,
            Action<Box> onBombLanded, Action onDone = null)
        {
            bool hasStick = stickmanCannon != null && stickmanTargets != null && stickmanTargets.Count > 0;
            bool hasBomb  = bombCannon != null && bombTargets != null && bombTargets.Count > 0;

            Debug.Log($"[CannonCoord] FireBoth — stickmanCannon={(stickmanCannon != null ? "OK" : "NULL")} " +
                      $"targets={stickmanTargets?.Count ?? 0} → hasStick={hasStick}; " +
                      $"bombCannon={(bombCannon != null ? "OK" : "NULL")} targets={bombTargets?.Count ?? 0} → hasBomb={hasBomb}");

            int remaining = (hasStick ? 1 : 0) + (hasBomb ? 1 : 0);
            if (remaining == 0) { onDone?.Invoke(); return; }
            Action one = () => { if (--remaining <= 0) onDone?.Invoke(); };

            // Only one present → just fire it now.
            if (hasStick ^ hasBomb)
            {
                if (hasStick) stickmanCannon.Fire(stickmanTargets, grid, one);
                else bombCannon.Fire(bombTargets, grid, onBombLanded, one);
                return;
            }

            // Both present → lead one, delay the other.
            Action fireStick = () => stickmanCannon.Fire(stickmanTargets, grid, one);
            Action fireBomb  = () => bombCannon.Fire(bombTargets, grid, onBombLanded, one);

            Action lead   = stickmanFirst ? fireStick : fireBomb;
            Action follow = stickmanFirst ? fireBomb : fireStick;

            lead();
            if (secondStartDelay > 0f) DOVirtual.DelayedCall(secondStartDelay, () => follow());
            else follow();
        }
    }
}
