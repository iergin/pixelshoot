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
        [SerializeField] private MeshRenderer meshRenderer;
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
        private Sequence boardingSeq;

        // Engagement tracking: each column/row gets at most one shot per pass.
        // Reset when the shooter changes side or moves off the grid (parallel = -1).
        private GridSide lastSide;
        private int lastEngagedParallelIndex = int.MinValue;

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
                meshRenderer.sharedMaterial = c.ShooterMaterial;
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
            boardingSeq?.Kill();
            boardingSeq = DOTween.Sequence();
            boardingSeq.Append(transform.DOJump(worldTarget, jumpPower, 1, Mathf.Max(0.05f, duration)));
            boardingSeq.OnComplete(() =>
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
        }

        public void SetPathProgress(float progress) => pathProgress = progress;
        public void SetPathSpeed(float speed) => pathSpeed = speed;

        private void Update()
        {
            if (state != ShooterState.OnConveyor || conveyor == null) return;

            pathProgress += pathSpeed * Time.deltaTime;
            float maxProgress = conveyor.MaxPathProgress;
            if (pathProgress >= maxProgress)
            {
                OnPathEnded?.Invoke(this);
                // Subscriber must transition state (e.g. to Boarding for reserve, or Expired).
                // If nobody handled it, default to Expire to avoid an infinite loop.
                if (state == ShooterState.OnConveyor) Expire();
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
            boardingSeq?.Kill();
        }
    }
}
