using UnityEngine;
using UnityEngine.InputSystem;
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
        [Tooltip("Seconds of no input before the shootable-box outline hint appears.")]
        [SerializeField, Min(0f)] private float idleSeconds = 2f;

        private float idle;
        private bool shown;

        private void OnDisable() => Clear();

        private void Update()
        {
            // Any press, or a modal/booster suspend, resets the timer and clears the hint.
            if (AnyPress() || ClickInputRouter.Suspended)
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

        private static bool AnyPress()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) return true;
            return false;
        }
    }
}
