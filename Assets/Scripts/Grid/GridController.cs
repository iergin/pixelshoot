using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Shooters;

namespace PixelShoot.Grid
{
    public class GridController : MonoBehaviour
    {
        [SerializeField] private Box boxPrefab;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private float cellSize = 1f;
        [Header("Bombs")]
        [Tooltip("Half-extent of the bomb explosion in cells. 2 → 5×5 footprint centred on the bomb.")]
        [SerializeField, Min(1)] private int bombRadius = 2;
        [Tooltip("Seconds between the bomb being shot and its affected cells actually opening. " +
                 "The cells are reserved (untargetable) immediately so other shooters can't double-fire.")]
        [SerializeField, Min(0f)] private float bombOpenDelay = 0.5f;
        [Header("Keys")]
        [Tooltip("Prefab (with a KeyVisual component) spawned at the centroid of each key-group's cells.")]
        [SerializeField] private PixelShoot.Game.KeyVisual keyVisualPrefab;
        [Tooltip("Extra height above the grid plane for the floating key visual.")]
        [SerializeField] private float keyVisualHeight = 0.6f;
        private Box[,] boxes;
        private int size;
        private int aliveCount;
        private Material lockedFallback;
        private Material unhitFallback;
        private readonly List<GameObject> keyVisuals = new List<GameObject>();

        private static readonly (int dx, int dz)[] Neighbors4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public event Action OnGridCleared;
        public int Size => size;
        public int AliveCount => aliveCount;
        public float CellSize => cellSize;
        public Transform GridRoot => gridRoot;

        public void Build(GridData data)
        {
            Clear();

            size = data.Size;
            if (gridRoot != null)
            {
                gridRoot.localPosition = data.RootPosition;
                gridRoot.localScale = data.RootScale;
            }
            boxes = new Box[size, size];

            var locked = GetLockedMaterial();
            var unhit = GetUnhitMaterial();
            foreach (var cell in data.Cells)
            {
                if (cell.IsEmpty) continue;
                if (cell.GridX < 0 || cell.GridX >= size || cell.GridZ < 0 || cell.GridZ >= size) continue;

                var pos = GetCellLocalPosition(cell.GridX, cell.GridZ);
                var box = Instantiate(boxPrefab, gridRoot != null ? gridRoot : transform);
                box.transform.localPosition = pos;
                box.Initialize(cell.GridX, cell.GridZ, cell.Color, locked, unhit, cell.Tone, cell.IsBomb, cell.KeyId);
                boxes[cell.GridX, cell.GridZ] = box;
                aliveCount++;
            }

            // Keys must exist BEFORE the initial frontier is computed — a key cell that
            // starts on the silhouette collects its key right away.
            SpawnKeyVisuals(data);
            ComputeInitialFrontier();
        }

        /// <summary>Spawns one floating key per key-group at the centroid of its cells.</summary>
        private void SpawnKeyVisuals(GridData data)
        {
            if (keyVisualPrefab == null) return;

            // centroid accumulation per key id
            var sum = new Dictionary<int, Vector3>();
            var count = new Dictionary<int, int>();
            foreach (var cell in data.Cells)
            {
                if (cell.IsEmpty || cell.KeyId <= 0) continue;
                Vector3 p = GetCellWorldPosition(cell.GridX, cell.GridZ);
                if (sum.ContainsKey(cell.KeyId)) { sum[cell.KeyId] += p; count[cell.KeyId]++; }
                else { sum[cell.KeyId] = p; count[cell.KeyId] = 1; }
            }

            foreach (var kv in sum)
            {
                Vector3 centroid = kv.Value / count[kv.Key];
                centroid.y += keyVisualHeight;
                var vis = Instantiate(keyVisualPrefab, centroid, Quaternion.identity, gridRoot != null ? gridRoot : transform);
                vis.Init(kv.Key);
                keyVisuals.Add(vis.gameObject);
            }
        }

        /// <summary>Set a box to Frontier and, if it carries a key, bank that key.</summary>
        private void PromoteToFrontier(Box b)
        {
            if (b == null) return;
            b.SetState(BoxState.Frontier);
            if (b.KeyId > 0) PixelShoot.Game.KeyManager.Instance?.Collect(b.KeyId);
        }

