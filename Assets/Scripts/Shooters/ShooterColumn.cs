using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// One column of shooters laid out along the column's local -Z axis (top-down play,
    /// Y stays 0). The shooter at local z=0 is the topmost (clickable); others trail
    /// behind it at increasing negative z.
    /// </summary>
    public class ShooterColumn : MonoBehaviour
    {
        [SerializeField] private float stackSpacing = 1.1f;
        [SerializeField] private float restackDuration = 0.25f;

        private readonly List<Shooter> shooters = new List<Shooter>();
        private Func<Shooter, bool> launchRequest;
        private Action<Shooter> reserveClickHandler;

        public void Initialize(Func<Shooter, bool> launchHandler, Action<Shooter> reserveClick)
        {
            launchRequest = launchHandler;
            reserveClickHandler = reserveClick;
        }

        public void AddShooter(Shooter shooter)
        {
            shooter.transform.SetParent(transform, false);
            shooters.Add(shooter);
            LayoutImmediate();

            var click = shooter.GetComponent<ShooterClickHandler>();
            if (click == null) click = shooter.gameObject.AddComponent<ShooterClickHandler>();
            click.Configure(shooter, this, reserveClickHandler);
        }

        // Top of column = last element (matches LevelData semantics: list order = bottom-to-top).
        public Shooter TopShooter => shooters.Count > 0 ? shooters[shooters.Count - 1] : null;

        public bool TryLaunchShooter(Shooter shooter)
        {
            if (shooter == null) return false;
            if (shooter != TopShooter) return false;
            if (shooter.State != ShooterState.InColumn) return false;

            if (launchRequest != null && launchRequest(shooter))
            {
                shooters.Remove(shooter);
                RestackAnimated();
                return true;
            }
            return false;
        }

        private void LayoutImmediate()
        {
            int count = shooters.Count;
            for (int i = 0; i < count; i++)
            {
                shooters[i].transform.localPosition = LocalPositionFor(i, count);
            }
        }

        private void RestackAnimated()
        {
            int count = shooters.Count;
            for (int i = 0; i < count; i++)
            {
                var tr = shooters[i].transform;
                var target = LocalPositionFor(i, count);
                // Already in place — no tween needed.
                if ((tr.localPosition - target).sqrMagnitude < 0.0001f) continue;
                // Kill any previous restack tween on THIS transform so rapid clicks
                // don't stack multiple parallel DOLocalMoves on the same shooter.
                tr.DOKill();
                tr.DOLocalMove(target, restackDuration).SetEase(Ease.OutCubic);
            }
        }

        // Top (last index) sits at z=0, each preceding one trails by stackSpacing along -Z.
        private Vector3 LocalPositionFor(int index, int count)
        {
            float z = -(count - 1 - index) * stackSpacing;
            return new Vector3(0f, 0f, z);
        }

#if UNITY_EDITOR
        [SerializeField] private int gizmoPreviewCount = 3;

        private void OnDrawGizmos()
        {
            // Preview the stack direction even before runtime spawning.
            int count = shooters != null && shooters.Count > 0 ? shooters.Count : gizmoPreviewCount;
            // Top marker (z=0): clickable
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            // Trail
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            for (int i = 1; i < count; i++)
            {
                Vector3 p = transform.TransformPoint(new Vector3(0f, 0f, -i * stackSpacing));
                Gizmos.DrawWireSphere(p, 0.18f);
                Vector3 prev = transform.TransformPoint(new Vector3(0f, 0f, -(i - 1) * stackSpacing));
                Gizmos.DrawLine(prev, p);
            }
        }
#endif
    }
}
