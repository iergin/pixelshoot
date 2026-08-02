using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Grid;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// A bus passenger (the projectile that replaces the old Bullet). It is spawned from
    /// the pool at the bus's single spawn point, then immediately flies straight to its
    /// target box and applies the hit. On arrival it returns to the pool (no Destroy).
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
        [Tooltip("Spawn/run scale tuning for bus stickmen (spawn scale, run = box scale × mult, grow time).")]
        [SerializeField] private StickmanScaleConfig scaleConfig;

        [Header("Footsteps")]
        [Tooltip("Seconds between this stickman's own footstep attempts while running. The AudioManager throttles them GLOBALLY, so many runners still collapse into one clean patter. 0 = no footsteps.")]
        [SerializeField, Min(0f)] private float footstepStride = 0.16f;

        /// <summary>Global run-speed multiplier — 1 normally, raised (e.g. x2) during the endgame so
        /// stickmen sprint to their boxes as fast as the sped-up conveyor. Set by GameController.</summary>
        public static float SpeedMultiplier = 1f;

        private Tween flightTween;
        private Tween flightScaleTween;
        private float footstepTimer;

        /// <summary>The prefab this instance was pooled from (set by <see cref="StickmanPool"/>).</summary>
        public Stickman SourcePrefab { get; private set; }
        public void SetSourcePrefab(Stickman prefab) => SourcePrefab = prefab;

        // Emit a footstep every footstepStride seconds of the run. The AudioManager throttles them
        // globally, so this stickman's steps blend with everyone else's into a single clean patter.
        private void EmitFootstepTick()
        {
            if (footstepStride <= 0f) return;
            footstepTimer -= Time.deltaTime;
            if (footstepTimer > 0f) return;
            footstepTimer = footstepStride;
            PixelShoot.Audio.AudioManager.Instance?.PlayFootstep();
        }

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
                animator.Play(idleState, 0, Random.value); // random offset so spawns don't move in lockstep
        }

        /// <summary>
        /// Detach from the bus and fly straight to the target box (eased DOMove).
        /// Applies the hit via <see cref="GridController.NotifyBoxHit"/> on landing,
        /// then returns to the pool.
        /// </summary>
        public void LaunchAt(Box target, GridController grid)
        {
            flightTween?.Kill();
            transform.SetParent(null, true); // survive the bus expiring mid-flight

            if (animator != null && !string.IsNullOrEmpty(fallingTrigger))
                animator.SetTrigger(fallingTrigger);

            Vector3 endPos = grid != null && target != null
                ? grid.GetCellWorldPosition(target.GridX, target.GridZ)
                : transform.position;

            // Spawn ALIGNED with the box on the bus's travel axis (the one parallel to the grid
            // edge), so the run is a single straight line along the perpendicular axis with the
            // travel axis fixed: bus moving in X → stickman keeps the box's X and runs along Z,
            // and vice-versa. No diagonal, no mid-run turn.
            if (grid != null && target != null)
            {
                Vector3 startLocal = grid.WorldToGridLocal(transform.position);
                Vector3 endLocal = grid.GetCellLocalPosition(target.GridX, target.GridZ);
                float offset = (grid.Size - 1) * 0.5f * grid.CellSize;
                bool fireAlongZ = (Mathf.Abs(startLocal.z) - offset) >= (Mathf.Abs(startLocal.x) - offset);
                Vector3 alignedLocal = fireAlongZ
                    ? new Vector3(endLocal.x, startLocal.y, startLocal.z)  // travel axis = X → fix X, run along Z
                    : new Vector3(startLocal.x, startLocal.y, endLocal.z); // travel axis = Z → fix Z, run along X
                transform.position = grid.GridLocalToWorld(alignedLocal);
            }

            float distance = Vector3.Distance(transform.position, endPos);
            float duration = Mathf.Max(minFlightDuration, distance / Mathf.Max(0.01f, flightSpeed * SpeedMultiplier));

            // Face the run direction, yaw-only (stay upright), then run straight in.
            Vector3 face = endPos - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);

            // Run animation plays at the same multiplier so it doesn't look like sliding.
            if (animator != null) animator.speed = Mathf.Max(0.01f, SpeedMultiplier);
            footstepTimer = 0f;
            flightTween = transform.DOMove(endPos, duration)
                .SetEase(FlightCurve())
                .OnUpdate(EmitFootstepTick)
                .OnComplete(() =>
                {
                    if (grid != null && target != null && target.IsAlive)
                        grid.NotifyBoxHit(target);
                    StickmanPool.Release(this);
                });

            // Spawn at the config's spawn scale, then smoothly grow to the RUN scale
            // (= target box's world scale × multiplier).
            if (scaleConfig != null)
            {
                flightScaleTween?.Kill();
                transform.localScale = Vector3.one * scaleConfig.spawnScale;
                float boxScale = target != null ? target.transform.lossyScale.x : 1f;
                float runScale = boxScale * scaleConfig.runScaleBoxMultiplier;
                flightScaleTween = transform.DOScale(Vector3.one * runScale, scaleConfig.growDuration)
                    .SetEase(scaleConfig.growEase);
            }
        }

        /// <summary>
        /// FillColor booster: run in from the current (off-screen) position to the target box's
        /// cell at a configurable ground speed, playing <paramref name="runState"/>. Applies the
        /// hit on arrival, invokes <paramref name="onDone"/>, then returns to the pool.
        /// </summary>
        public void RunInAndHit(Box target, GridController grid, float speed, float minDuration,
            string runTrigger, Ease ease, bool faceMovement, System.Action onDone)
        {
            flightTween?.Kill();
            transform.SetParent(null, true);

            if (animator != null && !string.IsNullOrEmpty(runTrigger))
                animator.SetTrigger(runTrigger);

            Vector3 endPos = grid != null && target != null
                ? grid.GetCellWorldPosition(target.GridX, target.GridZ)
                : transform.position;

            if (faceMovement)
            {
                Vector3 dir = endPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            float distance = Vector3.Distance(transform.position, endPos);
            float duration = Mathf.Max(minDuration, distance / Mathf.Max(0.01f, speed * SpeedMultiplier));
            if (animator != null) animator.speed = Mathf.Max(0.01f, SpeedMultiplier);
            footstepTimer = 0f;
            flightTween = transform.DOMove(endPos, duration)
                .SetEase(ease)
                .OnUpdate(EmitFootstepTick)
                .OnComplete(() =>
                {
                    if (grid != null && target != null && target.IsAlive) grid.NotifyBoxHit(target);
                    onDone?.Invoke();
                    StickmanPool.Release(this);
                });
        }

        /// <summary>Quick disappear (e.g. bomb payback); returns to the pool afterwards.</summary>
        public void DespawnImmediate()
        {
            flightTween?.Kill();
            flightScaleTween?.Kill();
            transform.DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => StickmanPool.Release(this));
        }

        /// <summary>Called by the pool before an instance is parked — cancel any running tweens.</summary>
        public void ResetForPool()
        {
            flightTween?.Kill();
            flightScaleTween?.Kill();
        }

        private void OnDestroy()
        {
            flightTween?.Kill();
            flightScaleTween?.Kill();
        }
    }
}
