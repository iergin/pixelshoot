using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Grid;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// A passenger riding the bus (the projectile that replaces the old Bullet).
    /// Sits in a seat playing the idle animation; on launch it plays the falling
    /// animation, flies straight to the target box, and applies the hit.
    ///
    /// <para><b>Seat shifting</b>: the bus retargets stickmen with
    /// <see cref="MoveToSeat"/>; a new call kills the previous shift tween so
    /// rapid-fire never desyncs the queue — the LOGICAL order lives in
    /// BusSeatController's list, visuals just catch up.</para>
    /// </summary>
    public class Stickman : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Animator animator;
        [Tooltip("Renderers tinted with the bus colour (ShooterMaterial).")]
        [SerializeField] private Renderer[] colorRenderers;

        [Header("Animation states")]
        [SerializeField] private string idleState = "Idle";
        [SerializeField] private string fallingTrigger = "Falling";

        [Header("Flight")]
        [Tooltip("World units per second of travel toward the target box. Duration = distance / speed, so far targets take longer than near ones.")]
        [SerializeField] private float flightSpeed = 12f;
        [Tooltip("Floor on the flight duration so an extremely close target still has a brief, visible flight. Keep small to preserve the speed-based feel.")]
        [SerializeField, Min(0f)] private float minFlightDuration = 0.05f;
        [Tooltip("Relative velocity at the START of the flight (slope at t=0). 1 = constant speed. Higher = faster launch.")]
        [SerializeField, Min(0f)] private float startSpeed = 2f;
        [Tooltip("Relative velocity at the END of the flight (slope at t=1). 1 = constant speed. Lower = more slowdown on arrival; raise it so it doesn't crawl at the end.")]
        [SerializeField, Min(0f)] private float endSpeed = 0.7f;
        [Tooltip("Scale multiplier at the midpoint of the flight — the stickman grows to this then shrinks back to its default scale. 1 = no scale change.")]
        [SerializeField, Min(1f)] private float apexScaleMultiplier = 1.4f;

        private Tween seatTween;
        private Tween flightTween;
        private Tween flightScaleTween;

        // Cached easing curve built from startSpeed / endSpeed (rebuilt when they change).
        private AnimationCurve flightCurve;
        private float cachedStart = float.NaN, cachedEnd = float.NaN;

        private AnimationCurve FlightCurve()
        {
            if (flightCurve == null || cachedStart != startSpeed || cachedEnd != endSpeed)
            {
                // Normalised ease (0,0)→(1,1); tangents are the relative start/end speeds.
                flightCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, startSpeed),
                    new Keyframe(1f, 1f, endSpeed, 0f));
                cachedStart = startSpeed;
                cachedEnd = endSpeed;
            }
            return flightCurve;
        }

        public void SetColor(ColorData c)
        {
            if (c == null || c.ShooterMaterial == null || colorRenderers == null) return;
            foreach (var r in colorRenderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats.Length == 0) continue;
                mats[0] = c.ShooterMaterial;
                r.sharedMaterials = mats;
            }
        }

        public void PlayIdle()
        {
            if (animator != null && !string.IsNullOrEmpty(idleState))
                animator.Play(idleState, 0, Random.value); // random offset so seated stickmen don't move in lockstep
        }

        /// <summary>
        /// Tween this stickman's local position to a seat. Kills any previous shift so
        /// overlapping fires can retarget mid-flight without visual desync.
        /// </summary>
        public void MoveToSeat(Vector3 localTarget, float duration)
        {
            seatTween?.Kill();
            if (duration <= 0f || !Application.isPlaying)
            {
                transform.localPosition = localTarget;
                return;
            }
            seatTween = transform.DOLocalMove(localTarget, duration).SetEase(Ease.OutCubic);
        }

        /// <summary>Place instantly on a seat (used at spawn so newcomers don't slide in from origin).</summary>
        public void SnapToSeat(Vector3 localTarget)
        {
            seatTween?.Kill();
            transform.localPosition = localTarget;
        }

        /// <summary>
        /// Detach from the bus and fly straight to the target box (eased DOMove).
        /// Applies the hit via <see cref="GridController.NotifyBoxHit"/> on landing,
        /// then destroys itself.
        /// </summary>
        public void LaunchAt(Box target, GridController grid)
        {
            seatTween?.Kill();
            flightTween?.Kill();
            transform.SetParent(null, true); // survive the bus expiring mid-flight

            if (animator != null && !string.IsNullOrEmpty(fallingTrigger))
                animator.SetTrigger(fallingTrigger);

            Vector3 endPos = grid != null && target != null
                ? grid.GetCellWorldPosition(target.GridX, target.GridZ)
                : transform.position;
            float distance = Vector3.Distance(transform.position, endPos);
            float duration = Mathf.Max(minFlightDuration, distance / Mathf.Max(0.01f, flightSpeed));

            transform.LookAt(endPos);
            flightTween = transform.DOMove(endPos, duration)
                .SetEase(FlightCurve())
                .OnComplete(() =>
                {
                    if (grid != null && target != null && target.IsAlive)
                        grid.NotifyBoxHit(target);
                    Destroy(gameObject);
                });

            // Scale pulse over the flight: grow to the midpoint, shrink BACK TO the
            // default scale (never below it) by the time it lands.
            if (apexScaleMultiplier > 1f)
            {
                flightScaleTween?.Kill();
                Vector3 baseScale = transform.localScale;
                flightScaleTween = DOTween.Sequence()
                    .Append(transform.DOScale(baseScale * apexScaleMultiplier, duration * 0.5f).SetEase(Ease.OutQuad))
                    .Append(transform.DOScale(baseScale, duration * 0.5f).SetEase(Ease.InQuad));
            }
        }

        /// <summary>Quick disappear used when a bomb consumes this stickman from the back of the bus.</summary>
        public void DespawnImmediate()
        {
            seatTween?.Kill();
            flightTween?.Kill();
            flightScaleTween?.Kill();
            transform.DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (this != null) Destroy(gameObject); });
        }

        private void OnDestroy()
        {
            seatTween?.Kill();
            flightTween?.Kill();
            flightScaleTween?.Kill();
        }
    }
}
