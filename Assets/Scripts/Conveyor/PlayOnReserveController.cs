using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Shooters;

namespace PixelShoot.Conveyor
{
    /// <summary>
    /// "Play on" reservoir — unlocked when the player chooses to continue after a fail.
    /// Unlimited capacity, left-to-right ordering. Slot positions are derived from
    /// slotsRoot.position + slotsRoot.right * (spacing * index). Shooters are added at
    /// the end, removed (back to conveyor) from anywhere, and the rest compact left
    /// to fill the gap — same dynamic as ReserveController.
    /// </summary>
    public class PlayOnReserveController : MonoBehaviour
    {
        [SerializeField] private Transform slotsRoot;
        [Tooltip("World distance between adjacent play-on slots, along slotsRoot's local +X.")]
        [SerializeField] private float slotSpacing = 1.1f;
        [SerializeField] private float jumpDuration = 0.4f;
        [SerializeField] private float compactJumpDuration = 0.22f;
        [Tooltip("Uniform scale multiplier (relative to spawn scale) applied to buses parked in play-on slots.")]
        [SerializeField, Min(0.01f)] private float slotScale = 1f;
        [Tooltip("World-space euler rotation the buses settle into while parked in play-on slots.")]
        [SerializeField] private Vector3 slotRotation = Vector3.zero;

        private readonly List<Shooter> occupants = new List<Shooter>();
        private bool compactPending;

        public int Count => occupants.Count;
        public float JumpDuration => jumpDuration;
        public IReadOnlyList<Shooter> Occupants => occupants;

        public Vector3 GetSlotPosition(int index)
        {
            if (slotsRoot == null) return transform.position + new Vector3(index * slotSpacing, 0f, 0f);
            return slotsRoot.position + slotsRoot.right * (slotSpacing * index);
        }

        /// <summary>Append a shooter to the right end, animating it into place.</summary>
        public void Append(Shooter shooter)
        {
            if (shooter == null) return;
            int idx = occupants.Count;
            occupants.Add(shooter);
            var target = GetSlotPosition(idx);
            shooter.JumpTo(target, jumpDuration, NotifyIncomingLanded, ShooterState.InReserve, slotScale, Quaternion.Euler(slotRotation));
        }

        public bool Contains(Shooter shooter) => shooter != null && occupants.Contains(shooter);

        /// <summary>Remove a shooter and request a left-compact of the rest.</summary>
        public bool Remove(Shooter shooter)
        {
            if (!occupants.Remove(shooter)) return false;
            compactPending = true;
            TryCompact();
            return true;
        }

        public void NotifyIncomingLanded() => TryCompact();

        private void TryCompact()
        {
            if (!compactPending) return;
            if (HasBoardingShooter()) return;
            DoCompact();
        }

        private bool HasBoardingShooter()
        {
            foreach (var s in occupants)
                if (s != null && s.State == ShooterState.Boarding) return true;
            return false;
        }

        private void DoCompact()
        {
            compactPending = false;
            for (int i = 0; i < occupants.Count; i++)
            {
                var s = occupants[i];
                if (s == null) continue;
                var target = GetSlotPosition(i);
                if (Vector3.Distance(s.transform.position, target) < 0.01f) continue;
                s.JumpTo(target, compactJumpDuration, NotifyIncomingLanded, ShooterState.InReserve, slotScale, Quaternion.Euler(slotRotation));
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Show the first 8 anticipated slot positions even when empty, so the row is visible while authoring.
            Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.7f);
            int preview = Mathf.Max(occupants.Count + 2, 6);
            for (int i = 0; i < preview; i++)
            {
                Gizmos.DrawWireCube(GetSlotPosition(i), new Vector3(0.8f, 0.05f, 0.8f));
                if (i + 1 < preview)
                    Gizmos.DrawLine(GetSlotPosition(i), GetSlotPosition(i + 1));
            }
        }
#endif
    }
}
