using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Shooters;

namespace PixelShoot.Conveyor
{
    public class ReserveController : MonoBehaviour
    {
        [SerializeField] private Transform[] slotTransforms;
        [SerializeField] private float jumpDuration = 0.4f;
        [Tooltip("Duration of the left-shift hop when reserve slots compact after a free.")]
        [SerializeField] private float compactJumpDuration = 0.22f;
        [Tooltip("Uniform scale multiplier (relative to spawn scale) applied to buses parked in reserve slots.")]
        [SerializeField, Min(0.01f)] private float slotScale = 1f;
        [Tooltip("World-space euler rotation the buses settle into while parked in reserve slots.")]
        [SerializeField] private Vector3 slotRotation = Vector3.zero;

        private Shooter[] occupants;
        private int capacity;
        private bool compactPending;

        public int Capacity => capacity;
        public int OccupiedCount
        {
            get
            {
                if (occupants == null) return 0;
                int c = 0;
                foreach (var s in occupants) if (s != null) c++;
                return c;
            }
        }
        public bool IsFull => OccupiedCount >= capacity;

        public void Initialize(int slotCapacity)
        {
            capacity = Mathf.Min(slotCapacity, slotTransforms != null ? slotTransforms.Length : 0);
            occupants = new Shooter[capacity];
            compactPending = false;
        }

        public int FindFreeSlot()
        {
            if (occupants == null) return -1;
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == null) return i;
            return -1;
        }

        public Vector3 GetSlotPosition(int index)
        {
            if (slotTransforms == null || index < 0 || index >= slotTransforms.Length) return transform.position;
            return slotTransforms[index].position;
        }

        public float JumpDuration => jumpDuration;
        public float SlotScale => slotScale;
        public Quaternion SlotRotation => Quaternion.Euler(slotRotation);

        public void Occupy(int index, Shooter shooter)
        {
            if (occupants == null || index < 0 || index >= occupants.Length) return;
            occupants[index] = shooter;
        }

        /// <summary>
        /// Remove a shooter from its reserve slot and request a left-compact so any
        /// shooters to its right shift down to fill the gap. The compact is deferred
        /// while there are still shooters mid-air (Boarding state) so animations
        /// never overlap with an incoming conveyor→reserve jump.
        /// </summary>
        public void FreeSlot(Shooter shooter)
        {
            if (occupants == null) return;
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == shooter) { occupants[i] = null; break; }
            compactPending = true;
            TryCompact();
        }

        /// <summary>
        /// Should be invoked when ANY shooter that was Boarding into / inside reserve
        /// finishes its jump. Re-checks whether a deferred compact can now run.
        /// </summary>
        public void NotifyIncomingLanded()
        {
            TryCompact();
        }

        private void TryCompact()
        {
            if (!compactPending) return;
            if (HasBoardingShooter()) return; // wait — something is mid-jump
            DoCompact();
        }

        private bool HasBoardingShooter()
        {
            if (occupants == null) return false;
            foreach (var s in occupants)
                if (s != null && s.State == ShooterState.Boarding) return true;
            return false;
        }

        private void DoCompact()
        {
            compactPending = false;

            var settled = new List<Shooter>();
            foreach (var s in occupants)
                if (s != null) settled.Add(s);
            for (int i = 0; i < occupants.Length; i++) occupants[i] = null;

            for (int i = 0; i < settled.Count; i++)
            {
                occupants[i] = settled[i];
                Vector3 target = GetSlotPosition(i);
                if (Vector3.Distance(settled[i].transform.position, target) < 0.01f) continue;
                // The shooter briefly enters Boarding while hopping; HasBoardingShooter
                // will block any new compact until it lands, then the callback retries.
                settled[i].JumpTo(target, compactJumpDuration, NotifyIncomingLanded, ShooterState.InReserve, slotScale, SlotRotation);
            }
        }

        public bool HasAny()
        {
            if (occupants == null) return false;
            foreach (var s in occupants) if (s != null) return true;
            return false;
        }

        public bool Contains(Shooter shooter)
        {
            if (occupants == null || shooter == null) return false;
            foreach (var s in occupants) if (s == shooter) return true;
            return false;
        }

        /// <summary>Removes the leftmost occupant.</summary>
        public Shooter TryPopFirst()
        {
            if (occupants == null) return null;
            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] != null)
                {
                    var s = occupants[i];
                    occupants[i] = null;
                    compactPending = true;
                    TryCompact();
                    return s;
                }
            }
            return null;
        }

        /// <summary>Removes the rightmost occupant. Used by Play-On to harvest one reserve shooter from the back.</summary>
        public Shooter TryPopLast()
        {
            if (occupants == null) return null;
            for (int i = occupants.Length - 1; i >= 0; i--)
            {
                if (occupants[i] != null)
                {
                    var s = occupants[i];
                    occupants[i] = null;
                    compactPending = true;
                    TryCompact();
                    return s;
                }
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (slotTransforms == null) return;
            for (int i = 0; i < slotTransforms.Length; i++)
            {
                var t = slotTransforms[i];
                if (t == null) continue;
                Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.85f);
                Gizmos.DrawWireCube(t.position, new Vector3(0.8f, 0.05f, 0.8f));
                if (i + 1 < slotTransforms.Length && slotTransforms[i + 1] != null)
                {
                    Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
                    Gizmos.DrawLine(t.position, slotTransforms[i + 1].position);
                }
            }
        }
#endif
    }
}
