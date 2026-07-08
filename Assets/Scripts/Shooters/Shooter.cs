using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
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
        [Tooltip("Extra bus meshes (body panels etc.) that should also be tinted with the bus colour's ShooterMaterial. The main meshRenderer above is coloured too even if not listed here.")]
        [SerializeField] private MeshRenderer[] colorRenderers;
        [Tooltip("Seat manager that owns the visible stickmen. If null, hits are applied instantly with no projectile.")]
        [SerializeField] private BusSeatController seats;
        [Tooltip("Optional world-space label showing how many stickmen are left to spawn (add a Billboard so it faces the camera).")]
        [SerializeField] private TMP_Text shotCountLabel;
        [SerializeField] private float jumpPower = 1.5f;
        [Header("Conveyor facing")]
        [Tooltip("How fast the bus turns to face its travel direction, in degrees per second. Higher = snappier cornering.")]
        [SerializeField] private float turnSpeed = 540f;
        [Tooltip("How far ahead along the path (world units) to sample when computing the travel direction.")]
        [SerializeField] private float facingLookAhead = 0.15f;
        [Tooltip("Uniform scale multiplier applied while riding the conveyor. The bus tweens to this during the boarding jump and back to its spawn scale when it hops off into reserve.")]
        [SerializeField] private float conveyorScale = 1.2f;
        [Tooltip("On seating, the bus surges to this many times the normal conveyor speed, then decays back over Board Boost Duration. 1 = no surge.")]
        [SerializeField, Min(1f)] private float boardBoostMultiplier = 2.5f;
        [Tooltip("Seconds for the seating speed-surge to ease back to the stable conveyor speed.")]
        [SerializeField, Min(0f)] private float boardBoostDuration = 0.25f;

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

        [Header("Surprise")]
        [Tooltip("Parent of all the normal bus visuals (body + seats). Hidden while the bus is a surprise, shown on reveal. If null, falls back to toggling the mesh renderer + seats object.")]
        [SerializeField] private GameObject busVisualRoot;
        [Tooltip("The '?' cover shown while the bus is a surprise. Hidden on reveal.")]
        [SerializeField] private GameObject surpriseVisual;
        [Tooltip("Punch scale applied to the body on surprise reveal for a little pop.")]
        [SerializeField] private float revealPunch = 0.25f;

        [Header("Link")]
        [Tooltip("LineRenderer (on the OWNER bus) drawn through every member of the link group. Optional.")]
        [SerializeField] private LineRenderer linkLine;
        [Tooltip("Vertical offset of the link line above each bus position.")]
        [SerializeField] private float linkLineYOffset = 0.4f;
        [Tooltip("Optional visual enabled only on linked buses (e.g. a tow hook / chain).")]
        [SerializeField] private GameObject linkedVisual;

        [Header("Lock")]
        [Tooltip("Lock overlay shown while this bus is locked. Hidden (with a pop) on unlock.")]
        [SerializeField] private GameObject lockVisual;

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
        private float boostTimer; // seating speed-surge countdown
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

        // ── Alive-bus registry (drives the endgame "last N buses" mode) ──────
        private static readonly HashSet<Shooter> aliveBuses = new HashSet<Shooter>();
        public static int AliveCount => aliveBuses.Count;
        public static event Action AliveCountChanged;
        public static void ClearAliveRegistry() => aliveBuses.Clear();
        private void RegisterAlive()   { if (aliveBuses.Add(this))    AliveCountChanged?.Invoke(); }
        private void UnregisterAlive() { if (aliveBuses.Remove(this)) AliveCountChanged?.Invoke(); }

        // ── Surprise / Link / Lock state ─────────────────────────────────────
        public bool IsSurprise { get; private set; }
        public int LinkGroupId { get; private set; }
        /// <summary>Shared list reference across all members of the same link group.</summary>
        public List<Shooter> LinkGroup { get; private set; }
        public bool IsLinkOwner { get; private set; }
        public bool IsLinked => LinkGroup != null && LinkGroup.Count >= 2;

        public int LockKeyId { get; private set; }
        public bool IsLocked { get; private set; }

        public ColorData Color => color;
        public int ShotsRemaining => shotsRemaining;
        public ShooterState State => state;
        public float PathProgress => pathProgress;

        public void Initialize(ColorData c, int shotCount, bool isSurprise = false, int linkGroupId = 0, int lockKeyId = 0)
        {
            color = c;
            shotsRemaining = shotCount;
            state = ShooterState.InColumn;
            RegisterAlive();
            IsSurprise = isSurprise;
            LinkGroupId = linkGroupId;
            LockKeyId = lockKeyId;
            IsLocked = lockKeyId > 0;
            if (lockVisual != null) lockVisual.SetActive(IsLocked);

            if (c != null && c.ShooterMaterial != null)
            {
                // Tint the main mesh + every extra bus mesh with the bus colour.
                // Use sharedMaterials (not materials) so we don't clone-and-leak the
                // material instance every time Initialize runs — especially important
                // when the level editor rebuilds the scene preview in edit mode.
                ApplyColorMaterial(null, c.ShooterMaterial);
                if (colorRenderers != null)
                    foreach (var r in colorRenderers)
                        ApplyColorMaterial(r, c.ShooterMaterial);
                // Link line uses the same bus colour material.
                ApplyColorMaterial(linkLine, c.ShooterMaterial);
            }

            // Bind the bus colour; passengers are now spawned on demand (one per shot).
            if (seats != null) seats.Initialize(shotCount, c);
            RefreshShotLabel();

            // Surprise: hide the real bus behind the "?" cover until it surfaces.
            SetVisualHidden(isSurprise);
            if (surpriseVisual != null) surpriseVisual.SetActive(isSurprise);

            StartEngineWobble();
        }

        // Update the world-space "shots left" label (no-op if none assigned).
        private void RefreshShotLabel()
        {
            if (shotCountLabel != null) shotCountLabel.text = Mathf.Max(0, shotsRemaining).ToString();
        }

        // Swap element 0 of a renderer's shared materials to the bus colour (no leak).
        private static void ApplyColorMaterial(Renderer r, Material mat)
        {
            if (r == null || mat == null) return;
            Material[] mats = r.sharedMaterials;
            if (mats.Length == 0) return;
            mats[0] = mat;
            r.sharedMaterials = mats;
        }

        // ── Surprise ─────────────────────────────────────────────────────────
        private void SetVisualHidden(bool hidden)
        {
            if (busVisualRoot != null)
            {
                busVisualRoot.SetActive(!hidden);
            }
            else
            {
                if (seats != null) seats.gameObject.SetActive(!hidden);
            }
            // The count label lives outside the visual root — hide/show it in lockstep.
            if (shotCountLabel != null) shotCountLabel.gameObject.SetActive(!hidden);
        }

        /// <summary>
        /// Reveal a surprise bus: drop the "?" cover, show the real body + count, pop a
        /// little punch. Idempotent — safe to call again. Called automatically when the
        /// bus surfaces to the top of its column, and defensively at board time.
        /// </summary>
        public void RevealSurprise()
        {
            if (!IsSurprise) return;
            IsSurprise = false;
            if (surpriseVisual != null) surpriseVisual.SetActive(false);
            SetVisualHidden(false);
            if (revealPunch > 0f && Application.isPlaying)
            {
                var t = busVisualRoot != null ? busVisualRoot.transform
                      :null;
                if (t != null && t != transform)
                    t.DOPunchScale(t.localScale * revealPunch, 0.3f, 6, 0.6f);
            }
        }

        // ── Link ─────────────────────────────────────────────────────────────
        /// <summary>Assign the shared member list. Exactly one member is the owner (draws the line).</summary>
        public void SetLinkGroup(List<Shooter> group, bool isOwner)
        {
            LinkGroup = group;
            IsLinkOwner = isOwner;
            if (linkedVisual != null) linkedVisual.SetActive(IsLinked);
            RefreshLinkLine();
        }

        /// <summary>Silently drop this bus's link state (no cascade) — used during dissolve.</summary>
        public void ClearLinkState()
        {
            LinkGroup = null;
            IsLinkOwner = false;
            LinkGroupId = 0;
            if (linkedVisual != null) linkedVisual.SetActive(false);
            if (linkLine != null) linkLine.positionCount = 0;
        }

        /// <summary>
        /// Remove this bus from its group mid-life (e.g. it was consumed alone). If ≤1
        /// member remains the whole link dissolves; otherwise ownership transfers if needed.
        /// </summary>
        public void BreakLink()
        {
            var group = LinkGroup;
            if (group == null) { ClearLinkState(); return; }

            bool wasOwner = IsLinkOwner;
            group.Remove(this);
            ClearLinkState();

            if (group.Count <= 1)
            {
                foreach (var m in group.ToArray())
                    if (m != null) m.ClearLinkState();
                group.Clear();
                return;
            }
            if (wasOwner)
            {
                var newOwner = group[0];
                for (int i = 0; i < group.Count; i++)
                    if (group[i] != null) group[i].SetLinkGroup(group, isOwner: group[i] == newOwner);
            }
        }

        private void RefreshLinkLine()
        {
            if (linkLine == null) return;
            // Only the owner renders the polyline; non-owners keep an empty line.
            if (!IsLinkOwner || LinkGroup == null) { linkLine.positionCount = 0; return; }
            linkLine.positionCount = LinkGroup.Count;
            // Populate positions immediately so the line shows in the level-editor preview,
            // where LateUpdate never runs (edit mode).
            UpdateLinkLinePositions();
        }

        private void LateUpdate() => UpdateLinkLinePositions();

        private void UpdateLinkLinePositions()
        {
            if (linkLine == null || !IsLinkOwner || LinkGroup == null) return;
            if (linkLine.positionCount != LinkGroup.Count) linkLine.positionCount = LinkGroup.Count;
            for (int i = 0; i < LinkGroup.Count; i++)
            {
                var m = LinkGroup[i];
                Vector3 p = m != null ? m.transform.position : transform.position;
                p.y += linkLineYOffset;
                linkLine.SetPosition(i, p);
            }
        }

        /// <summary>True when every still-alive member of this link group has 0 shots left.</summary>
        private bool AllGroupSpent()
        {
            if (LinkGroup == null) return shotsRemaining <= 0;
            foreach (var m in LinkGroup)
                if (m != null && m.shotsRemaining > 0) return false;
            return true;
        }

        /// <summary>
        /// Called when this bus runs out of shots. Unlinked → expire immediately. Linked →
        /// stay (it just stops firing) until EVERY member is spent, then the whole group
        /// dissolves at once. Mirrors HexaSort's "link doesn't break on single exhaustion".
        /// </summary>
        private void HandleShotsDepleted()
        {
            if (!IsLinked)
            {
                Expire();
                return;
            }
            if (AllGroupSpent())
            {
                foreach (var m in LinkGroup.ToArray())
                    if (m != null) m.ExpireForDissolve();
            }
            // else: keep riding idle; firing stops naturally via the shotsRemaining<=0 check.
        }

        /// <summary>Expire as part of a coordinated link-group dissolve (clears link state first).</summary>
        private void ExpireForDissolve()
        {
            ClearLinkState();
            Expire();
        }

        // ── Lock ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Called when this bus is the top of its column. If it's locked and its key has
        /// been collected, unlock it (drop the lock visual with a pop). Otherwise stays
        /// locked. No-op for unlocked buses. Returns true if it is (now) tappable.
        /// </summary>
        public bool TryUnlock()
        {
            if (!IsLocked) return true;
            var km = PixelShoot.Game.KeyManager.Instance;
            if (km == null || !km.IsCollected(LockKeyId)) return false;

            IsLocked = false;
            if (lockVisual != null)
            {
                if (Application.isPlaying)
                    lockVisual.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack)
                        .OnComplete(() => { if (lockVisual != null) lockVisual.SetActive(false); });
                else
                    lockVisual.SetActive(false);
            }
            return true;
        }

        /// <summary>Little shake when a locked bus is tapped but can't board yet.</summary>
        public void PlayLockedFeedback()
        {
            if (!Application.isPlaying) return;
            var t = busVisualRoot != null ? busVisualRoot.transform
                  : null;
            t.DOShakePosition(0.25f, 0.12f, 12, 90f, false, true);
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
                  : null;
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
            float scaleMultiplier = 1f, Quaternion? landingRotation = null, Ease? jumpEase = null)
        {
            if (!baseScaleCaptured)
            {
                baseScale = transform.localScale;
                baseScaleCaptured = true;
            }

            state = ShooterState.Boarding;
            // Stop ANY tween on the ROOT before reparenting — crucially a still-running
            // restack DOLocalMove started by the column when a bus above just launched.
            // Left alive, SetParent(null) turns its local target into a world target and it
            // yanks the bus toward the column origin ("teleport") while the jump also runs.
            // The engine wobble lives on a CHILD transform, so it's unaffected.
            transform.DOKill();
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
            if (jumpEase.HasValue) boardingTween.SetEase(jumpEase.Value);

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
            boostTimer = boardBoostDuration; // surge on seating, then decay to normal
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

            // Seating speed-surge: start fast (boardBoostMultiplier) and ease back to 1.
            float speedMul = 1f;
            if (boostTimer > 0f && boardBoostDuration > 0f)
            {
                float t = boostTimer / boardBoostDuration; // 1 → 0
                speedMul = Mathf.Lerp(1f, boardBoostMultiplier, t);
                boostTimer -= Time.deltaTime;
            }

            pathProgress += pathSpeed * speedMul * conveyor.SpeedMultiplier * Time.deltaTime;
            float maxProgress = conveyor.MaxPathProgress;

            if (conveyor.LoopMode && shotsRemaining > 0 && maxProgress > 0f)
            {
                // Endgame loop: travel the polyline AND the closing bridge (last node → first
                // node), then wrap — the belt is a continuous loop, no jump, no reserve.
                float loopLen = conveyor.LoopPathLength;
                if (pathProgress >= loopLen)
                {
                    pathProgress -= loopLen;              // carry overflow → seamless
                    lastEngagedParallelIndex = int.MinValue; // fresh lap can fire again
                    lastSide = default;
                }
            }
            else if (pathProgress >= maxProgress)
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
            RefreshShotLabel();

            if (seats != null)
            {
                launchQueue.Enqueue(target);
                DrainLaunchQueue(); // launch right away if the interval has elapsed
            }
            else
            {
                // No seat system → apply the hit instantly.
                grid.NotifyBoxHit(target);
                if (shotsRemaining <= 0) HandleShotsDepleted();
            }
        }

        /// <summary>
        /// Pops at most one queued launch per call, spacing consecutive launches by
        /// <see cref="launchInterval"/>. A stickman is spawned from the pool at ACTUAL
        /// launch time and immediately flies at its target.
        /// </summary>
        private void DrainLaunchQueue()
        {
            if (launchQueue.Count == 0) return;
            if (Time.time < nextLaunchTime) return;

            LaunchOne(launchQueue.Dequeue());
            nextLaunchTime = Time.time + launchInterval;

            // The last queued passenger just left and there's nothing left to shoot.
            if (launchQueue.Count == 0 && shotsRemaining <= 0) HandleShotsDepleted();
        }

        private void LaunchOne(Box target)
        {
            var stickman = seats != null ? seats.SpawnRunner() : null;
            if (stickman != null) stickman.LaunchAt(target, grid);
            else if (grid != null) grid.NotifyBoxHit(target); // bus visually empty — instant hit fallback
        }

        public void Expire()
        {
            if (state == ShooterState.Expired) return;
            UnregisterAlive();
            // Still linked when expiring outside a coordinated dissolve (e.g. path-end)?
            // Detach cleanly so siblings don't hold a destroyed reference.
            if (IsLinked) BreakLink();
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
            RefreshShotLabel();
            // Mirror the deduction on the bus: stickmen leave from the back
            // (hidden reserve drains first, then back seats despawn).
            if (seats != null) seats.ConsumeFromBack(taken);
            if (shotsRemaining <= 0) HandleShotsDepleted();
            return taken;
        }

        private void OnDestroy()
        {
            UnregisterAlive();
            boardingTween?.Kill();
            facingTween?.Kill();
            scaleTween?.Kill();
            wobbleTween?.Kill();
        }
    }
}
