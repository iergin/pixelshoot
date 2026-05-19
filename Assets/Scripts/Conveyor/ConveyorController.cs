using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Shooters;

namespace PixelShoot.Conveyor
{
    public class ConveyorController : MonoBehaviour
    {
        [SerializeField] private Transform pathRoot;
        [Tooltip("World units per second the conveyor moves shooters along the path.")]
        [SerializeField] private float pathSpeed = 3.0f;
        [Tooltip("World units of safe spacing between two consecutive shooters.")]
        [SerializeField] private float safeSpacing = 1.2f;
        [Tooltip("Base duration of the boarding jump animation.")]
        [SerializeField] private float baseBoardingDuration = 0.45f;

        // Progress is expressed in world distance along the polyline.
        private readonly List<ConveyorPathNode> nodes = new List<ConveyorPathNode>();
        private readonly List<float> cumulativeDistances = new List<float>(); // cumulativeDistances[i] = distance from node 0 to node i
        private readonly List<Shooter> ridingShooters = new List<Shooter>();
        private int capacity;
        private int reservedCount;
        private float lastReservationLandTime = float.NegativeInfinity;

        public int Capacity => capacity;
        public int FreeCount => capacity - (ridingShooters.Count + reservedCount);
        public float MaxPathProgress => cumulativeDistances.Count > 0 ? cumulativeDistances[cumulativeDistances.Count - 1] : 0f;
        public Vector3 EntryWorldPosition => nodes.Count > 0 ? nodes[0].Position : transform.position;

        public void Initialize(int slotCapacity)
        {
            capacity = slotCapacity;
            nodes.Clear();
            cumulativeDistances.Clear();
            if (pathRoot != null)
            {
                for (int i = 0; i < pathRoot.childCount; i++)
                {
                    var n = pathRoot.GetChild(i).GetComponent<ConveyorPathNode>();
                    if (n != null) nodes.Add(n);
                }
            }
            float accum = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i > 0) accum += Vector3.Distance(nodes[i - 1].Position, nodes[i].Position);
                cumulativeDistances.Add(accum);
            }

            ridingShooters.Clear();
            reservedCount = 0;
            lastReservationLandTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Reserves a slot atomically. All shooters land at path progress 0 (entry);
        /// boarding duration is stretched so the new shooter lands at least safeSpacing
        /// world units behind the previous one.
        /// </summary>
        public bool TryReserveSlot(out float boardingDuration, out float landingProgress)
        {
            boardingDuration = baseBoardingDuration;
            landingProgress = 0f;

            if (FreeCount <= 0) return false;

            float now = Time.time;
            float earliestSafeLandTime = lastReservationLandTime + safeSpacing / Mathf.Max(0.0001f, pathSpeed);
            float ourLandTime = Mathf.Max(now + baseBoardingDuration, earliestSafeLandTime);
            boardingDuration = ourLandTime - now;

            lastReservationLandTime = ourLandTime;
            reservedCount++;
            return true;
        }

        public void RegisterRider(Shooter shooter, float landingProgress)
        {
            reservedCount = Mathf.Max(0, reservedCount - 1);
            ridingShooters.Add(shooter);
            shooter.SetPathProgress(landingProgress);
            shooter.StartFollowingPath(pathSpeed);
            shooter.OnExpired += HandleRiderExpired;
        }

        private void HandleRiderExpired(Shooter s)
        {
            s.OnExpired -= HandleRiderExpired;
            ridingShooters.Remove(s);
        }

        /// <summary>Frees a conveyor slot without expiring the shooter (e.g., when sending it to reserve at path end).</summary>
        public void RemoveRider(Shooter s)
        {
            if (s == null) return;
            s.OnExpired -= HandleRiderExpired;
            ridingShooters.Remove(s);
        }

        public void CancelReservation()
        {
            reservedCount = Mathf.Max(0, reservedCount - 1);
        }

        /// <summary>Sample the path at a distance from start in world units. Beyond max → returns end.</summary>
        public void EvaluatePath(float distance, out Vector3 worldPos, out bool canShoot, out GridSide side)
        {
            worldPos = transform.position;
            canShoot = false;
            side = GridSide.Bottom;
            if (nodes.Count == 0) return;
            if (nodes.Count == 1)
            {
                worldPos = nodes[0].Position;
                canShoot = nodes[0].IsCanShoot;
                side = nodes[0].TargetSide;
                return;
            }

            distance = Mathf.Clamp(distance, 0f, MaxPathProgress);

            // Binary/linear search for the segment containing this distance
            int i = 0;
            for (int k = 1; k < cumulativeDistances.Count; k++)
            {
                if (cumulativeDistances[k] >= distance) { i = k - 1; break; }
                i = k;
            }
            if (i >= nodes.Count - 1)
            {
                worldPos = nodes[nodes.Count - 1].Position;
                canShoot = nodes[nodes.Count - 1].IsCanShoot;
                side = nodes[nodes.Count - 1].TargetSide;
                return;
            }

            float segLen = cumulativeDistances[i + 1] - cumulativeDistances[i];
            float t = segLen > 0.0001f ? (distance - cumulativeDistances[i]) / segLen : 0f;
            worldPos = Vector3.Lerp(nodes[i].Position, nodes[i + 1].Position, t);

            canShoot = nodes[i].IsCanShoot;
            side = nodes[i].TargetSide;
        }
    }
}
