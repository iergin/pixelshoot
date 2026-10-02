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
        [Tooltip("Height of the curved entry at each end so the rope plugs into the bus (near-)perpendicular. 0 = straight (no curve).")]
        [SerializeField] private float perpRise = 0.4f;
        [Tooltip("Particles forming the smooth entry arc at EACH end. More = smoother, rounder bend (needs enough blueprint particles).")]
        [SerializeField, Min(2)] private int entrySmoothness = 4;
        [Tooltip("Looseness of the middle span: rope rest length × this. 1 = taut, >1 = looser/saggier. Needs solver gravity on. Only affects the free middle, not the pinned ends.")]
        [SerializeField, Min(1f)] private float slack = 1.4f;
        [Tooltip("Two-tone: the rope shows bus A's colour on its half and bus B's on the other. This is the blend band around the middle — 0 = hard split, larger = smoother gradient. Needs a vertex-colour rope material.")]
        [SerializeField, Range(0f, 0.5f)] private float colorBlendBand = 0.15f;

        private class Rope { public Shooter a, b; public GameObject go; }
        private readonly List<Rope> ropes = new List<Rope>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Rebuild every rope with the CURRENT inspector values — press this after
        /// tweaking yOffset / perpRise / entrySmoothness to see the change live (in Play mode).
        /// Right-click the component header (or the ⋮ menu) → "Rebuild Link Ropes".</summary>
        [ContextMenu("Rebuild Link Ropes")]
        public void RebuildAll()
        {
            var pairs = new List<(Shooter a, Shooter b)>();
            foreach (var r in ropes)
            {
                if (r.a != null && r.b != null) pairs.Add((r.a, r.b));
                if (r.go != null) Destroy(r.go);
            }
            ropes.Clear();
            foreach (var p in pairs) CreateRope(p.a, p.b);
        }

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
            while (guard++ < 300)
            {
                if (rope == null || a == null || b == null) yield break; // rope may be destroyed by a rebuild
                if (rope.solver != null && rope.activeParticleCount > 0) break;
                yield return null;
            }
            if (rope == null || a == null || b == null) yield break;

            int n = rope.activeParticleCount;
            Vector3 baseA = a.transform.position + Vector3.up * yOffset;
            Vector3 baseB = b.transform.position + Vector3.up * yOffset;

            // Reset the end groups to a single particle so re-tuning (rebuild) starts clean.
            ResetGroup(atts[0].particleGroup, 0);
            ResetGroup(atts[1].particleGroup, n - 1);

            int k = Mathf.Clamp(entrySmoothness, 2, Mathf.Max(2, (n - 1) / 2)); // arc particles per end
            if (n >= 2 * k + 1 && perpRise > 0.001f)
            {
                // Pin a small quarter-arc at each end: the rope leaves the bus (near-)vertical
                // and curves SMOOTHLY into the span — steep but no sharp kink. The pinned arc
                // particles rotate with the bus.
                Vector3 toB = baseB - baseA; toB.y = 0f;
                Vector3 dirB = toB.sqrMagnitude > 1e-6f ? toB.normalized : Vector3.forward;
                Vector3 dirA = -dirB;
                float r = perpRise;

                for (int j = 0; j < k; j++)
                {
                    float th = (Mathf.PI * 0.5f) * (j / (float)(k - 1)); // 0=vertical → 90°=horizontal
                    Vector3 aPos = baseA + Vector3.up * (r * Mathf.Sin(th)) + dirB * (r * (1f - Mathf.Cos(th)));
                    Vector3 bPos = baseB + Vector3.up * (r * Mathf.Sin(th)) + dirA * (r * (1f - Mathf.Cos(th)));
                    rope.TeleportParticle(j, aPos);
                    rope.TeleportParticle(n - 1 - j, bPos);
                    AddToGroup(atts[0].particleGroup, j);
                    AddToGroup(atts[1].particleGroup, n - 1 - j);
                }

                Vector3 arcTopA = baseA + Vector3.up * r + dirB * r; // horizontal tangent — matches the span
                Vector3 arcTopB = baseB + Vector3.up * r + dirA * r;
                int midCount = n - 2 * k;
                for (int m = 0; m < midCount; m++)
                {
                    float t = midCount > 1 ? (m + 1) / (float)(midCount + 1) : 0.5f;
                    rope.TeleportParticle(k + m, Vector3.Lerp(arcTopA, arcTopB, t));
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                    rope.TeleportParticle(i, Vector3.Lerp(baseA, baseB, n > 1 ? i / (float)(n - 1) : 0.5f));
            }

            // Looser middle: longer rest length → the free span sags (pinned ends unaffected).
            rope.stretchingScale = slack;

            atts[0].target = a.transform; // binds capturing the stub/offset
            atts[1].target = b.transform;

            // Two-tone: paint each particle from bus A's colour to bus B's colour along the rope.
            ApplyRopeColors(rope, n, a, b);
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

        // Set per-particle colours (bus A → bus B). Obi's extruded renderer bakes these into the
        // mesh vertex colours, so a vertex-colour material shows the two-tone rope.
        private void ApplyRopeColors(ObiRope rope, int n, Shooter a, Shooter b)
        {
            var s = rope.solver;
            if (s == null) return;
            Color colA = RenderColor(ColorOf(a)), colB = RenderColor(ColorOf(b));
            float lo = 0.5f - colorBlendBand, hi = 0.5f + colorBlendBand;

            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 0.5f;
                float k = (hi > lo) ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo, hi, t))
                                    : (t < 0.5f ? 0f : 1f); // 0 band → hard split
                int si = rope.solverIndices[i];
                s.colors[si] = Color.Lerp(colA, colB, k);
            }
        }

        // The EXACT colour the bus renders with (its ShooterMaterial's base colour), or grey for
        // a still-hidden surprise (don't leak its colour).
        private static Color ColorOf(Shooter s)
        {
            if (s == null || s.IsSurprise || s.Color == null) return Color.gray;
            var cd = s.Color;
            var m = cd.ShooterMaterial;
            if (m != null)
            {
                if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
                if (m.HasProperty("_Color"))     return m.GetColor("_Color");
            }
            return cd.DisplayColor;
        }

        // Unlit vertex colours are passed to the shader AS-IS (Unity gamma→linear-converts material
        // colours but not vertex colours), so in a Linear project convert here to match the bus.
        private static Color RenderColor(Color c) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        private static void AddToGroup(ObiParticleGroup group, int index)
        {
            if (group != null && !group.particleIndices.Contains(index))
                group.particleIndices.Add(index);
        }

        private static void ResetGroup(ObiParticleGroup group, int endIndex)
        {
            if (group == null) return;
            group.particleIndices.Clear();
            group.particleIndices.Add(endIndex);
        }
    }
}
