using System;
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
        [Tooltip("Extra points for the endgame loop's end→start bridge so it curves (oval) like the other corners instead of a straight line. Order them from NEAR the last path node toward the first. Only used in LoopMode.")]
        [SerializeField] private Transform[] loopBridgePoints;

        // Progress is expressed in world distance along the polyline.
        private readonly List<ConveyorPathNode> nodes = new List<ConveyorPathNode>();
        private readonly List<float> cumulativeDistances = new List<float>();
        // Closing bridge polyline: [lastNode, bridge points…, firstNode] + its cumulative distances.
        private readonly List<Vector3> bridgePts = new List<Vector3>();
        private readonly List<float> bridgeCumulative = new List<float>();
        private readonly List<Shooter> ridingShooters = new List<Shooter>();
        private int capacity;
        private int reservedCount;
        private float lastReservationLandTime = float.NegativeInfinity;

        public int Capacity => capacity;
        /// <summary>Fired whenever the capacity changes (e.g. the conveyor-capacity booster).</summary>
        public event Action OnCapacityChanged;
        /// <summary>Booster hook: permanently add conveyor slots this level.</summary>
        public void AddCapacity(int n)
        {
            capacity = Mathf.Max(0, capacity + n);
            OnCapacityChanged?.Invoke();
        }
        public int FreeCount => capacity - (ridingShooters.Count + reservedCount);
        public int OccupiedCount => ridingShooters.Count + reservedCount;
        public bool IsFull => FreeCount <= 0 && capacity > 0;

        /// <summary>Fired when a slot reservation is attempted while the conveyor is full
        /// (i.e. the player clicked a shooter that can't board). Drives the HUD shake.</summary>
        public event Action OnFullSlotAttempt;
        public float MaxPathProgress => cumulativeDistances.Count > 0 ? cumulativeDistances[cumulativeDistances.Count - 1] : 0f;
        public Vector3 EntryWorldPosition => nodes.Count > 0 ? nodes[0].Position : transform.position;

        /// <summary>When true, every Shooter on the belt freezes in place (used after a level fail).</summary>
        public bool IsPaused { get; set; }

        /// <summary>Global belt-speed multiplier applied to every rider (1 = normal). The
        /// endgame "last N buses" mode cranks this up so the conveyor whips around.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>When true, riders that still have shots WRAP seamlessly at the path end
        /// (continue from the start) instead of leaving to reserve — the belt behaves like a
        /// continuous loop. Enabled by the endgame "last N buses" mode.</summary>
        public bool LoopMode { get; set; }

        // Distance of the closing "bridge" segment from the last node back to the first, so a
        // looping bus travels end→start instead of snapping. LoopPathLength = polyline + bridge.
        private float closingLength;
        public float LoopPathLength => MaxPathProgress + closingLength;

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
            BuildBridge();

            ridingShooters.Clear();
            reservedCount = 0;
            lastReservationLandTime = float.NegativeInfinity;
            IsPaused = false;
            SpeedMultiplier = 1f;
            LoopMode = false;
        }

        public bool TryReserveSlot(out float boardingDuration, out float landingProgress)
        {
            boardingDuration = baseBoardingDuration;
            landingProgress = 0f;

            if (FreeCount <= 0) { OnFullSlotAttempt?.Invoke(); return false; }

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

        /// <summary>Copy of every shooter currently riding the conveyor (state OnConveyor or Boarding-to-conveyor).</summary>
        public List<Shooter> GetRidersSnapshot()
        {
            return new List<Shooter>(ridingShooters);
        }

        public void CancelReservation()
        {
            reservedCount = Mathf.Max(0, reservedCount - 1);
        }

        // Build the closing-bridge polyline: last node → bridge points → first node.
        private void BuildBridge()
        {
            bridgePts.Clear();
            bridgeCumulative.Clear();
            closingLength = 0f;
            if (nodes.Count < 2) return;

            bridgePts.Add(nodes[nodes.Count - 1].Position);
            if (loopBridgePoints != null)
                foreach (var t in loopBridgePoints)
                    if (t != null) bridgePts.Add(t.position);
            bridgePts.Add(nodes[0].Position);

            float acc = 0f;
            bridgeCumulative.Add(0f);
            for (int i = 1; i < bridgePts.Count; i++)
            {
                acc += Vector3.Distance(bridgePts[i - 1], bridgePts[i]);
                bridgeCumulative.Add(acc);
            }
            closingLength = acc;
        }

        // Position at `local` world units along the bridge (0 = last node, closingLength = first node).
        private Vector3 EvaluateBridge(float local)
        {
            if (bridgePts.Count == 0) return transform.position;
            if (bridgePts.Count == 1) return bridgePts[0];
            local = Mathf.Clamp(local, 0f, closingLength);

            int i = 0;
            for (int k = 1; k < bridgeCumulative.Count; k++)
            {
                if (bridgeCumulative[k] >= local) { i = k - 1; break; }
                i = k;
            }
            if (i >= bridgePts.Count - 1) return bridgePts[bridgePts.Count - 1];

            float segLen = bridgeCumulative[i + 1] - bridgeCumulative[i];
            float t = segLen > 0.0001f ? (local - bridgeCumulative[i]) / segLen : 0f;
            return Vector3.Lerp(bridgePts[i], bridgePts[i + 1], t);
        }

        /// <summary>Sample the path at a distance from start in world units. Linear between nodes.</summary>
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

            // Closing bridge: distances past the polyline walk the bridge polyline
            // (last node → optional bridge points → first node). Only reached in LoopMode.
            if (distance > MaxPathProgress)
            {
                worldPos = EvaluateBridge(distance - MaxPathProgress);
                canShoot = false;          // the bridge is a travel-only transition
                side = nodes[nodes.Count - 1].TargetSide;
                return;
            }

            distance = Mathf.Clamp(distance, 0f, MaxPathProgress);

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

            var a = nodes[i];
            var b = nodes[i + 1];
            // Segment is "shoot" only if BOTH endpoints are canShoot with the same side.
            canShoot = a.IsCanShoot && b.IsCanShoot && a.TargetSide == b.TargetSide;
            side = a.TargetSide;
            worldPos = Vector3.Lerp(a.Position, b.Position, t);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (pathRoot == null) return;
            ConveyorPathNode prev = null;
            for (int i = 0; i < pathRoot.childCount; i++)
            {
                var node = pathRoot.GetChild(i).GetComponent<ConveyorPathNode>();
                if (node == null) continue;
                if (prev != null)
                {
                    bool segShoot = prev.IsCanShoot && node.IsCanShoot && prev.TargetSide == node.TargetSide;
                    Gizmos.color = segShoot
                        ? new Color(0.2f, 0.9f, 0.3f, 1f)
                        : new Color(0.55f, 0.55f, 0.55f, 1f);
                    Gizmos.DrawLine(prev.Position, node.Position);

                    Vector3 mid = Vector3.Lerp(prev.Position, node.Position, 0.5f);
                    Vector3 dir = (node.Position - prev.Position).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * 0.12f;
                    Gizmos.DrawLine(mid, mid - dir * 0.25f + right);
                    Gizmos.DrawLine(mid, mid - dir * 0.25f - right);
                }
                prev = node;
            }

            // Draw the endgame loop bridge (last node → bridge points → first node) so it can
            // be shaped in the editor. Dashed-looking cyan to distinguish it from the belt.
            ConveyorPathNode firstNode = null, lastNode = null;
            for (int i = 0; i < pathRoot.childCount; i++)
            {
                var n = pathRoot.GetChild(i).GetComponent<ConveyorPathNode>();
                if (n == null) continue;
                if (firstNode == null) firstNode = n;
                lastNode = n;
            }
            if (firstNode != null && lastNode != null && firstNode != lastNode)
            {
                var pts = new List<Vector3> { lastNode.Position };
                if (loopBridgePoints != null)
                    foreach (var t in loopBridgePoints) if (t != null) pts.Add(t.position);
                pts.Add(firstNode.Position);

                Gizmos.color = new Color(0.2f, 0.85f, 0.95f, 1f);
                for (int i = 1; i < pts.Count; i++) Gizmos.DrawLine(pts[i - 1], pts[i]);
                if (loopBridgePoints != null)
                    foreach (var t in loopBridgePoints) if (t != null) Gizmos.DrawWireSphere(t.position, 0.12f);
            }
        }
#endif
    }
}
