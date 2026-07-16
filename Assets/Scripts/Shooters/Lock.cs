using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// A column barrier that occupies a slot in the stack exactly like a bus, but blocks the
    /// column until its key is collected. It is never boardable, never counts as a live bus,
    /// is ignored by the Claw, and is kept fixed by Shuffle.
    ///
    /// <para>A Lock IS-A <see cref="Shooter"/> only so it fits the column's <c>List&lt;Shooter&gt;</c>
    /// and reuses the stacking/positioning plumbing. It uses NONE of the bus fields (colour,
    /// shots, seats, wobble) — its setup goes through <see cref="InitializeAsLock"/>, not the
    /// bus <see cref="Shooter.Initialize"/>, so it never registers as alive.</para>
    /// </summary>
    public class Lock : Shooter
    {
        [Header("Lock")]
        [Tooltip("The lock model shown while this barrier is closed. Popped away on open.")]
        [SerializeField] private GameObject lockVisual;

        /// <summary>Key id this lock waits for; opens once that key is collected.</summary>
        public int KeyId { get; private set; }

        /// <summary>Set this up as a lock barrier waiting on <paramref name="keyId"/>. No colour,
        /// no seats, no RegisterAlive — just take a resting column slot and show the lock.</summary>
        public void InitializeAsLock(int keyId)
        {
            KeyId = keyId;
            ApplyColumnScale();
            if (lockVisual != null) lockVisual.SetActive(true);
        }

        /// <summary>
        /// Called when this lock is the top of its column. If its key has been collected, open:
        /// drop out of the column (which restacks) and destroy with a pop. Returns true if opened.
        /// </summary>
        public bool TryOpen()
        {
            var km = PixelShoot.Game.KeyManager.Instance;
            if (km == null || !km.IsCollected(KeyId)) return false;

            ShooterColumn.ColumnOf(this)?.RemoveShooter(this); // remove + restack the column
            if (Application.isPlaying)
                transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack)
                    .OnComplete(() => { if (this != null) Destroy(gameObject); });
            else if (this != null) DestroyImmediate(gameObject);
            return true;
        }

        /// <summary>Little shake when a still-locked barrier is tapped (key not collected yet).</summary>
        public void PlayLockedFeedback()
        {
            if (!Application.isPlaying) return;
            Transform t = lockVisual != null ? lockVisual.transform : transform;
            t.DOShakePosition(0.25f, 0.12f, 12, 90f, false, true);
        }
    }
}
