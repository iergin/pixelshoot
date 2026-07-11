using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Conveyor;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Central booster logic: a button asks to use a booster; if the player owns one we
    /// consume it, fly a particle from the button to the target (e.g. the conveyor capacity
    /// text) and apply the effect when it lands. If the player owns none, the shared
    /// purchase popup opens.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        [Header("Effect targets")]
        [Tooltip("Conveyor the ConveyorCapacity booster grows.")]
        [SerializeField] private ConveyorController conveyor;

        [Header("Purchase")]
        [SerializeField] private BoosterPurchaseController purchasePopup;

        [Header("Fly-to-target animation")]
        [Tooltip("Particle prefab that flies from the button to the target, then the effect applies. Assign in the inspector.")]
        [SerializeField] private GameObject flyParticlePrefab;
        [Tooltip("World-space target (e.g. the world conveyor-capacity text). Preferred.")]
        [SerializeField] private Transform flyWorldTarget;
        [Tooltip("UI target used only if Fly World Target is empty (converted to world at Fly Depth).")]
        [SerializeField] private RectTransform flyTarget;
        [SerializeField] private Camera flyCamera;
        [Tooltip("Distance from the camera used to place the fly in world space (for UI screen positions).")]
        [SerializeField] private float flyDepth = 10f;
        [SerializeField] private float flyDuration = 0.5f;
        [SerializeField] private Ease flyEase = Ease.InOutSine;
        [Tooltip("Seconds to keep the particle alive after landing so trailing particles fade.")]
        [SerializeField] private float flyCleanupDelay = 1f;

        /// <summary>Use a booster (or open its purchase popup if none owned).</summary>
        public void RequestBooster(BoosterData data, RectTransform fromButton)
        {
            if (data == null) return;

            if (PlayerBoosters.Count(data.Id) > 0)
            {
                if (!PlayerBoosters.TryConsume(data.Id)) return;
                FlyThenApply(data, fromButton);
            }
            else if (purchasePopup != null)
            {
                purchasePopup.Open(data);
            }
        }

        private void FlyThenApply(BoosterData data, RectTransform fromButton)
        {
            var cam = flyCamera != null ? flyCamera : Camera.main;
            if (flyParticlePrefab == null || fromButton == null || cam == null ||
                !ResolveWorldPoints(cam, fromButton, out Vector3 startW, out Vector3 endW))
            {
                ApplyEffect(data); // no prefab / target → apply immediately
                return;
            }

            var go = Instantiate(flyParticlePrefab, startW, Quaternion.identity);
            go.transform.DOMove(endW, flyDuration)
              .SetEase(flyEase)
              .SetUpdate(true)
              .OnComplete(() =>
              {
                  ApplyEffect(data);
                  if (flyTarget != null) flyTarget.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.6f).SetUpdate(true);
                  var ps = go.GetComponentInChildren<ParticleSystem>();
                  if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                  Destroy(go, Mathf.Max(0f, flyCleanupDelay));
              });
        }

        // Resolve the fly start (button) and end (target) in WORLD space, both at the
        // target's depth so the particle travels on a consistent plane.
        private bool ResolveWorldPoints(Camera cam, RectTransform fromButton, out Vector3 startW, out Vector3 endW)
        {
            startW = endW = Vector3.zero;
            float depth;

            if (flyWorldTarget != null)
            {
                endW = flyWorldTarget.position;
                depth = cam.WorldToScreenPoint(endW).z;
                if (depth <= 0.01f) depth = flyDepth;
            }
            else if (flyTarget != null)
            {
                depth = flyDepth;
                Vector3 ts = flyTarget.position; // screen px (overlay canvas)
                endW = cam.ScreenToWorldPoint(new Vector3(ts.x, ts.y, depth));
            }
            else return false;

            Vector3 bs = fromButton.position; // screen px (overlay canvas)
            startW = cam.ScreenToWorldPoint(new Vector3(bs.x, bs.y, depth));
            return true;
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
