using System;
using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Grid;
using PixelShoot.Bullets;
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

    public class Shooter : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer meshRenderer;
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float jumpPower = 1.5f;

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
        }

        public void SetGridAndConveyor(GridController g, ConveyorController cv)
        {
            grid = g;
            conveyor = cv;
        }

        /// <summary>Animate jump from current position to a target world position, then call onDone.</summary>
        public void JumpTo(Vector3 worldTarget, float duration, Action onDone, ShooterState endState)
        {
            state = ShooterState.Boarding;
            transform.SetParent(null, true);
            boardingTween?.Kill();
            // DOJump itself is a sequence of tweens internally — wrapping it in another
            // Sequence just to attach OnComplete doubled the tween count for every jump.
            boardingTween = transform.DOJump(worldTarget, jumpPower, 1, Mathf.Max(0.05f, duration))
                .OnComplete(() =>
                {
                    state = endState;
                    onDone?.Invoke();
                });
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
        }

        public void SetPathProgress(float progress) => pathProgress = progress;
        public void SetPathSpeed(float speed) => pathSpeed = speed;

        private void Update()
        {
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
            target.ReserveHit();
            shotsRemaining--;

            if (bulletPrefab != null)
            {
                var origin = muzzle != null ? muzzle.position : transform.position;
                var bullet = Instantiate(bulletPrefab);
                bullet.Fire(origin, target, grid, color);
            }
            else
            {
                grid.NotifyBoxHit(target);
            }

            if (shotsRemaining <= 0) Expire();
        }

        public void Expire()
        {
            if (state == ShooterState.Expired) return;
            state = ShooterState.Expired;
            OnExpired?.Invoke(this);

            transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (this != null) Destroy(gameObject); });
        }

        private void OnDestroy()
        {
            boardingTween?.Kill();
        }
    }
}
