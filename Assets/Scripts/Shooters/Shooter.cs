using System;
using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Grid;
using PixelShoot.Conveyor;

namespace PixelShoot.Shooters
{
    public enum ShooterState
    {
        InColumn,
        Boarding,      // animating from column to conveyor entry (or to reserve)
        InReserve,
        OnConveyor,
        Expired
    }

    /// <summary>
    /// The bus. Rides the conveyor; its passengers (Stickman) are the projectiles.
    /// Firing launches the front-seat stickman in an arc toward the target box.
    /// Seat shifting / refilling is delegated to <see cref="BusSeatController"/>.
    /// </summary>
    public class Shooter : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [Tooltip("Seat manager that owns the visible stickmen. If null, hits are applied instantly with no projectile.")]
        [SerializeField] private BusSeatController seats;
        [SerializeField] private float jumpPower = 1.5f;
        [Header("Conveyor facing")]
        [Tooltip("How fast the bus turns to face its travel direction, in degrees per second. Higher = snappier cornering.")]
        [SerializeField] private float turnSpeed = 540f;
        [Tooltip("How far ahead along the path (world units) to sample when computing the travel direction.")]
        [SerializeField] private float facingLookAhead = 0.15f;
        [Tooltip("Uniform scale multiplier applied while riding the conveyor. The bus tweens to this during the boarding jump and back to its spawn scale when it hops off into reserve.")]
        [SerializeField] private float conveyorScale = 1.2f;

        [Header("Firing")]
        [Tooltip("Minimum seconds between consecutive stickman launches. Targets are reserved instantly; only the visual departure is staggered.")]
        [SerializeField, Min(0f)] private float launchInterval = 0.12f;

        [Header("Engine idle wobble")]
        [Tooltip("Child visual that pulses like a running engine. MUST be a child, not the bus root — the root's scale is owned by the conveyor/reserve transitions. If null, the mesh renderer's transform is used.")]
        [SerializeField] private Transform wobbleTarget;
        [Tooltip("Squash-and-stretch amplitude. 0.04 = Y stretches +4% while XZ squash -2%, looped. 0 disables the wobble.")]
        [SerializeField, Min(0f)] private float wobbleAmount = 0.04f;
        [Tooltip("Seconds for one half-cycle of the wobble (up OR down).")]
        [SerializeField, Min(0.05f)] private float wobbleDuration = 0.18f;

        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;
        private Tween facingTween;
        private Tween scaleTween;
        private readonly System.Collections.Generic.Queue<Box> launchQueue = new System.Collections.Generic.Queue<Box>();
        private float nextLaunchTime;
        private Tween wobbleTween;

        private ColorData color;
        private int shotsRemaining;
        private ShooterState state = ShooterState.InColumn;

        private GridController grid;
        private ConveyorController conveyor;

        private float pathProgress;
        private float pathSpeed;
        private Tween boardingTween;

        // Engagement tracking: each column/row gets at most one shot per pass.
        // Reset when the shooter changes side or moves off the grid (parallel = -1).
        private GridSide lastSide;
        private int lastEngagedParallelIndex = int.MinValue;
        // Guard so OnPathEnded fires exactly once per lap (avoids re-firing every frame
        // when the conveyor is paused at fail with the shooter still at pathProgress == max).
        private bool pathEndFired;

        public event Action<Shooter> OnExpired;
        public event Action<Shooter> OnPathEnded;

        public ColorData Color => color;
        public int ShotsRemaining => shotsRemaining;
        public ShooterState State => state;
        public float PathProgress => pathProgress;

        public void Initialize(ColorData c, int shotCount)
        {
            color = c;
            shotsRemaining = shotCount;
            state = ShooterState.InColumn;

            if (meshRenderer != null && c != null && c.ShooterMaterial != null)
            {
                // Use sharedMaterials (not materials) so we don't clone-and-leak the
                // material instance every time Initialize runs — especially important
                // when the level editor rebuilds the scene preview in edit mode.
                Material[] mats = meshRenderer.sharedMaterials;
                mats[0] = c.ShooterMaterial;
                meshRenderer.sharedMaterials = mats;
            }

            // Populate the bus seats with stickmen (visible 6 + hidden reserve).
            if (seats != null) seats.Initialize(shotCount, c);

            StartEngineWobble();
        }

