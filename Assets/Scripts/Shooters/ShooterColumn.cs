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
        // Active column registry used by bomb payback and other cross-column queries.
        private static readonly List<ShooterColumn> all = new List<ShooterColumn>();
        public static IReadOnlyList<ShooterColumn> All => all;
        private void OnEnable()  { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() { all.Remove(this); }

        public IReadOnlyList<Shooter> Shooters => shooters;

        /// <summary>Find the column currently holding <paramref name="s"/>, or null.</summary>
        public static ShooterColumn ColumnOf(Shooter s)
        {
            if (s == null) return null;
            foreach (var c in all)
                if (c != null && c.shooters.Contains(s)) return c;
            return null;
        }

        public int IndexOf(Shooter s) => shooters.IndexOf(s);

        /// <summary>Remove a specific shooter from this column (restacks + refreshes the new top).</summary>
        public bool RemoveShooter(Shooter s)
        {
            if (s == null || !shooters.Remove(s)) return false;
            RestackAnimated();
            RefreshTop();
            return true;
        }

        /// <summary>Auto-reveal the top bus when it's a surprise — mirrors HexaSort's reveal-on-surface.</summary>
        public void RevealTopIfSurprise()
        {
            var top = TopShooter;
            if (top != null && top.IsSurprise) top.RevealSurprise();
        }

        /// <summary>
        /// Refresh the column's top bus: reveal it if it's a surprise, and try to unlock it
        /// if it's locked (its key may already be collected). Called whenever the top changes
        /// and whenever a key is collected.
        /// </summary>
        public void RefreshTop()
        {
            var top = TopShooter;
            if (top == null) return;
            if (top.IsSurprise) top.RevealSurprise();
            if (top.IsLocked) top.TryUnlock();
        }

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

            // Locked: a top bus whose key isn't collected yet can't board. Try once more
            // (key may have just been collected), else shake and reject.
            if (shooter.IsLocked)
            {
                if (!shooter.TryUnlock()) { shooter.PlayLockedFeedback(); return false; }
            }

            // Linked: the GameController removes EVERY member from its column itself,
            // so this column must not pop the tapped one here.
            if (shooter.IsLinked)
                return launchRequest != null && launchRequest(shooter);

            if (launchRequest != null && launchRequest(shooter))
            {
                shooters.Remove(shooter);
                RestackAnimated();
                RefreshTop();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Shuffle booster: replace this column's stack (bottom-to-top order). Reparents and
        /// reconfigures any incoming shooter (keeping its world pose so it slides in), animates
        /// everyone to place, and refreshes the new top (revealing a surprise that surfaced).
        /// </summary>
        public void ApplyShuffledStack(List<Shooter> newList)
        {
            shooters.Clear();
            if (newList != null) shooters.AddRange(newList);
            foreach (var s in shooters)
            {
                if (s == null) continue;
                if (s.transform.parent != transform) s.transform.SetParent(transform, true); // keep world pose → slides in
                var click = s.GetComponent<ShooterClickHandler>();
                if (click == null) click = s.gameObject.AddComponent<ShooterClickHandler>();
                click.Configure(s, this, reserveClickHandler);
            }
            RestackAnimated();
            RefreshTop();
        }

        /// <summary>Drop a shooter from this column (used when a bomb pays back its shots in full).</summary>
        public void RemoveExpiredShooter(Shooter s)
        {
            if (s == null) return;
            if (shooters.Remove(s)) { RestackAnimated(); RefreshTop(); }
        }

        /// <summary>
        /// Walks all active columns and subtracts <paramref name="amount"/> shots from
        /// shooters matching the gameplay color, starting with the BOTTOM-MOST shooter
        /// (lowest index in its column — the one farthest from the player's tap point).
        /// A shooter whose shots reach 0 is Expired and removed from the column.
        /// Returns how many shots were actually consumed (could be &lt; amount if no more matches).
        /// </summary>
        public static int ConsumeShotsForGameplayColor(PixelShoot.Data.ColorData gameplayColor, int amount)
        {
            if (gameplayColor == null || amount <= 0) return 0;

            // Gather candidates across every active column, then sort:
            //   1) by within-column index ascending (back-of-queue first = "bottom-most")
            //   2) tiebreak: column instanceID for determinism
            var pool = new List<(ShooterColumn col, int idx, Shooter s)>();
            foreach (var col in all)
            {
                if (col == null) continue;
                for (int i = 0; i < col.shooters.Count; i++)
                {
                    var s = col.shooters[i];
                    if (s == null || s.Color == null) continue;
                    if (s.Color.GameplayColor != gameplayColor) continue;
                    pool.Add((col, i, s));
                }
            }
            pool.Sort((a, b) =>
            {
                int cmp = a.idx.CompareTo(b.idx);
                if (cmp != 0) return cmp;
                return a.col.GetInstanceID().CompareTo(b.col.GetInstanceID());
            });

            int consumed = 0;
            foreach (var entry in pool)
            {
                if (amount <= 0) break;
                int took = entry.s.ConsumeShots(amount);
                amount   -= took;
                consumed += took;
                if (entry.s.ShotsRemaining <= 0) entry.col.RemoveExpiredShooter(entry.s);
            }
            return consumed;
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
