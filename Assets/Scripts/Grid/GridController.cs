using System;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Grid
{
    public class GridController : MonoBehaviour
    {
        [SerializeField] private Box boxPrefab;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private float cellSize = 1f;
        private Box[,] boxes;
        private int size;
        private int aliveCount;
        private Material lockedFallback;
        private Material unhitFallback;

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
                box.Initialize(cell.GridX, cell.GridZ, cell.Color, locked, unhit, cell.Tone);
                boxes[cell.GridX, cell.GridZ] = box;
                aliveCount++;
            }

            ComputeInitialFrontier();
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
                    if (IsOnSilhouette(x, z)) b.SetState(BoxState.Frontier);
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

        public void NotifyBoxHit(Box b)
        {
            if (b == null || !b.IsAlive) return;
            b.TakeHit();
            aliveCount--;

            // Promote any locked 4-neighbors to frontier — wave-front expansion.
            foreach (var n in Neighbors4)
            {
                int nx = b.GridX + n.dx, nz = b.GridZ + n.dz;
                if (nx < 0 || nx >= size || nz < 0 || nz >= size) continue;
                var nb = boxes[nx, nz];
                if (nb != null && nb.State == BoxState.Locked) nb.SetState(BoxState.Frontier);
            }

            if (aliveCount <= 0) OnGridCleared?.Invoke();
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
