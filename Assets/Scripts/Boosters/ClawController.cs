using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Conveyor;
using PixelShoot.Shooters;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// The interactive "Claw" booster. When begun: the conveyor freezes, the camera slides
    /// down (a dedicated Cinemachine vcam), normal gameplay input is suspended, a description
    /// panel appears, and every grabbable column shooter glows. The player taps one and the
    /// claw pulls it (its whole link group if linked) straight onto the conveyor — bypassing
    /// column burial. Locked buses can't be taken; surprises reveal as they're carried. A
    /// grab (or the cancel button) ends the mode and restores everything.
    /// </summary>
    public class ClawController : MonoBehaviour
    {
        [Header("Gameplay links")]
        [SerializeField] private GameController gameController;
        [SerializeField] private ConveyorController conveyor;

        [Header("Selection raycast")]
        [SerializeField] private Camera cam;
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField] private float rayDistance = 200f;

        [Header("Camera")]
        [Tooltip("Cinemachine vcam GameObject enabled while the claw is active (give it higher Priority so the brain blends down to the columns).")]
        [SerializeField] private GameObject clawCamera;

        [Header("Input to suspend")]
        [Tooltip("Gameplay input behaviours (e.g. ClickInputRouter) disabled while the claw runs, so normal boarding taps don't fire.")]
        [SerializeField] private Behaviour[] gameplayInput;

        [Header("UI")]
        [SerializeField] private GameObject descriptionPanel;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("Cancels the claw without grabbing (booster is NOT consumed).")]
        [SerializeField] private Button cancelButton;

        private bool active;
        private bool consumeOnGrab;
        private bool waitingForRelease; // ignore the very tap that opened the mode
        private BoosterData current;
        private readonly List<Shooter> highlighted = new List<Shooter>();
        private bool wired;

        public bool IsActive => active;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            Wire();
        }

        private void Wire()
        {
            if (wired) return;
            wired = true;
            if (cancelButton != null) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(Cancel); }
        }

        /// <summary>Enter claw mode. <paramref name="consume"/> = spend one booster on a
        /// successful grab (false for the free tutorial use).</summary>
        public bool Begin(BoosterData data, bool consume)
        {
            if (active || gameController == null || conveyor == null) return false;
            Wire();

            active = true;
            current = data;
            consumeOnGrab = consume;
            waitingForRelease = true;

            BoosterProcess.Set(true); // lock other booster buttons + hide the bar
            conveyor.IsPaused = true;
            if (clawCamera != null) clawCamera.SetActive(true);
            ClickInputRouter.PushSuspend(); // reliably block normal boarding taps
            SetGameplayInput(false);

            if (descriptionText != null && data != null) descriptionText.text = data.Description;
            if (descriptionPanel != null) descriptionPanel.SetActive(true);

            HighlightGrabbables(true);
            return true;
        }

        public void Cancel() { if (active) End(); }

        private void End()
        {
            HighlightGrabbables(false);
            if (descriptionPanel != null) descriptionPanel.SetActive(false);
            if (clawCamera != null) clawCamera.SetActive(false);
            ClickInputRouter.PopSuspend();
            SetGameplayInput(true);
            if (conveyor != null) conveyor.IsPaused = false;
            active = false;
            current = null;
            BoosterProcess.Set(false); // restore other buttons + bring the bar back
        }

        private void Update()
        {
            if (!active) return;

            // Swallow the opening tap: wait until the pointer is released once before we
            // accept a selection, so the tap on the booster button can't grab immediately.
            if (waitingForRelease)
            {
                if (!PointerHeld()) waitingForRelease = false;
                return;
            }

            if (TryReadPress(out Vector2 screen)) HandlePress(screen);
        }

        private void HandlePress(Vector2 screen)
        {
            // Don't grab through UI (description panel / cancel button).
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var c = cam != null ? cam : Camera.main;
            if (c == null) return;

            var ray = c.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, rayDistance, clickMask))
            {
                var shooter = hit.collider.GetComponentInParent<Shooter>();
                if (shooter != null && gameController.CanClawGrab(shooter))
                {
                    HighlightGrabbables(false);          // stop pulses before the bus flies off
                    gameController.ClawGrab(shooter);
                    if (consumeOnGrab && current != null) PlayerBoosters.TryConsume(current.Id);
                    End();
                    return;
                }
            }
            // Tapped empty space or an ineligible bus → negative cue, stay in claw mode.
            PixelShoot.Audio.AudioManager.Instance?.PlayBlocked();
        }

        // Glow every shooter the claw could currently take (and clear on exit).
        private void HighlightGrabbables(bool on)
        {
            if (!on)
            {
                foreach (var s in highlighted) if (s != null) s.SetClawHighlight(false);
                highlighted.Clear();
                return;
            }

            highlighted.Clear();
            foreach (var col in ShooterColumn.All)
            {
                if (col == null) continue;
                foreach (var s in col.Shooters)
                {
                    if (s == null || highlighted.Contains(s)) continue;
                    if (gameController.CanClawGrab(s)) { s.SetClawHighlight(true); highlighted.Add(s); }
                }
            }
        }

        private void SetGameplayInput(bool enabled)
        {
            if (gameplayInput == null) return;
            foreach (var b in gameplayInput) if (b != null) b.enabled = enabled;
        }

        // ── Pointer helpers (new Input System, mirrors ClickInputRouter) ─────
        private static bool TryReadPress(out Vector2 screen)
        {
            screen = default;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            { screen = Mouse.current.position.ReadValue(); return true; }
            if (Touchscreen.current != null)
            {
                var t = Touchscreen.current.primaryTouch;
                if (t.press.wasPressedThisFrame) { screen = t.position.ReadValue(); return true; }
            }
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            { screen = Pointer.current.position.ReadValue(); return true; }
            return false;
        }

        private static bool PointerHeld()
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
            if (Pointer.current != null && Pointer.current.press.isPressed) return true;
            return false;
        }
    }
}
