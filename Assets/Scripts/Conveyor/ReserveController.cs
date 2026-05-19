using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Shooters;

namespace PixelShoot.Conveyor
{
    public class ReserveController : MonoBehaviour
    {
        [SerializeField] private Transform[] slotTransforms;
        [SerializeField] private float jumpDuration = 0.4f;

        private Shooter[] occupants;
        private int capacity;

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

        public void Occupy(int index, Shooter shooter)
        {
            if (occupants == null || index < 0 || index >= occupants.Length) return;
            occupants[index] = shooter;
        }

        public void FreeSlot(Shooter shooter)
        {
            if (occupants == null) return;
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == shooter) { occupants[i] = null; return; }
        }

        public bool HasAny()
        {
            if (occupants == null) return false;
            foreach (var s in occupants) if (s != null) return true;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (slotTransforms == null) return;
            for (int i = 0; i < slotTransforms.Length; i++)
            {
                var t = slotTransforms[i];
                if (t == null) continue;
                // Slot pad
                Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.85f);
                Gizmos.DrawWireCube(t.position, new Vector3(0.8f, 0.05f, 0.8f));
                // Connector to next slot
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
