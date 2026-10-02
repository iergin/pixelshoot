using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Conveyor;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Central booster logic: a button asks to use a booster; if the player owns one we
    /// consume it, fly a particle from a START world point to an END world point, then
    /// apply the effect when it lands. If the player owns none, the purchase popup opens.
    ///
    /// <para>The fly uses two plain world-space Transforms (drop empty GameObjects where
    /// you want) — no UI/camera math. A button may override the start with its own point.</para>
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        [Header("Effect targets")]
        [Tooltip("Conveyor the ConveyorCapacity booster grows.")]
        [SerializeField] private ConveyorController conveyor;

        [Header("Purchase")]
        [SerializeField] private BoosterPurchaseController purchasePopup;
        [Tooltip("Shown after a purchase (coins/ad) so the player can use the booster right away.")]
        [SerializeField] private BoosterUsePanel usePanel;

        [Header("Interactive")]
        [Tooltip("Handles the Claw booster (pick a column bus → conveyor).")]
        [SerializeField] private ClawController clawController;
        [Tooltip("Handles the FillColor booster (pick a box → hit every box of that color).")]
        [SerializeField] private FillColorController fillColorController;
        [Tooltip("Handles the Shuffle booster (rearranges the front column rows).")]
        [SerializeField] private ShuffleController shuffleController;

        [Header("Fly (world-space)")]
        [Tooltip("Particle prefab that flies from Start to End, then the effect applies.")]
        [SerializeField] private GameObject flyParticlePrefab;
        [Tooltip("Default START world point (an empty GameObject near the booster bar). A button can override this.")]
        [SerializeField] private Transform flyStart;
        [Tooltip("END world point — an empty GameObject at the conveyor capacity text.")]
        [SerializeField] private Transform flyEnd;
        [SerializeField] private float flyDuration = 0.5f;
        [SerializeField] private Ease flyEase = Ease.InOutSine;
        [Tooltip("Seconds to keep the particle alive after landing so trailing particles fade.")]
        [SerializeField] private float flyCleanupDelay = 1f;

        [Header("Lock hint")]
        [Tooltip("OPTIONAL. A tap outside a booster button already closes the hint (handled in Update). Assign only if you also want a visible dim/catcher object toggled with the hint.")]
        [SerializeField] private GameObject clickBlocker;

        private BoosterButton openUnlock;

        /// <summary>Use a booster (or open its purchase popup if none owned).
        /// <paramref name="startOverride"/> is an optional per-button start point.</summary>
        public void RequestBooster(BoosterData data, Transform startOverride = null)
        {
            if (data == null) return;
            CloseUnlockInfo(); // tapping any usable booster closes an open unlock hint

            if (PlayerBoosters.Count(data.Id) > 0)
            {
                // Interactive boosters (Claw) open a selection mode and consume on a successful
                // grab — NOT up front, so a cancel keeps the booster.
                if (data.IsInteractive)
                {
                    BeginInteractive(data, consume: true);
                    return;
                }
                if (!PlayerBoosters.TryConsume(data.Id)) return;
                TriggerEffect(data, startOverride);
            }
            else if (purchasePopup != null)
            {
                purchasePopup.Open(data);
            }
        }

        /// <summary>Use a booster WITHOUT consuming one (the tutorial's free use).</summary>
        public void UseBoosterFree(BoosterData data, Transform startOverride = null)
        {
            if (data == null) return;
            if (data.IsInteractive)
            {
                BeginInteractive(data, consume: false);
                return;
            }
            TriggerEffect(data, startOverride);
        }

        /// <summary>Called by the purchase popup after a booster is bought (coins or ad):
        /// open the use panel so the player can use it right away. If no panel is assigned
        /// the booster simply stays in the inventory.</summary>
        public void OnPurchased(BoosterData data)
        {
            if (data != null && usePanel != null) usePanel.Open(data);
        }

        /// <summary>The use panel's button: use one booster (instant effect, or open the
        /// interactive mode which consumes on a successful action).</summary>
        public void UseFromPanel(BoosterData data)
        {
            if (data == null) return;
            if (data.IsInteractive)
            {
                BeginInteractive(data, consume: true);
                return;
            }
            if (!PlayerBoosters.TryConsume(data.Id)) return;
            TriggerEffect(data, null);
        }

        // Route an interactive booster to its controller. Consume happens on a successful
        // action inside the controller (so cancelling keeps the booster).
        private void BeginInteractive(BoosterData data, bool consume)
        {
            switch (data.Type)
            {
                case BoosterType.Claw:
                    if (clawController != null) clawController.Begin(data, consume);
                    else Debug.LogWarning("[BoosterManager] Claw booster used but no ClawController assigned.");
                    break;
                case BoosterType.FillColor:
                    if (fillColorController != null) fillColorController.Begin(data, consume);
                    else Debug.LogWarning("[BoosterManager] FillColor booster used but no FillColorController assigned.");
                    break;
                default:
                    Debug.LogWarning($"[BoosterManager] No interactive controller for {data.Type}.");
                    break;
            }
        }

        // ── Locked-booster unlock hint ───────────────────────────────────────
        /// <summary>Tapped a locked booster: toggle its hint. Same button → close;
        /// another → switch; any tap outside a booster button also closes (see Update).</summary>
        public void ToggleUnlockInfo(BoosterButton btn)
        {
            if (openUnlock == btn) { CloseUnlockInfo(); return; }
            CloseUnlockInfo();            // close any other open hint first
            openUnlock = btn;
            btn.SetUnlockInfoVisible(true);
            if (clickBlocker != null) clickBlocker.SetActive(true);
        }

        /// <summary>Close the open unlock hint.</summary>
        public void CloseUnlockInfo()
        {
            if (openUnlock != null) openUnlock.SetUnlockInfoVisible(false);
            openUnlock = null;
            if (clickBlocker != null) clickBlocker.SetActive(false);
        }

        // While a hint is open, any press that ISN'T on a booster button closes it. Presses
        // ON a booster button are left to that button's onClick (which toggles/switches),
        // so there's no race. This makes the optional clickBlocker unnecessary.
        private static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        private void Update()
        {
            if (openUnlock == null) return;
            if (!TryReadPress(out Vector2 screen)) return;
            if (IsPointerOverBoosterButton(screen)) return; // its onClick handles it
            CloseUnlockInfo();
        }

        private static bool IsPointerOverBoosterButton(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            var ped = new PointerEventData(EventSystem.current) { position = screen };
            raycastResults.Clear();
            EventSystem.current.RaycastAll(ped, raycastResults);
            foreach (var r in raycastResults)
                if (r.gameObject != null && r.gameObject.GetComponentInParent<BoosterButton>() != null)
                    return true;
            return false;
        }

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

        // Only the conveyor-capacity booster flies a particle to the conveyor text; other
        // effects (shuffle, …) apply where they happen, so skip the fly for them.
        private void TriggerEffect(BoosterData data, Transform startOverride)
        {
            if (data.Type == BoosterType.ConveyorCapacity) FlyThenApply(data, startOverride);
            else ApplyEffect(data);
        }

        private void FlyThenApply(BoosterData data, Transform startOverride)
        {
            Transform start = startOverride != null ? startOverride : flyStart;
            if (flyParticlePrefab == null || start == null || flyEnd == null)
            {
                ApplyEffect(data); // nothing to fly → apply immediately
                return;
            }

            var go = Instantiate(flyParticlePrefab, start.position, start.rotation);
            go.transform.DOMove(flyEnd.position, flyDuration)
              .SetEase(flyEase)
              .SetUpdate(true)
              .OnComplete(() =>
              {
                  ApplyEffect(data);
                  flyEnd.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.6f).SetUpdate(true);
                  var ps = go.GetComponentInChildren<ParticleSystem>();
                  if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                  Destroy(go, Mathf.Max(0f, flyCleanupDelay));
              });
        }

        private void ApplyEffect(BoosterData data)
        {
            switch (data.Type)
            {
                case BoosterType.ConveyorCapacity:
                    if (conveyor != null) conveyor.AddCapacity(data.Amount);
                    break;
                case BoosterType.Shuffle:
                    if (shuffleController != null) shuffleController.Shuffle();
                    else Debug.LogWarning("[BoosterManager] Shuffle booster used but no ShuffleController assigned.");
                    break;
                default:
                    Debug.LogWarning($"[BoosterManager] No effect implemented for {data.Type} ('{data.Id}').");
                    break;
            }
        }
    }
}