        /// <summary>
        /// Looping squash-and-stretch on a child visual so the bus looks like its
        /// engine is running. Applied to a CHILD transform — the root's scale belongs
        /// to the conveyor / reserve / expire transitions and must not be fought over.
        /// </summary>
        private void StartEngineWobble()
        {
            if (!Application.isPlaying) return;           // edit-mode previews stay still
            if (wobbleTween != null && wobbleTween.IsActive()) return;
            if (wobbleAmount <= 0f) return;

            var t = wobbleTarget != null ? wobbleTarget
                  : (meshRenderer != null ? meshRenderer.transform : null);
            if (t == null || t == transform) return;      // never wobble the root

            Vector3 baseS = t.localScale;
            Vector3 stretched = new Vector3(
                baseS.x * (1f - wobbleAmount * 0.5f),
                baseS.y * (1f + wobbleAmount),
                baseS.z * (1f - wobbleAmount * 0.5f));

            wobbleTween = t.DOScale(stretched, wobbleDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        public void SetGridAndConveyor(GridController g, ConveyorController cv)
        {
            grid = g;
            conveyor = cv;
        }

        /// <summary>
        /// Animate jump from current position to a target world position, then call onDone.
        /// <paramref name="scaleMultiplier"/> scales the bus relative to its spawn scale at the
        /// destination (reserve / play-on slots pass their own configured value); ignored when
        /// jumping ONTO the conveyor, which always uses <see cref="conveyorScale"/>.
        /// <paramref name="landingRotation"/> is the world rotation the bus settles into at a
        /// non-conveyor destination (defaults to identity); conveyor jumps always face the path.
        /// </summary>
        public void JumpTo(Vector3 worldTarget, float duration, Action onDone, ShooterState endState,
            float scaleMultiplier = 1f, Quaternion? landingRotation = null)
        {
            if (!baseScaleCaptured)
            {
                baseScale = transform.localScale;
                baseScaleCaptured = true;
            }

            state = ShooterState.Boarding;
            transform.SetParent(null, true);
            boardingTween?.Kill();
            facingTween?.Kill();
            scaleTween?.Kill();

            float dur = Mathf.Max(0.05f, duration);

            // DOJump itself is a sequence of tweens internally — wrapping it in another
            // Sequence just to attach OnComplete doubled the tween count for every jump.
            boardingTween = transform.DOJump(worldTarget, jumpPower, 1, dur)
                .OnComplete(() =>
                {
                    state = endState;
                    onDone?.Invoke();
                });

            if (endState == ShooterState.OnConveyor)
            {
                // Rotate IN THE AIR toward the path direction at the landing point so the
                // bus touches down already facing its travel direction — no snap on entry.
                // Boarding always lands at progress 0 (see ConveyorController.TryReserveSlot).
                if (TryGetPathRotation(0f, out Quaternion landRot))
                    facingTween = transform.DORotateQuaternion(landRot, dur).SetEase(Ease.InOutSine);
                // Grow (or shrink) to the conveyor scale during the same flight.
                scaleTween = transform.DOScale(baseScale * conveyorScale, dur).SetEase(Ease.InOutSine);
            }
            else
            {
                // Leaving the conveyor (reserve / play-on): scale to the destination's
                // configured multiplier and settle into its configured rotation.
                facingTween = transform.DORotateQuaternion(landingRotation ?? Quaternion.identity, dur).SetEase(Ease.InOutSine);
                scaleTween = transform.DOScale(baseScale * Mathf.Max(0.01f, scaleMultiplier), dur).SetEase(Ease.InOutSine);
            }
        }

        public void StartFollowingPath(float speed)
        {
            state = ShooterState.OnConveyor;
            pathProgress = 0f;
            pathSpeed = speed;
            // Reset engagement so the shooter can fire again at every column on this fresh lap.
            lastEngagedParallelIndex = int.MinValue;
            lastSide = default;
            pathEndFired = false;
            SnapFacingToPath();
        }

        /// <summary>Instantly orient the bus along the path direction at its current progress —
        /// safety net right after boarding (the in-air rotation tween should already have
        /// us facing this way, so any correction here is invisible).</summary>
        private void SnapFacingToPath()
        {
            if (TryGetPathRotation(pathProgress, out Quaternion rot))
                transform.rotation = rot;
        }

        /// <summary>Yaw-only rotation facing the conveyor's travel direction at <paramref name="progress"/>.</summary>
        private bool TryGetPathRotation(float progress, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (conveyor == null) return false;
            conveyor.EvaluatePath(progress, out Vector3 here, out _, out _);
            conveyor.EvaluatePath(progress + facingLookAhead, out Vector3 ahead, out _, out _);
            Vector3 dir = ahead - here;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return false;
            rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            return true;
        }

        public void SetPathProgress(float progress) => pathProgress = progress;
        public void SetPathSpeed(float speed) => pathSpeed = speed;

        private void Update()
        {
            // Launch pacing runs regardless of conveyor state so queued stickmen still
            // depart while the bus is at path end / briefly paused.
            DrainLaunchQueue();

            if (state != ShooterState.OnConveyor || conveyor == null) return;
            // Conveyor paused (e.g., level failed) → freeze in place.
            if (conveyor.IsPaused) return;

            pathProgress += pathSpeed * Time.deltaTime;
            float maxProgress = conveyor.MaxPathProgress;
            if (pathProgress >= maxProgress)
            {
                pathProgress = maxProgress;
                if (pathEndFired) return; // already notified for this lap
                pathEndFired = true;
                OnPathEnded?.Invoke(this);
                // Subscriber must transition state OR keep us on the conveyor (e.g. fail).
                // If nobody handled it AND we weren't deliberately kept, default to Expire.
                if (state == ShooterState.OnConveyor && !conveyor.IsPaused) Expire();
                return;
            }

            conveyor.EvaluatePath(pathProgress, out Vector3 worldPos, out bool canShoot, out GridSide side);
            transform.position = worldPos;

            // Face the travel direction: sample slightly ahead on the path and turn
            // toward it at a capped angular speed so corners are smooth, not snappy.
            conveyor.EvaluatePath(pathProgress + facingLookAhead, out Vector3 aheadPos, out _, out _);
            Vector3 travelDir = aheadPos - worldPos;
            travelDir.y = 0f; // keep the bus level — only yaw toward the path
            if (travelDir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(travelDir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            // Reset engagement when entering a new side of the grid.
            if (side != lastSide)
            {
                lastSide = side;
                lastEngagedParallelIndex = int.MinValue;
            }

            if (!canShoot || shotsRemaining <= 0 || grid == null) return;

            int parallel = grid.GetParallelIndex(side, worldPos);
            if (parallel < 0)
            {
                // Off-grid (e.g., past a corner). Allow re-engagement when we re-enter.
                lastEngagedParallelIndex = -1;
                return;
            }
            if (parallel == lastEngagedParallelIndex) return; // already handled this column on this pass

            lastEngagedParallelIndex = parallel;

            var target = grid.FindTarget(side, worldPos, color);
            if (target == null) return; // wrong color outer or no outer at all

            FireAt(target);
        }

        private void FireAt(Box target)
        {
            // Reserve + count the shot IMMEDIATELY — game logic never waits for the
            // visual launch, so a delayed stickman can't cause double-targeting.
            target.ReserveHit();
            shotsRemaining--;

            if (seats != null)
            {
                launchQueue.Enqueue(target);
                DrainLaunchQueue(); // launch right away if the interval has elapsed
            }
            else
            {
                // No seat system → apply the hit instantly.
                grid.NotifyBoxHit(target);
                if (shotsRemaining <= 0) Expire();
            }
        }

        /// <summary>
        /// Pops at most one queued launch per call, spacing consecutive launches by
        /// <see cref="launchInterval"/>. The stickman is popped from the seats at
        /// ACTUAL launch time so the seat queue order always matches launch order.
        /// </summary>
        private void DrainLaunchQueue()
        {
            if (launchQueue.Count == 0) return;
            if (Time.time < nextLaunchTime) return;

            LaunchOne(launchQueue.Dequeue());
            nextLaunchTime = Time.time + launchInterval;

            // The last queued passenger just left and there's nothing left to shoot.
            if (launchQueue.Count == 0 && shotsRemaining <= 0) Expire();
        }

        private void LaunchOne(Box target)
        {
            var stickman = seats != null ? seats.PopFront() : null;
            if (stickman != null) stickman.LaunchAt(target, grid);
            else if (grid != null) grid.NotifyBoxHit(target); // bus visually empty — instant hit fallback
        }

        public void Expire()
        {
            if (state == ShooterState.Expired) return;
            // Flush any launches still waiting on the interval — their targets are
            // already reserved and counted, the hits MUST happen.
            while (launchQueue.Count > 0) LaunchOne(launchQueue.Dequeue());
            state = ShooterState.Expired;
            OnExpired?.Invoke(this);

            // Kill the boarding-flight scale tween so it can't fight the shrink-out.
            scaleTween?.Kill();
            transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (this != null) Destroy(gameObject); });
        }

        /// <summary>
        /// Bomb-payback hook — subtract <paramref name="amount"/> shots without firing
        /// bullets. Returns how many shots were actually consumed (clamped to ShotsRemaining).
        /// Expires the shooter when its shots reach zero. Used by GridController to repay
        /// the cells opened by a bomb explosion.
        /// </summary>
        public int ConsumeShots(int amount)
        {
            if (amount <= 0 || shotsRemaining <= 0) return 0;
            int taken = Mathf.Min(amount, shotsRemaining);
            shotsRemaining -= taken;
            // Mirror the deduction on the bus: stickmen leave from the back
            // (hidden reserve drains first, then back seats despawn).
            if (seats != null) seats.ConsumeFromBack(taken);
            if (shotsRemaining <= 0) Expire();
            return taken;
        }

        private void OnDestroy()
        {
            boardingTween?.Kill();
            facingTween?.Kill();
            scaleTween?.Kill();
            wobbleTween?.Kill();
        }
    }
}
