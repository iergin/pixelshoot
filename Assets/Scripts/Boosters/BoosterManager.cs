using UnityEngine;
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
        [Tooltip("Full-screen transparent click catcher — enabled while an 'unlock level' hint is open so a tap OUTSIDE closes it. Its own Button/onClick should call CloseUnlockInfo.")]
        [SerializeField] private GameObject clickBlocker;

        private BoosterButton openUnlock;

        /// <summary>Use a booster (or open its purchase popup if none owned).
        /// <paramref name="startOverride"/> is an optional per-button start point.</summary>
        public void RequestBooster(BoosterData data, Transform startOverride = null)
        {
            if (data == null) return;

            if (PlayerBoosters.Count(data.Id) > 0)
            {
                if (!PlayerBoosters.TryConsume(data.Id)) return;
                FlyThenApply(data, startOverride);
            }
            else if (purchasePopup != null)
            {
                purchasePopup.Open(data);
            }
        }

        /// <summary>Use a booster WITHOUT consuming one (the tutorial's free use).</summary>
        public void UseBoosterFree(BoosterData data, Transform startOverride = null)
        {
            if (data != null) FlyThenApply(data, startOverride);
        }

        /// <summary>Called by the purchase popup after a booster is bought (coins or ad):
        /// open the use panel so the player can use it right away. If no panel is assigned
        /// the booster simply stays in the inventory.</summary>
        public void OnPurchased(BoosterData data)
        {
            if (data != null && usePanel != null) usePanel.Open(data);
        }

        /// <summary>The use panel's button: consume one booster and trigger the fly + effect.</summary>
        public void UseFromPanel(BoosterData data)
        {
            if (data == null) return;
            if (!PlayerBoosters.TryConsume(data.Id)) return;
            FlyThenApply(data, null);
        }

        // ── Locked-booster unlock hint ───────────────────────────────────────
        /// <summary>Tapped a locked booster: toggle its hint. Same button → close;
        /// another → switch; any outside tap (via the click blocker) also closes.</summary>
        public void ToggleUnlockInfo(BoosterButton btn)
        {
            if (openUnlock == btn) { CloseUnlockInfo(); return; }
            CloseUnlockInfo();            // close any other open hint first
            openUnlock = btn;
            btn.SetUnlockInfoVisible(true);
            if (clickBlocker != null) clickBlocker.SetActive(true);
        }

        /// <summary>Close the open unlock hint (wire the click blocker's onClick to this).</summary>
        public void CloseUnlockInfo()
        {
            if (openUnlock != null) openUnlock.SetUnlockInfoVisible(false);
            openUnlock = null;
            if (clickBlocker != null) clickBlocker.SetActive(false);
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
                default:
                    Debug.LogWarning($"[BoosterManager] No effect implemented for {data.Type} ('{data.Id}').");
                    break;
            }
        }
    }
}
