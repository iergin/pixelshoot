using UnityEngine;
using UnityEngine.InputSystem;
using PixelShoot.Conveyor;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    /// <summary>
    /// "You can shoot here" idle hint. If the player gives no input for <see cref="idleSeconds"/>, it
    /// lights the Free Outline on every shootable / outermost box (via <see cref="GridController.SetShootableHint"/>);
    /// the moment any tap arrives — or gameplay input is suspended (a popup / booster is up) — the hint
    /// clears and the timer restarts.
    /// </summary>
    public class IdleHintController : MonoBehaviour
    {
        [SerializeField] private GridController grid;
        [Tooltip("If a bus is riding / boarding the conveyor, the hint stays hidden (the player has " +
                 "something to act on already). Assign the level's ConveyorController.")]
        [SerializeField] private ConveyorController conveyor;
        [Tooltip("Seconds of no input before the shootable-box outline hint appears.")]
        [SerializeField, Min(0f)] private float idleSeconds = 2f;

        private float idle;
        private bool shown;

        private void OnDisable() => Clear();

        private void Update()
        {
            // Any press, a modal/booster suspend, OR a bus on the conveyor → reset the timer + clear
            // the hint (no hint needed while there's something riding the conveyor to act on).
            if (AnyPress() || ClickInputRouter.Suspended || ConveyorHasBus())
            {
                idle = 0f;
                Clear();
                return;
            }

            idle += Time.unscaledDeltaTime;
            if (!shown && idle >= idleSeconds)
            {
                shown = true;
                if (grid != null) grid.SetShootableHint(true);
            }
        }

        private void Clear()
        {
            if (!shown) return;
            shown = false;
            if (grid != null) grid.SetShootableHint(false);
        }

        private bool ConveyorHasBus() => conveyor != null && conveyor.OccupiedCount > 0;

        private static bool AnyPress()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) return true;
            return false;
        }
    }
}
