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

        public event Action OnGridCleared;
        public int Size => size;
        public int AliveCount => aliveCount;
        public float CellSize => cellSize;
        public Transform GridRoot => gridRoot;

        public void Build(GridData data)
        {
            Clear();

            size = data.Size;
            if (gridRoot != null) gridRoot.localScale = data.RootScale;
            boxes = new Box[size, size];

            foreach (var cell in data.Cells)
            {
                if (cell.IsEmpty) continue;
                if (cell.GridX < 0 || cell.GridX >= size || cell.GridZ < 0 || cell.GridZ >= size) continue;

                var pos = GetCellLocalPosition(cell.GridX, cell.GridZ);
                var box = Instantiate(boxPrefab, gridRoot != null ? gridRoot : transform);
                box.transform.localPosition = pos;
                box.Initialize(cell.GridX, cell.GridZ, cell.Color);
                boxes[cell.GridX, cell.GridZ] = box;
                aliveCount++;
            }
        }

        public void Clear()
        {
            if (boxes != null)
            {
                foreach (var b in boxes)
                    if (b != null) Destroy(b.gameObject);
            }
            boxes = null;
            aliveCount = 0;
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
            if (!b.IsTargetable) return false;
            if (requiredColor != null && b.Color != requiredColor) return false;
            return true;
        }

        public void NotifyBoxHit(Box b)
        {
            if (b == null || !b.IsAlive) return;
            b.TakeHit();
            aliveCount--;
            if (aliveCount <= 0) OnGridCleared?.Invoke();
        }

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
