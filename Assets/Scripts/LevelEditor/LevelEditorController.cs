using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.LevelEditor
{
    public enum LevelPreviewMode
    {
        Editing,    // paint mode — every cell shows its vivid unhit material
        Initial,    // game-start simulation — Frontier cells vivid, Locked cells gray
        Completed   // post-game simulation — every cell faded (Hit material)
    }

    /// <summary>
    /// Lives in the LevelEditor scene. Holds the in-progress grid (cells[]) and a
    /// reference to the LevelData asset being authored. Visual cubes are spawned
    /// as children of gridRoot so you can see and edit the level in the scene view.
    /// </summary>
    public class LevelEditorController : MonoBehaviour
    {
        [Header("Target asset")]
        [Tooltip("Drag the LevelData asset you want to edit here. Save writes into it; Load reads from it.")]
        public LevelData targetAsset;

        [Header("Grid")]
        [Min(1)] public int gridSize = 30;
        [Min(0.01f)] public float cellSize = 0.3f;
        public Vector3 gridRootScale = Vector3.one;
        public Transform gridRoot;
        public GameObject cellPrefab;

        [Header("Palette (editor working set — not stored on LevelData)")]
        public List<ColorData> palette = new List<ColorData>();

        [Header("Shooters (synced to/from LevelData on Save/Load)")]
        public List<ColumnData> columns = new List<ColumnData>();
        [Min(1)] public int conveyorSlotCapacity = 5;
        [Min(1)] public int reserveSlotCapacity = 5;

        [Header("Preview")]
        [Tooltip("Editing = paint mode (all painted cells show their vivid unhit material).\n" +
                 "Initial = simulate the game start: silhouette cells show vivid (Frontier), interior cells show gray (Locked).\n" +
                 "Completed = simulate every cell having been shot (all faded Hit materials).")]
        public LevelPreviewMode previewMode = LevelPreviewMode.Editing;
        [Tooltip("Material used for Locked-state cubes in Initial preview. If null, a gray fallback is created at runtime.")]
        public Material lockedPreviewMaterial;

        // Authoring state. Public so the inspector can read/write under Undo.
        [HideInInspector] public int[] cells;
        [HideInInspector] public int currentPaletteIdx = 0;
        [HideInInspector] public bool eraseMode = false;
        private Material lockedFallback;

        public bool HasCells => cells != null && cells.Length == gridSize * gridSize;
        public int CellCount => gridSize * gridSize;

        public void NewGrid()
        {
            cells = new int[CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;
            ClearVisuals();
        }

        public void EnsureCellsArray()
        {
            if (HasCells) return;
            var old = cells;
            int oldSize = (old != null) ? Mathf.RoundToInt(Mathf.Sqrt(old.Length)) : 0;
            cells = new int[CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;
            if (old != null && oldSize > 0)
            {
                int copy = Mathf.Min(oldSize, gridSize);
                for (int z = 0; z < copy; z++)
                    for (int x = 0; x < copy; x++)
                        cells[z * gridSize + x] = old[z * oldSize + x];
            }
        }

        public void Paint(int x, int z)
        {
            EnsureCellsArray();
            if (x < 0 || x >= gridSize || z < 0 || z >= gridSize) return;
            int newColor = eraseMode ? -1 : currentPaletteIdx;
            int idx = z * gridSize + x;
            if (cells[idx] == newColor) return;
            cells[idx] = newColor;
            UpdateVisual(x, z);
        }

        public void Rebuild()
        {
            ClearVisuals();
            if (!HasCells) return;
            for (int z = 0; z < gridSize; z++)
                for (int x = 0; x < gridSize; x++)
                    if (cells[z * gridSize + x] >= 0) UpdateVisual(x, z);
        }

        public void ClearVisuals()
        {
            if (gridRoot == null) return;
            for (int i = gridRoot.childCount - 1; i >= 0; i--)
            {
                var child = gridRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void UpdateVisual(int x, int z)
        {
            if (gridRoot == null || cellPrefab == null) return;
            int flatIdx = z * gridSize + x;
            int colorIdx = cells[flatIdx];

            string name = $"cell_{x}_{z}";
            Transform existing = gridRoot.Find(name);

            if (colorIdx < 0)
            {
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }
                return;
            }

            GameObject go;
            if (existing == null)
            {
#if UNITY_EDITOR
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(cellPrefab, gridRoot);
                if (go == null) go = Instantiate(cellPrefab, gridRoot);
#else
                go = Instantiate(cellPrefab, gridRoot);
#endif
                go.name = name;
                go.transform.localPosition = LocalPositionFor(x, z);
                go.transform.localScale = Vector3.one * cellSize * 0.95f;
            }
            else
            {
                go = existing.gameObject;
            }

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = GetMaterialForCell(x, z, colorIdx);
        }

        private Material GetMaterialForCell(int x, int z, int colorIdx)
        {
            if (colorIdx < 0 || colorIdx >= palette.Count) return null;
            var cd = palette[colorIdx];
            if (cd == null) return null;

            switch (previewMode)
            {
                case LevelPreviewMode.Completed:
                    return cd.BoxHitMaterial != null ? cd.BoxHitMaterial : cd.BoxUnhitMaterial;
                case LevelPreviewMode.Initial:
                    return IsCellOnSilhouette(x, z)
                        ? cd.BoxUnhitMaterial
                        : GetLockedPreviewMaterial();
                default: // Editing
                    return cd.BoxUnhitMaterial;
            }
        }

        // 4-connected silhouette test that mirrors GridController.IsOnSilhouette but operates
        // on the editor's flat cells[] array. A cell is on the silhouette if any neighbor is
        // off-grid or empty (-1).
        private bool IsCellOnSilhouette(int x, int z)
        {
            if (!HasCells) return true;
            (int dx, int dz)[] n4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var n in n4)
            {
                int nx = x + n.dx, nz = z + n.dz;
                if (nx < 0 || nx >= gridSize || nz < 0 || nz >= gridSize) return true;
                if (cells[nz * gridSize + nx] < 0) return true;
            }
            return false;
        }

        private Material GetLockedPreviewMaterial()
        {
            if (lockedPreviewMaterial != null) return lockedPreviewMaterial;
            if (lockedFallback == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var col = new Color(0.42f, 0.42f, 0.46f);
                lockedFallback = new Material(shader) { color = col };
                if (lockedFallback.HasProperty("_BaseColor")) lockedFallback.SetColor("_BaseColor", col);
            }
            return lockedFallback;
        }

        public Vector3 LocalPositionFor(int x, int z)
        {
            float offset = (gridSize - 1) * 0.5f * cellSize;
            return new Vector3(x * cellSize - offset, 0f, z * cellSize - offset);
        }

        public bool TryWorldToCell(Vector3 worldPos, out int x, out int z)
        {
            x = z = -1;
            if (gridRoot == null) return false;
            Vector3 local = gridRoot.InverseTransformPoint(worldPos);
            float offset = (gridSize - 1) * 0.5f * cellSize;
            x = Mathf.RoundToInt((local.x + offset) / cellSize);
            z = Mathf.RoundToInt((local.z + offset) / cellSize);
            return x >= 0 && x < gridSize && z >= 0 && z < gridSize;
        }

        public void LoadFromAsset()
        {
            if (targetAsset == null) return;
            gridSize = Mathf.Max(1, targetAsset.Grid.Size);
            gridRootScale = targetAsset.Grid.RootScale;

            // Build cells[] from BoxCellData list. Extend the existing palette with any
            // colors the level uses but our palette doesn't already contain (keeps the
            // user-imported palette intact while filling gaps).
            EnsureCellsArray();
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;

            foreach (var bc in targetAsset.Grid.Cells)
            {
                if (bc.IsEmpty) continue;
                int idx = palette.IndexOf(bc.Color);
                if (idx < 0) { palette.Add(bc.Color); idx = palette.Count - 1; }
                if (bc.GridX < 0 || bc.GridX >= gridSize || bc.GridZ < 0 || bc.GridZ >= gridSize) continue;
                cells[bc.GridZ * gridSize + bc.GridX] = idx;
            }

            columns = new List<ColumnData>(targetAsset.Columns);
            conveyorSlotCapacity = targetAsset.ConveyorSlotCapacity;
            reserveSlotCapacity = targetAsset.ReserveSlotCapacity;

            Rebuild();
        }

        public void SaveToAsset()
        {
            if (targetAsset == null) return;
            EnsureCellsArray();

            // GridData fields are private — set via reflection.
            var grid = targetAsset.Grid;
            SetPrivateField(grid, "size", gridSize);
            SetPrivateField(grid, "rootScale", gridRootScale);

            var boxCells = new List<BoxCellData>();
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int colorIdx = cells[z * gridSize + x];
                    if (colorIdx < 0) continue;
                    if (colorIdx >= palette.Count || palette[colorIdx] == null) continue;
                    var bc = new BoxCellData();
                    SetPrivateField(bc, "gridX", x);
                    SetPrivateField(bc, "gridZ", z);
                    SetPrivateField(bc, "isEmpty", false);
                    SetPrivateField(bc, "color", palette[colorIdx]);
                    boxCells.Add(bc);
                }
            }
            SetPrivateField(grid, "cells", boxCells);

            SetPrivateField(targetAsset, "columns", new List<ColumnData>(columns));
            SetPrivateField(targetAsset, "conveyorSlotCapacity", conveyorSlotCapacity);
            SetPrivateField(targetAsset, "reserveSlotCapacity", reserveSlotCapacity);
        }

        /// <summary>
        /// Rebuilds the columns list from the painted grid. Cells are grouped by their
        /// gameplay color (a tone's main color), so a single shooter destroys every
        /// tone variant of that color group. One column per gameplay group, with enough
        /// shooters to cover every cell in that group.
        /// </summary>
        public void GenerateColumnsFromGrid(int maxShotsPerShooter)
        {
            EnsureCellsArray();
            maxShotsPerShooter = Mathf.Max(1, maxShotsPerShooter);

            // Group counts by GameplayColor (so tone variants merge into one column).
            // Preserve first-appearance order for deterministic output.
            var order = new List<ColorData>();
            var counts = new Dictionary<ColorData, int>();
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int c = cells[z * gridSize + x];
                    if (c < 0 || c >= palette.Count || palette[c] == null) continue;
                    var key = palette[c].GameplayColor;
                    if (!counts.ContainsKey(key)) { counts[key] = 0; order.Add(key); }
                    counts[key]++;
                }
            }

            var newColumns = new List<ColumnData>();
            foreach (var gameplayColor in order)
            {
                int total = counts[gameplayColor];
                int shooterCount = Mathf.CeilToInt(total / (float)maxShotsPerShooter);
                int remaining = total;
                var shooterList = new List<ShooterData>();
                for (int s = 0; s < shooterCount; s++)
                {
                    int shots = Mathf.Min(maxShotsPerShooter, remaining);
                    remaining -= shots;
                    var sd = new ShooterData();
                    SetPrivateField(sd, "color", gameplayColor);
                    SetPrivateField(sd, "shotCount", shots);
                    shooterList.Add(sd);
                }
                var col = new ColumnData();
                SetPrivateField(col, "shooters", shooterList);
                newColumns.Add(col);
            }

            columns = newColumns;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(target, value); return; }
                t = t.BaseType;
            }
            Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (gridRoot == null) return;
            float gs = gridSize * cellSize;
            Vector3 center = gridRoot.position;
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(center, new Vector3(gs, 0.01f, gs));
        }

        private void OnDrawGizmosSelected()
        {
            if (gridRoot == null) return;
            float gs = gridSize * cellSize;
            float off = gs * 0.5f;
            Vector3 c = gridRoot.position;
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.18f);
            for (int i = 0; i <= gridSize; i++)
            {
                float v = -off + i * cellSize;
                Gizmos.DrawLine(new Vector3(c.x + v, c.y, c.z - off), new Vector3(c.x + v, c.y, c.z + off));
                Gizmos.DrawLine(new Vector3(c.x - off, c.y, c.z + v), new Vector3(c.x + off, c.y, c.z + v));
            }
        }
#endif
    }
}