        // Any box adjacent to a grid edge OR to an empty cell starts on the frontier.
        // Boxes fully surrounded by other boxes start locked.
        private void ComputeInitialFrontier()
        {
            if (boxes == null) return;
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    var b = boxes[x, z];
                    if (b == null) continue;
                    if (IsOnSilhouette(x, z)) PromoteToFrontier(b);
                }
            }
        }

        private bool IsOnSilhouette(int x, int z)
        {
            foreach (var n in Neighbors4)
            {
                int nx = x + n.dx, nz = z + n.dz;
                if (nx < 0 || nx >= size || nz < 0 || nz >= size) return true; // grid edge
                if (boxes[nx, nz] == null) return true;                        // empty cell neighbor
            }
            return false;
        }

        private Material GetLockedMaterial()
        {
            if (lockedFallback == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                lockedFallback = new Material(shader) { color = new Color(0.42f, 0.42f, 0.46f) };
                if (lockedFallback.HasProperty("_BaseColor"))
                    lockedFallback.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.46f));
            }
            return lockedFallback;
        }

        private Material GetUnhitMaterial()
        {
            if (unhitFallback == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var c = new Color(0.82f, 0.82f, 0.84f);
                unhitFallback = new Material(shader) { color = c };
                if (unhitFallback.HasProperty("_BaseColor"))
                    unhitFallback.SetColor("_BaseColor", c);
            }
            return unhitFallback;
        }

        public void Clear()
        {
            if (boxes != null)
            {
                foreach (var b in boxes)
                    if (b != null) SafeDestroy(b.gameObject);
            }
            boxes = null;
            aliveCount = 0;
            foreach (var kv in keyVisuals)
                if (kv != null) SafeDestroy(kv);
            keyVisuals.Clear();
        }

        /// <summary>Destroy that works in both play mode (Destroy) and edit mode
        /// (DestroyImmediate) — the level editor preview drives Clear() at edit time.</summary>
        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        public Vector3 GetCellLocalPosition(int x, int z)
        {
            // Center the grid on origin: cell at (0,0) is at offset -((size-1)/2)*cellSize
            float offset = (size - 1) * 0.5f * cellSize;
            return new Vector3(x * cellSize - offset, 0f, z * cellSize - offset);
        }

        public Vector3 GetCellWorldPosition(int x, int z)
        {
            var local = GetCellLocalPosition(x, z);
            return gridRoot != null ? gridRoot.TransformPoint(local) : transform.TransformPoint(local);
        }

        /// <summary>Grid-local → world (respects the grid root's transform).</summary>
        public Vector3 GridLocalToWorld(Vector3 local) =>
            gridRoot != null ? gridRoot.TransformPoint(local) : transform.TransformPoint(local);

        /// <summary>World → grid-local (respects the grid root's transform).</summary>
        public Vector3 WorldToGridLocal(Vector3 world) =>
            gridRoot != null ? gridRoot.InverseTransformPoint(world) : transform.InverseTransformPoint(world);

        // ── Booster helpers (FillColor) ──────────────────────────────────────
        /// <summary>Box at a grid cell, or null if out of range / empty.</summary>
        public Box GetBox(int x, int z)
        {
            if (boxes == null || x < 0 || z < 0 || x >= size || z >= size) return null;
            return boxes[x, z];
        }

        /// <summary>All still-alive boxes whose gameplay color matches (tone variants resolved).</summary>
        public List<Box> CollectAliveBoxes(ColorData gameplayColor)
        {
            var result = new List<Box>();
            if (boxes == null || gameplayColor == null) return result;
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    var b = boxes[x, z];
                    if (b != null && b.IsAlive && b.Color != null && b.Color.GameplayColor == gameplayColor)
                        result.Add(b);
                }
            return result;
        }

        /// <summary>Intersect a ray with the grid plane. Returns the world point on the plane.</summary>
        public bool RaycastGridPlane(Ray ray, out Vector3 world)
        {
            var root = gridRoot != null ? gridRoot : transform;
            var plane = new Plane(root.up, root.position);
            if (plane.Raycast(ray, out float enter)) { world = ray.GetPoint(enter); return true; }
            world = default;
            return false;
        }

        /// <summary>Map a camera ray to the box under it (via the grid plane). Null if off-grid.</summary>
        public Box PickBox(Ray ray)
        {
            if (!RaycastGridPlane(ray, out Vector3 world)) return null;
            var root = gridRoot != null ? gridRoot : transform;
            Vector3 local = root.InverseTransformPoint(world);
            float offset = (size - 1) * 0.5f * cellSize;
            int x = Mathf.RoundToInt((local.x + offset) / cellSize);
            int z = Mathf.RoundToInt((local.z + offset) / cellSize);
            return GetBox(x, z);
        }

        /// <summary>
        /// Find the outermost targetable box from the given side, at the line of fire
        /// closest to the shooter's world position.
        /// </summary>
        public Box FindTarget(GridSide side, Vector3 shooterWorldPos, ColorData requiredColor)
        {
            if (boxes == null) return null;
            int parallelIndex = GetParallelIndex(side, shooterWorldPos);
            if (parallelIndex < 0 || parallelIndex >= size) return null;

            Box outer = FindOutermostAlive(side, parallelIndex);
            // Only return it if it matches the shooter's color and isn't already
            // reserved by another bullet. Shooters cannot shoot past mismatched
            // outer cells — those have to be cleared by a same-color shooter first.
            return IsValidTarget(outer, requiredColor) ? outer : null;
        }

        /// <summary>
        /// Returns the column index (Bottom/Top) or row index (Left/Right) the
        /// shooter currently aligns with. Out-of-range = -1.
        /// </summary>
        public int GetParallelIndex(GridSide side, Vector3 shooterWorldPos)
        {
            var localPos = (gridRoot != null ? gridRoot : transform).InverseTransformPoint(shooterWorldPos);
            float offset = (size - 1) * 0.5f * cellSize;
            int idx = (side == GridSide.Bottom || side == GridSide.Top)
                ? Mathf.RoundToInt((localPos.x + offset) / cellSize)
                : Mathf.RoundToInt((localPos.z + offset) / cellSize);
            return (idx < 0 || idx >= size) ? -1 : idx;
        }

        private Box FindOutermostAlive(GridSide side, int parallelIndex)
        {
            switch (side)
            {
                case GridSide.Bottom:
                    for (int z = 0; z < size; z++)
                    {
                        var b = boxes[parallelIndex, z];
                        if (b != null && b.IsAlive) return b;
                    }
                    break;
                case GridSide.Top:
                    for (int z = size - 1; z >= 0; z--)
                    {
                        var b = boxes[parallelIndex, z];
                        if (b != null && b.IsAlive) return b;
                    }
                    break;
                case GridSide.Left:
                    for (int x = 0; x < size; x++)
                    {
                        var b = boxes[x, parallelIndex];
                        if (b != null && b.IsAlive) return b;
                    }
                    break;
                case GridSide.Right:
                    for (int x = size - 1; x >= 0; x--)
                    {
                        var b = boxes[x, parallelIndex];
                        if (b != null && b.IsAlive) return b;
                    }
                    break;
            }
            return null;
        }

        private bool IsValidTarget(Box b, ColorData requiredColor)
        {
            if (b == null) return false;
            // Only frontier boxes that haven't already been promised to another bullet are valid targets.
            if (!b.IsShootable) return false;
            // Match through the main color so any tone variant of the same color group is valid.
            if (requiredColor != null && b.Color != null
                && b.Color.GameplayColor != requiredColor.GameplayColor) return false;
            return true;
        }

        public void NotifyBoxHit(Box b, bool fromBomb = false)
        {
            if (b == null || !b.IsAlive) return;
            bool wasBomb = b.IsBomb;
            int bombX = b.GridX, bombZ = b.GridZ;
            var bombParticle = b.ExplosionParticlePrefab;
            Vector3 bombWorldPos = b.transform.position;

            b.TakeHit(fromBomb);
            aliveCount--;

            // Promote any locked 4-neighbors to frontier — wave-front expansion.
            foreach (var n in Neighbors4)
            {
                int nx = b.GridX + n.dx, nz = b.GridZ + n.dz;
                if (nx < 0 || nx >= size || nz < 0 || nz >= size) continue;
                var nb = boxes[nx, nz];
                if (nb != null && nb.State == BoxState.Locked) PromoteToFrontier(nb);
            }

            // Bomb side effect — runs AFTER the bomb cell itself is processed so the
            // bomb's neighbours are already promoted before we walk the 5x5.
            if (wasBomb) TriggerBombExplosion(bombX, bombZ, bombParticle, bombWorldPos);

            if (aliveCount <= 0) OnGridCleared?.Invoke();
        }

        /// <summary>
        /// Reserves every alive cell in the <see cref="bombRadius"/>-half-extent square
        /// around (cx, cz) so other shooters can't redundantly target them, computes the
        /// shot debt to repay each colour, spawns the explosion particle, and schedules
        /// the hits to ripple outward ring by ring: ring 1 (the 3×3 shell) opens after
        /// one <see cref="bombOpenDelay"/>, ring 2 (the 5×5 shell) one delay later, etc.
        /// </summary>
        private void TriggerBombExplosion(int cx, int cz, GameObject particlePrefab, Vector3 worldPos)
        {
            // Particle FX immediately at the bomb's last-known position.
            if (particlePrefab != null)
            {
                var fx = Instantiate(particlePrefab, worldPos, Quaternion.identity);
                Destroy(fx, 4f);
            }

            // Group affected cells by their ring (Chebyshev distance from the bomb):
            // ringsAffected[0] = distance 1 shell, ringsAffected[1] = distance 2 shell, ...
            var ringsAffected = new List<Box>[bombRadius];
            for (int r = 0; r < bombRadius; r++) ringsAffected[r] = new List<Box>();
            var consumeByColor = new Dictionary<ColorData, int>();

            for (int dx = -bombRadius; dx <= bombRadius; dx++)
            {
                for (int dz = -bombRadius; dz <= bombRadius; dz++)
                {
                    if (dx == 0 && dz == 0) continue;          // bomb cell already processed
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || x >= size || z < 0 || z >= size) continue;
                    var nb = boxes[x, z];
                    if (nb == null || !nb.IsAlive) continue;
                    if (nb.IsReserved) continue;               // someone else already promised this one

                    nb.ReserveHit();                           // lock immediately — guards against double-fire
                    int ring = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)); // 1..bombRadius
                    ringsAffected[ring - 1].Add(nb);

                    var key = nb.Color != null ? nb.Color.GameplayColor : null;
                    if (key != null)
                    {
                        if (!consumeByColor.ContainsKey(key)) consumeByColor[key] = 0;
                        consumeByColor[key]++;
                    }
                }
            }

            // Pay back the shooters NOW so the rest of the world (HUD, validation, etc.)
            // sees the deduction even before the delayed visual hit.
            foreach (var kvp in consumeByColor)
                ShooterColumn.ConsumeShotsForGameplayColor(kvp.Key, kvp.Value);

            StartCoroutine(BombHitSequence(ringsAffected));
        }

        private IEnumerator BombHitSequence(List<Box>[] rings)
        {
            // Each ring waits one bombOpenDelay after the previous: the explosion
            // ripples outward instead of opening the whole 5×5 at once.
            foreach (var ring in rings)
            {
                if (bombOpenDelay > 0f) yield return new WaitForSeconds(bombOpenDelay);
                foreach (var b in ring)
                {
                    if (b == null || !b.IsAlive) continue;
                    // NotifyBoxHit handles aliveCount, frontier promotion, and any chained
                    // bomb on the affected cells (chain explosion). The reservation already
                    // set on neighbour bombs prevents them from being double-processed.
                    // fromBomb:true → these play the bomb-open clip.
                    NotifyBoxHit(b, fromBomb: true);
                }
            }
        }

