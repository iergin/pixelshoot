using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Obi;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Connects linked buses with Obi ropes. For each link group it spawns one rope PER
    /// ADJACENT PAIR (list order), pinning each rope end (Static attachment) to a bus. The
    /// ropes then follow the buses automatically. Dead ropes (a member expired or unlinked)
    /// are cleaned up each frame. Put this in the scene, assign the LinkRope prefab and the
    /// ObiSolver transform; buses reach it via <see cref="Instance"/>.
    /// </summary>
    public class LinkRopeController : MonoBehaviour
    {
        public static LinkRopeController Instance { get; private set; }

        [Tooltip("The LinkRope prefab (ObiRope + 2 Static ObiParticleAttachments).")]
        [SerializeField] private GameObject ropePrefab;
        [Tooltip("ObiSolver transform — ropes are parented here so they get simulated.")]
        [SerializeField] private Transform solver;
        [Tooltip("Vertical offset of the rope's attach point above each bus (world +Y).")]
        [SerializeField] private float yOffset = 0.4f;
        [Tooltip("Height of the vertical 'stub' at each end so the rope plugs into the bus perpendicular. 0 = straight (no stub).")]
        [SerializeField] private float perpRise = 0.4f;

        private class Rope { public Shooter a, b; public GameObject go; }
        private readonly List<Rope> ropes = new List<Rope>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Spawn ropes for a link group (one per adjacent pair). Replaces any existing
        /// ropes touching these members first, so it's safe to call again.</summary>
        public void BuildForGroup(IReadOnlyList<Shooter> members)
        {
            if (ropePrefab == null || members == null || members.Count < 2) return;
            RemoveRopesTouching(members);
            for (int i = 0; i + 1 < members.Count; i++)
                CreateRope(members[i], members[i + 1]);
        }

        private void CreateRope(Shooter a, Shooter b)
        {
            if (a == null || b == null) return;
            var go = Instantiate(ropePrefab, solver != null ? solver : transform);
            var rope = go.GetComponent<ObiRope>();
            var atts = go.GetComponents<ObiParticleAttachment>();
            if (rope == null || atts.Length < 2)
            {
                Debug.LogWarning("[LinkRopeController] Rope prefab needs an ObiRope + 2 ObiParticleAttachments.");
            }
            else
            {
                StartCoroutine(SetupRope(rope, atts, a, b));
            }
            ropes.Add(new Rope { a = a, b = b, go = go });
        }

        // Wait until the actor is loaded, lay all particles on the A→B line (so the ends sit
        // exactly on the buses), then pin each end (Static) to its bus.
        private IEnumerator SetupRope(ObiRope rope, ObiParticleAttachment[] atts, Shooter a, Shooter b)
        {
            int guard = 0;
            while ((rope.solver == null || rope.activeParticleCount == 0) && guard++ < 300)
                yield return null;
            if (a == null || b == null || rope == null) yield break;

            int n = rope.activeParticleCount;
            Vector3 baseA = a.transform.position + Vector3.up * yOffset;
            Vector3 baseB = b.transform.position + Vector3.up * yOffset;

            if (n >= 4 && perpRise > 0.001f)
            {
                // Rigid 2-particle "stub" at each end: pin the end particle at the bus AND the
                // next one directly above it (add it to the attachment group), so the last
                // segment stays vertical — the rope plugs into the bus perpendicular and the
                // stub rotates with the bus.
                if (atts[0].particleGroup != null && !atts[0].particleGroup.particleIndices.Contains(1))
                    atts[0].particleGroup.particleIndices.Add(1);
                if (atts[1].particleGroup != null && !atts[1].particleGroup.particleIndices.Contains(n - 2))
                    atts[1].particleGroup.particleIndices.Add(n - 2);

                Vector3 topA = baseA + Vector3.up * perpRise;
                Vector3 topB = baseB + Vector3.up * perpRise;
                for (int i = 0; i < n; i++)
                {
                    Vector3 pos;
                    if (i == 0)          pos = baseA; // end pinned at bus A
                    else if (i == 1)     pos = topA;  // riser straight above A
                    else if (i == n - 1) pos = baseB; // end pinned at bus B
                    else if (i == n - 2) pos = topB;  // riser straight above B
                    else                 pos = Vector3.Lerp(topA, topB, (i - 1) / (float)(n - 3));
                    rope.TeleportParticle(i, pos);
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                    rope.TeleportParticle(i, Vector3.Lerp(baseA, baseB, n > 1 ? i / (float)(n - 1) : 0.5f));
            }

            atts[0].target = a.transform; // binds capturing the stub/offset
            atts[1].target = b.transform;
        }

        private void RemoveRopesTouching(IReadOnlyList<Shooter> members)
        {
            for (int i = ropes.Count - 1; i >= 0; i--)
            {
                var r = ropes[i];
                if (members.Contains(r.a) || members.Contains(r.b))
                {
                    if (r.go != null) Destroy(r.go);
                    ropes.RemoveAt(i);
                }
            }
        }

        // Drop ropes whose endpoints died or unlinked (group dissolved).
        private void LateUpdate()
        {
            for (int i = ropes.Count - 1; i >= 0; i--)
            {
                var r = ropes[i];
                if (!Alive(r.a) || !Alive(r.b))
                {
                    if (r.go != null) Destroy(r.go);
                    ropes.RemoveAt(i);
                }
            }
        }

        private static bool Alive(Shooter s) =>
            s != null && s.State != ShooterState.Expired && s.IsLinked;
    }
}
