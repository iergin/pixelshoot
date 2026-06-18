using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Manages the bus's visible seats (typically 6, in a 2-wide / 3-row layout)
    /// and the queue of stickmen riding it.
    ///
    /// <para><b>Queue model</b>: <c>seated[i]</c> is the stickman LOGICALLY on seat i;
    /// seat 0 fires first. The list is mutated synchronously on every event (pop,
    /// consume), and visuals merely tween toward their assigned anchor afterwards.
    /// Because game logic never reads transform positions, firing again while a
    /// shift animation is still running can never desync the order.</para>
    ///
    /// <para><b>Hidden reserve</b>: when total shots exceed the seat count, the
    /// surplus is tracked as a number. Every time a seat frees up at the back, one
    /// reserve stickman spawns there.</para>
    ///
    /// <para>Anchors can be nested anywhere under the bus; targets are computed via
    /// <see cref="Transform.InverseTransformPoint"/> so the layout transform doesn't
    /// matter. Stickmen are parented to this component's transform so they ride the
    /// bus along the conveyor.</para>
    /// </summary>
    public class BusSeatController : MonoBehaviour
    {
        [Tooltip("Seat anchor transforms in firing order: element 0 = front seat (fires first). Typical bus: 6 anchors as 2 columns × 3 rows.")]
        [SerializeField] private Transform[] seatAnchors;
        [SerializeField] private Stickman stickmanPrefab;
        [Tooltip("Duration of the shift-forward hop when a seat frees up.")]
        [SerializeField] private float shiftDuration = 0.2f;

        private readonly List<Stickman> seated = new List<Stickman>();
        private int hiddenReserve;
        private ColorData color;

        public int SeatCount => seatAnchors != null ? seatAnchors.Length : 0;
        public int SeatedCount => seated.Count;

        public void Initialize(int totalShots, ColorData c)
        {
            color = c;
            Clear();
            int visible = Mathf.Min(totalShots, SeatCount);
            for (int i = 0; i < visible; i++)
            {
                var s = Spawn();
                seated.Add(s);
                s.SnapToSeat(LocalSeatPosition(i));
            }
            hiddenReserve = Mathf.Max(0, totalShots - visible);
        }

        /// <summary>
        /// Remove and return the front-seat stickman. List updates instantly; the
        /// remaining stickmen tween one seat forward and, if the hidden reserve still
        /// has people, a new stickman spawns at the back seat.
        /// Returns null when the bus is visually empty (fall back to an instant hit).
        /// </summary>
        public Stickman PopFront()
        {
            if (seated.Count == 0) return null;
            var front = seated[0];
            seated.RemoveAt(0);

            // Back seat freed → pull one out of the hidden reserve.
            if (hiddenReserve > 0 && seated.Count < SeatCount)
            {
                hiddenReserve--;
                var newcomer = Spawn();
                seated.Add(newcomer);
                // Spawn directly at the back anchor — no slide-in from origin.
                newcomer.SnapToSeat(LocalSeatPosition(seated.Count - 1));
            }

            Reflow();
            return front;
        }

        /// <summary>
        /// Bomb payback: remove <paramref name="amount"/> stickmen from the BACK of
        /// the bus. Hidden reserve is drained first (they're invisible anyway), then
        /// seated stickmen despawn back-to-front.
        /// </summary>
        public void ConsumeFromBack(int amount)
        {
            if (amount <= 0) return;

            int fromReserve = Mathf.Min(hiddenReserve, amount);
            hiddenReserve -= fromReserve;
            amount -= fromReserve;

            while (amount > 0 && seated.Count > 0)
            {
                int last = seated.Count - 1;
                var s = seated[last];
                seated.RemoveAt(last);
                if (s != null) s.DespawnImmediate();
                amount--;
            }
            Reflow();
        }

        /// <summary>Re-tween every seated stickman toward the anchor matching its list index.</summary>
        private void Reflow()
        {
            for (int i = 0; i < seated.Count; i++)
            {
                var s = seated[i];
                if (s == null) continue;
                s.MoveToSeat(LocalSeatPosition(i), shiftDuration);
            }
        }

        private Stickman Spawn()
        {
            var s = Instantiate(stickmanPrefab, transform);
            s.transform.localScale = Vector3.one * 1.27f;
            s.SetColor(color);
            s.PlayIdle();
            return s;
        }

        private Vector3 LocalSeatPosition(int seatIndex)
        {
            if (seatAnchors == null || seatIndex < 0 || seatIndex >= seatAnchors.Length || seatAnchors[seatIndex] == null)
                return Vector3.zero;
            return transform.InverseTransformPoint(seatAnchors[seatIndex].position);
        }

        private void Clear()
        {
            foreach (var s in seated)
                if (s != null) Destroy(s.gameObject);
            seated.Clear();
            hiddenReserve = 0;
        }
    }
}
