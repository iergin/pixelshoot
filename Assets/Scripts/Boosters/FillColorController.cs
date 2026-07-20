using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Grid;
using PixelShoot.Conveyor;
using PixelShoot.Shooters;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// The interactive "FillColor" booster. When begun: the conveyor freezes and gameplay
    /// input is suspended (columns can't be tapped). The player taps ANY box; every still-alive
    /// box of that gameplay color is then "hit" as if shot — stickmen run in from off-screen
    /// (speed/animation from <see cref="FillColorConfig"/>) and strike each box. The matching
    /// color's buses lose that many shots (depleted buses are removed). Once every box has been
    /// hit, the conveyor resumes.
    /// </summary>
    public class FillColorController : MonoBehaviour
    {
        [Header("Gameplay links")]
        [SerializeField] private GridController grid;
        [SerializeField] private ConveyorController conveyor;
        [SerializeField] private Camera cam;

        [Header("Config")]
        [SerializeField] private FillColorConfig config;

        [Header("Camera")]
        [Tooltip("Cinemachine vcam GameObject enabled while the mode is active (give it higher Priority so the brain blends to it), like the Claw camera.")]
        [SerializeField] private GameObject fillCamera;

        [Header("UI")]
        [Tooltip("Description panel shown after the button is pressed (like the Claw panel).")]
        [SerializeField] private GameObject descriptionPanel;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("Exit/cancel button — ends without filling (booster NOT consumed).")]
        [SerializeField] private Button cancelButton;

        private bool active;
        private bool running;         // a fill is in progress (ignore further taps)
        private bool consumeOnGrab;
        private bool waitingForRelease;
        private BoosterData current;
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

        /// <summary>Enter box-pick mode. <paramref name="consume"/> spends one booster on a
        /// successful fill (false for the free tutorial use).</summary>
        public bool Begin(BoosterData data, bool consume)
        {
            if (active || grid == null || conveyor == null) return false;
            Wire();

            active = true;
            running = false;
            consumeOnGrab = consume;
            current = data;
            waitingForRelease = true;

            BoosterProcess.Set(true); // lock other booster buttons + hide the bar
            conveyor.IsPaused = true;
            ClickInputRouter.PushSuspend();
            if (fillCamera != null) fillCamera.SetActive(true);
            if (descriptionText != null && data != null) descriptionText.text = data.Description;
            if (descriptionPanel != null) descriptionPanel.SetActive(true);
            return true;
        }

        public void Cancel()
        {
            if (!active || running) return; // can't cancel once the fill has started
            EndMode();
        }

        private void EndMode()
        {
            if (descriptionPanel != null) descriptionPanel.SetActive(false);
            if (fillCamera != null) fillCamera.SetActive(false);
            ClickInputRouter.PopSuspend();
            if (conveyor != null) conveyor.IsPaused = false;
            active = false;
            running = false;
            current = null;
            BoosterProcess.Set(false); // restore other buttons + bring the bar back
        }

        private void Update()
        {
            if (!active || running) return;

            if (waitingForRelease)
            {
                if (!PointerHeld()) waitingForRelease = false;
                return;
            }

            if (TryReadPress(out Vector2 screen)) HandlePress(screen);
        }

        private void HandlePress(Vector2 screen)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var c = cam != null ? cam : Camera.main;
            if (c == null) return;

            var box = grid.PickBox(c.ScreenPointToRay(screen));
            if (box != null && box.IsAlive && box.Color != null) StartFill(box);
            else PixelShoot.Audio.AudioManager.Instance?.PlayBlocked();
        }

        private void StartFill(Box picked)
        {
            var color = picked.Color.GameplayColor;
            var targets = grid.CollectAliveBoxes(color);
            if (targets.Count == 0) { PixelShoot.Audio.AudioManager.Instance?.PlayBlocked(); return; }

            running = true;
            if (descriptionPanel != null) descriptionPanel.SetActive(false);
            if (consumeOnGrab && current != null) PlayerBoosters.TryConsume(current.Id);

            // Reserve the boxes so nothing else targets them, then pay back the buses of this
            // color by exactly the number of boxes we're about to fill.
            foreach (var b in targets) b.ReserveHit();
            ShooterColumn.ConsumeShotsForGameplayColor(color, targets.Count);

            StartCoroutine(RunFill(targets));
        }

        private IEnumerator RunFill(List<Box> targets)
        {
            int pending = targets.Count;
            var wait = config != null && config.spawnStagger > 0f ? new WaitForSeconds(config.spawnStagger) : null;

            foreach (var box in targets)
            {
                SpawnRunner(box, () => pending--);
                if (wait != null) yield return wait;
            }
            while (pending > 0) yield return null;

            EndMode(); // all boxes hit → resume the conveyor
        }

        /// <summary>
        /// Non-interactive fill used by the STREAK paint gift: run stickmen in from off-screen to
        /// the given boxes (same runners as the booster), no camera / UI / conveyor-pause. Reserves
        /// each box; if <paramref name="consumeShots"/>, the matching buses lose a shot per box so
        /// the bullet budget stays balanced. Safe to call without <see cref="Begin"/>.
        /// </summary>
        public void FillBoxes(List<Box> targets, bool consumeShots = true)
        {
            if (grid == null || targets == null || targets.Count == 0) return;

            foreach (var b in targets) if (b != null && b.IsAlive) b.ReserveHit();

            if (consumeShots)
            {
                var byColor = new Dictionary<ColorData, int>();
                foreach (var b in targets)
                {
                    var col = b != null && b.Color != null ? b.Color.GameplayColor : null;
                    if (col == null) continue;
                    byColor.TryGetValue(col, out int c); byColor[col] = c + 1;
                }
                foreach (var kv in byColor) ShooterColumn.ConsumeShotsForGameplayColor(kv.Key, kv.Value);
            }

            StartCoroutine(RunFillBoxes(new List<Box>(targets)));
        }

        // Like RunFill but WITHOUT EndMode() — the gift never entered interactive mode.
        private IEnumerator RunFillBoxes(List<Box> targets)
        {
            var wait = config != null && config.spawnStagger > 0f ? new WaitForSeconds(config.spawnStagger) : null;
            foreach (var box in targets)
            {
                if (box != null && box.IsAlive) SpawnRunner(box, null);
                if (wait != null) yield return wait;
            }
        }

        private void SpawnRunner(Box box, System.Action onDone)
        {
            // No prefab configured → apply the hit instantly (still works, just no runner).
            if (config == null || config.stickmanPrefab == null)
            {
                if (box != null && box.IsAlive) grid.NotifyBoxHit(box);
                onDone?.Invoke();
                return;
            }

            var inst = StickmanPool.Get(config.stickmanPrefab, null);
            if (inst == null) { if (box.IsAlive) grid.NotifyBoxHit(box); onDone?.Invoke(); return; }

            inst.transform.position = RandomOffscreenPoint();
            inst.transform.localScale = Vector3.one * config.stickmanScale;
            inst.SetColor(box.Color);
            inst.RunInAndHit(box, grid, config.runSpeed, config.minRunDuration,
                config.runAnimState, config.moveEase, config.faceMovement, onDone);
        }

        // A point on the grid plane just outside the screen (random edge), so the stickman runs in.
        private Vector3 RandomOffscreenPoint()
        {
            var c = cam != null ? cam : Camera.main;
            float w = Screen.width, h = Screen.height;
            float m = config != null ? config.edgeMarginPixels : 120f;

            Vector2 sp;
            switch (Random.Range(0, 4))
            {
                case 0: sp = new Vector2(Random.Range(-m, w + m), h + m); break; // top
                case 1: sp = new Vector2(Random.Range(-m, w + m), -m); break;    // bottom
                case 2: sp = new Vector2(-m, Random.Range(-m, h + m)); break;    // left
                default: sp = new Vector2(w + m, Random.Range(-m, h + m)); break; // right
            }
            if (c != null && grid.RaycastGridPlane(c.ScreenPointToRay(sp), out Vector3 world)) return world;
            return grid.GridRoot != null ? grid.GridRoot.position : transform.position;
        }

        // ── Pointer helpers (new Input System) ───────────────────────────────
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
