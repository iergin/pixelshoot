using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Casts a ray on left mouse click and forwards the hit to ShooterClickHandler.
    /// Uses the new Input System.
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
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            var screen = Mouse.current.position.ReadValue();
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, rayDistance, clickMask))
            {
                var handler = hit.collider.GetComponentInParent<ShooterClickHandler>();
                if (handler != null) handler.NotifyClicked();
            }
        }
    }
}