#if UNITY_EDITOR
        [SerializeField] private int gizmoPreviewSize = 7;

        private void OnDrawGizmos()
        {
            // At runtime, draw the actual built grid; at edit time, draw a preview
            // using gizmoPreviewSize so we can place the conveyor around it.
            int s = (boxes != null) ? size : gizmoPreviewSize;
            if (s <= 0) return;
            var root = gridRoot != null ? gridRoot : transform;

            float off = (s - 1) * 0.5f * cellSize;
            // Outer bounds
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.8f);
            Vector3 c00 = root.TransformPoint(new Vector3(-off - cellSize * 0.5f, 0f, -off - cellSize * 0.5f));
            Vector3 cN0 = root.TransformPoint(new Vector3( off + cellSize * 0.5f, 0f, -off - cellSize * 0.5f));
            Vector3 cNN = root.TransformPoint(new Vector3( off + cellSize * 0.5f, 0f,  off + cellSize * 0.5f));
            Vector3 c0N = root.TransformPoint(new Vector3(-off - cellSize * 0.5f, 0f,  off + cellSize * 0.5f));
            Gizmos.DrawLine(c00, cN0);
            Gizmos.DrawLine(cN0, cNN);
            Gizmos.DrawLine(cNN, c0N);
            Gizmos.DrawLine(c0N, c00);

            // Cell grid
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.25f);
            for (int i = 0; i <= s; i++)
            {
                float v = -off - cellSize * 0.5f + i * cellSize;
                Gizmos.DrawLine(
                    root.TransformPoint(new Vector3(v, 0f, -off - cellSize * 0.5f)),
                    root.TransformPoint(new Vector3(v, 0f,  off + cellSize * 0.5f)));
                Gizmos.DrawLine(
                    root.TransformPoint(new Vector3(-off - cellSize * 0.5f, 0f, v)),
                    root.TransformPoint(new Vector3( off + cellSize * 0.5f, 0f, v)));
            }
        }
#endif

        /// <summary>Check if a shooter at given world pos is roughly aligned (within tolerance) with target box's perpendicular axis.</summary>
        public bool IsAlignedWith(Box target, GridSide side, Vector3 shooterWorldPos, float tolerance)
        {
            if (target == null) return false;
            var targetWorld = GetCellWorldPosition(target.GridX, target.GridZ);
            switch (side)
            {
                case GridSide.Bottom:
                case GridSide.Top:
                    return Mathf.Abs(targetWorld.x - shooterWorldPos.x) <= tolerance;
                default:
                    return Mathf.Abs(targetWorld.z - shooterWorldPos.z) <= tolerance;
            }
        }
    }
}
