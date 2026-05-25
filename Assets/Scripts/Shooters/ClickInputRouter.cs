using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Casts a ray on a tap / left-click and forwards the hit to ShooterClickHandler.
    /// Uses the new Input System's Pointer abstraction so the same code path handles
    /// mouse (editor / desktop) and touchscreen (mobile / Device Simulator).
    /// </summary>
    public class ClickInputRouter : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField] private float rayDistance = 200f;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null) return;

            if (TryReadPress(out Vector2 screen))
                HandlePress(screen);
        }

        // Returns true on a fresh press this frame, populating the screen pos. Works for
        // mouse left-click, touchscreen primary touch, or the generic Pointer fallback.
        private static bool TryReadPress(out Vector2 screen)
        {
            screen = default;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screen = Mouse.current.position.ReadValue();
                return true;
            }
            if (Touchscreen.current != null)
            {
                var t = Touchscreen.current.primaryTouch;
                if (t.press.wasPressedThisFrame)
                {
                    screen = t.position.ReadValue();
                    return true;
                }
            }
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                screen = Pointer.current.position.ReadValue();
                return true;
            }
            return false;
        }

        private void HandlePress(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, rayDistance, clickMask))
            {
                var handler = hit.collider.GetComponentInParent<ShooterClickHandler>();
                if (handler != null) handler.NotifyClicked();
            }
        }
    }
}
