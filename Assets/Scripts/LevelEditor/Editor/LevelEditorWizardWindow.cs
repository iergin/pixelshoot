#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Grid;

namespace PixelShoot.LevelEditor.EditorTools
{
    /// <summary>
    /// Self-contained level authoring tool. All editing state, painting and
    /// asset I/O happens inside this EditorWindow — no scene, no MonoBehaviour,
    /// no inspector required. Open via PixelShoot ▶ Open Level Editor Wizard.
    /// </summary>
    public class LevelEditorWizardWindow : EditorWindow
    {
        // ─── Paths / regex ────────────────────────────────────────────
        private const string ColorsDir     = "Assets/_Game/Colors";
        private const string MatBoxesDir   = "Assets/_Game/Materials/Boxes";
        private const string MatShootersDir = "Assets/_Game/Materials/Shooters";
        private const string MatBulletsDir = "Assets/_Game/Materials/Bullets";
        private const string LevelsDir     = "Assets/_Game/Levels";
        private static readonly Regex HexColorRegex = new Regex("#([0-9A-Fa-f]{6})");

        // ─── Persisted editing state ──────────────────────────────────
        [SerializeField] private LevelData targetAsset;
        [SerializeField] private int gridSize = 30;
        [SerializeField] private Vector3 gridRootPosition = Vector3.zero;
        [SerializeField] private Vector3 gridRootScale = Vector3.one;
        [SerializeField] private int conveyorSlotCapacity = 5;
        [SerializeField] private int reserveSlotCapacity = 5;
        [SerializeField] private List<ColorData> palette = new List<ColorData>();
        [SerializeField] private List<ColumnData> columns = new List<ColumnData>();
        [SerializeField] private int[] cells; // -1 = empty; else palette index
        [SerializeField] private int currentPaletteIdx = 0;
        [SerializeField] private bool eraseMode = false;
        [SerializeField] private bool paintMode = false;
        [SerializeField] private bool initialPreview = false;
        // True while "Show Final state" is active. Persists across auto-refreshes so the
        // visual doesn't snap back to Locked/Frontier the moment something else triggers a Build.
        [SerializeField] private bool inFinalStateView = false;

        // ─── Transient UI state ───────────────────────────────────────
        private string levelName = "Level_01";
        private string importBuffer = "";
        private int shotsPerShooter = 5;
        private Vector2 scroll;
        private string lastImportStatus = "";
        private static GUIStyle swatchStyle;

        // ─── Auto-refresh debounce ────────────────────────────────────
        private bool pendingPreviewRefresh = false;
        // Min interval between auto-refreshes (sec). Prevents lag while typing into fields.
        private const double AutoRefreshIntervalSec = 0.15;
        private double lastAutoRefreshTime = -1;
        // Last refresh outcome — shown in the Scene preview section so problems are visible.
        private string lastRefreshStatus = "";
        private MessageType lastRefreshStatusType = MessageType.None;

        [MenuItem("PixelShoot/Open Level Editor Wizard")]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWizardWindow>("Level Wizard");
            w.minSize = new Vector2(520, 800);
        }

        // ─── Helpers (state) ──────────────────────────────────────────
        private bool HasCells => cells != null && cells.Length == gridSize * gridSize;
        private int CellCount => gridSize * gridSize;

        private void EnsureCellsArray()
        {
            if (HasCells) return;
            var old = cells;
            int oldSize = (old != null && old.Length > 0) ? Mathf.RoundToInt(Mathf.Sqrt(old.Length)) : 0;
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

        // ─── OnGUI ────────────────────────────────────────────────────
        private void OnGUI()
        {
            EnsureStyles();

            EditorGUI.BeginChangeCheck();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("PixelShoot — Level Authoring Wizard",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            EditorGUILayout.Space(4);

            DrawTopAssetBar();
            DrawSectionBreak();
            DrawScenePreviewSection();
            DrawSectionBreak();
            DrawStep_Import();
            DrawSectionBreak();
            DrawStep_Grid();
            DrawSectionBreak();
            DrawStep_Columns();

            EditorGUILayout.EndScrollView();
            // Any value change in a widget above flips the auto-refresh flag.
            // The paint event in DrawClickableGrid also flips it directly.
            if (EditorGUI.EndChangeCheck()) pendingPreviewRefresh = true;
        }

        // Called several times per second by Unity for any visible EditorWindow.
        // Use it to debounce auto-refresh so we don't rebuild the scene on every keystroke.
        private void Update()
        {
            if (!pendingPreviewRefresh) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastAutoRefreshTime < AutoRefreshIntervalSec) return;
            pendingPreviewRefresh = false;
            lastAutoRefreshTime = now;
            if (targetAsset == null) return;
            // Do NOT persist on auto-refresh. Disk writes happen only when the user
            // explicitly presses Save — that's the contract the wizard advertises.
            // The asset's in-memory state still gets the latest data (so the scene
            // build matches the wizard), and SetDirty makes Unity flag it for save.
            RefreshScenePreview(persistToDisk: false);
        }

        private void EnsureStyles()
        {
            if (swatchStyle == null)
            {
                swatchStyle = new GUIStyle(GUI.skin.button)
                {
                    fixedWidth = 34, fixedHeight = 34, fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private static void DrawSectionBreak()
        {
            EditorGUILayout.Space(10);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.2f));
            EditorGUILayout.Space(6);
        }

        // ─── Top bar — asset + Save/Load/Clear ────────────────────────
        private void DrawTopAssetBar()
        {
            EditorGUILayout.LabelField("Asset", EditorStyles.boldLabel);
            levelName = EditorGUILayout.TextField("Level name", levelName);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save", GUILayout.Height(28))) SmartSave();
                if (GUILayout.Button("Load", GUILayout.Height(28))) SmartLoad();
                if (GUILayout.Button("Clear window", GUILayout.Height(28))) ClearWindow();
            }

            targetAsset = (LevelData)EditorGUILayout.ObjectField("Bound asset", targetAsset, typeof(LevelData), false);
            EditorGUILayout.HelpBox(
                targetAsset != null
                    ? $"Editing: {targetAsset.name}"
                    : $"No asset bound. Press Save to create / overwrite '{levelName}.asset' under {LevelsDir}.",
                targetAsset != null ? MessageType.Info : MessageType.None);
        }

        /// <summary>
        /// Save behavior:
        ///   - If an asset already exists at LevelsDir/{levelName}.asset → load it (if not
        ///     already bound) and overwrite its contents with the wizard state.
        ///   - Otherwise → create a new asset with that name and save into it.
        /// </summary>
        private void SmartSave()
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                EditorUtility.DisplayDialog("Save", "Please enter a level name first.", "OK");
                return;
            }
            EnsureDir(LevelsDir);
            string path = $"{LevelsDir}/{levelName}.asset";
            var onDisk = AssetDatabase.LoadAssetAtPath<LevelData>(path);

            if (onDisk != null)
            {
                // Asset with this name exists → overwrite.
                targetAsset = onDisk;
            }
            else
            {
                // Create new asset with the requested name (no GenerateUniqueAssetPath —
                // we *want* the exact filename, and we already proved nothing's there).
                var asset = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(asset, path);
                targetAsset = asset;
            }

            SaveToAsset();
            EditorUtility.SetDirty(targetAsset);
            AssetDatabase.SaveAssets();
            // Refresh the in-scene preview so the saved data shows up immediately.
            if (HasLoaderInScene()) RefreshScenePreview(persistToDisk: false);
        }

        private void ClearWindow()
        {
            if (!EditorUtility.DisplayDialog("Clear",
                "Empties the wizard, removes spawned boxes / columns from the open scene, " +
                "and unbinds the asset so it CANNOT be auto-overwritten with the empty state.\n\n" +
                "The asset on disk stays intact. Use Load again if you want to keep working on it.\n\n" +
                "Continue?", "Yes", "No"))
                return;

            // 1) Cancel any pending auto-refresh — otherwise a previously-tracked widget
            //    change would fire Update() *after* the clear and save empty data to the
            //    asset, wiping its cells / columns / palette.
            pendingPreviewRefresh = false;

            // 2) Unbind the asset so neither the explicit Save button nor any future
            //    auto-refresh can mutate it. The user must Load again to resume editing.
            string previouslyBound = targetAsset != null ? targetAsset.name : null;
            targetAsset = null;

            // 3) Wizard state.
            cells = new int[CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;
            columns?.Clear();
            palette?.Clear();
            currentPaletteIdx = 0;
            importBuffer = "";
            lastImportStatus = "";

            // 4) Scene preview: nuke whatever is currently parented under gridRoot /
            //    columnsRoot so the visuals match the cleared wizard state.
            ClearSceneVisuals();

            SetStatus(
                previouslyBound != null
                    ? $"Wizard and scene cleared. '{previouslyBound}' on disk is untouched — Load again to keep editing."
                    : "Wizard and scene cleared.",
                MessageType.Info);
            SceneView.RepaintAll();
            Debug.Log($"[LevelWizard] ClearWindow done. Previously bound asset='{previouslyBound ?? "<none>"}' — left on disk, unbound from wizard.");
        }

        /// <summary>Destroys all children of GridController.gridRoot and LevelLoader.columnsRoot.</summary>
        private static void ClearSceneVisuals()
        {
#if UNITY_2023_1_OR_NEWER
            var gridCtrl = Object.FindFirstObjectByType<GridController>(FindObjectsInactive.Include);
            var loader = Object.FindFirstObjectByType<LevelLoader>(FindObjectsInactive.Include);
#else
            var gridCtrl = Object.FindObjectOfType<GridController>();
            var loader = Object.FindObjectOfType<LevelLoader>();
#endif
            if (gridCtrl != null)
            {
                // Easiest path: GridController already has a public Clear() that destroys
                // every spawned Box under gridRoot.
                gridCtrl.Clear();
                // gridCtrl.Clear() destroys boxes via runtime Destroy. In edit mode we also
                // catch anything left as a child of gridRoot just in case.
                var gridRootField = gridCtrl.GetType().GetField("gridRoot",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (gridRootField != null && gridRootField.GetValue(gridCtrl) is Transform gr && gr != null)
                {
                    for (int i = gr.childCount - 1; i >= 0; i--)
                        DestroyImmediate(gr.GetChild(i).gameObject);
                }
            }
            if (loader != null)
            {
                var colsRootField = loader.GetType().GetField("columnsRoot",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (colsRootField != null && colsRootField.GetValue(loader) is Transform cr && cr != null)
                {
                    for (int i = cr.childCount - 1; i >= 0; i--)
                        DestroyImmediate(cr.GetChild(i).gameObject);
                }
            }
            Debug.Log("[LevelWizard] ClearSceneVisuals: emptied gridRoot and columnsRoot.");
        }

        private bool HasLoaderInScene()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<LevelLoader>(FindObjectsInactive.Include) != null;
#else
            return Object.FindObjectOfType<LevelLoader>() != null;
#endif
        }

        /// <summary>
        /// Load behavior:
        ///   - Look for a LevelData asset named '{levelName}.asset' under LevelsDir.
        ///   - If found → bind it and pull its state into the wizard.
        ///   - If not found → tell the user (no file picker fallback by design — the
        ///     workflow is "type name → press button").
        /// </summary>
        private void SmartLoad()
        {
            Debug.Log($"[LevelWizard] SmartLoad() invoked. levelName='{levelName}'");
            if (string.IsNullOrWhiteSpace(levelName))
            {
                Debug.LogWarning("[LevelWizard] SmartLoad aborted: levelName is empty.");
                EditorUtility.DisplayDialog("Load", "Please enter a level name first.", "OK");
                return;
            }
            string path = $"{LevelsDir}/{levelName}.asset";
            Debug.Log($"[LevelWizard] SmartLoad looking up '{path}'");
            var onDisk = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (onDisk == null)
            {
                Debug.LogWarning($"[LevelWizard] SmartLoad: no LevelData at '{path}'.");
                EditorUtility.DisplayDialog("Load",
                    $"No LevelData asset named '{levelName}.asset' under {LevelsDir}.", "OK");
                return;
            }
            Debug.Log($"[LevelWizard] SmartLoad found asset '{onDisk.name}', binding…");
            targetAsset = onDisk;
            LoadFromAsset();
            Debug.Log($"[LevelWizard] SmartLoad: HasLoaderInScene()={HasLoaderInScene()}");
            if (HasLoaderInScene()) RefreshScenePreview(persistToDisk: false);
            Debug.Log("[LevelWizard] SmartLoad finished.");
        }

        // ─── Import (palette + RLE) ──────────────────────────────────
        private void DrawStep_Import()
        {
            bool gated = targetAsset == null;
            EditorGUILayout.LabelField("Import (palette + RLE in one paste)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                EditorGUILayout.LabelField("Paste the full encoder export (PALETTE and/or RLE_ROWS):", EditorStyles.miniLabel);
                importBuffer = EditorGUILayout.TextArea(importBuffer, GUILayout.MinHeight(140));
                if (GUILayout.Button("Import all (auto-detect)", GUILayout.Height(28))) ImportAll();
            }
            if (!string.IsNullOrEmpty(lastImportStatus))
                EditorGUILayout.HelpBox(lastImportStatus, MessageType.Info);
        }

        private void ImportAll()
        {
            string text = importBuffer ?? "";
            int paletteCount = 0;
            int filledCells = 0;
            int detectedGrid = 0;

            var matches = HexColorRegex.Matches(text);
            if (matches.Count > 0)
            {
                var newPalette = new List<ColorData>(matches.Count);
                foreach (Match m in matches)
                {
                    string hex = m.Groups[1].Value.ToUpperInvariant();
                    Color col = ColorUtility.TryParseHtmlString("#" + hex, out var parsed) ? parsed : Color.magenta;
                    newPalette.Add(GetOrCreateColorData(hex, col));
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                palette = newPalette;
                currentPaletteIdx = 0;
                paletteCount = newPalette.Count;
            }

            if (RLECodec.TryDetectGridSize(text, out int detected))
            {
                detectedGrid = detected;
                gridSize = detected;
            }
            EnsureCellsArray();

            if (gridSize > 0 && RLECodec.TryDecode(text, gridSize, out var decoded))
            {
                cells = decoded;
                foreach (var v in decoded) if (v >= 0) filledCells++;
            }

            var parts = new List<string>();
            if (paletteCount > 0) parts.Add($"{paletteCount} colors");
            if (detectedGrid > 0) parts.Add($"{detectedGrid}×{detectedGrid} grid");
            if (filledCells > 0) parts.Add($"{filledCells} filled cells");
            lastImportStatus = parts.Count == 0
                ? "Nothing detected in the pasted text — make sure it contains hex colors and/or an RLE_ROWS array."
                : "Imported: " + string.Join(", ", parts) + ". (Press Save to persist to disk.)";
            // No disk write here — the user has to press Save explicitly. Auto-refresh
            // will rebuild the scene from the in-memory asset state.
            pendingPreviewRefresh = true;
            Repaint();
        }

        // ─── Grid & painting ─────────────────────────────────────────
        private void DrawStep_Grid()
        {
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                int newSize = Mathf.Max(1, EditorGUILayout.IntField("Grid size", gridSize, GUILayout.Width(180)));
                if (newSize != gridSize) { gridSize = newSize; EnsureCellsArray(); }
                if (GUILayout.Button("New (clear)", GUILayout.Width(90)))
                {
                    if (EditorUtility.DisplayDialog("Clear grid", "Clear the current grid?", "Yes", "No"))
                    {
                        cells = new int[CellCount];
                        for (int i = 0; i < cells.Length; i++) cells[i] = -1;
                        pendingPreviewRefresh = true;
                    }
                }
            }
            gridRootPosition = EditorGUILayout.Vector3Field("Grid root position", gridRootPosition);
            // Uniform scale slider — every axis tracks the same value so the grid scales evenly.
            float currentUniform = gridRootScale.x > 0.0001f ? gridRootScale.x : 1f;
            float newUniform = EditorGUILayout.Slider("Grid root scale", currentUniform, 0.05f, 5f);
            if (!Mathf.Approximately(newUniform, currentUniform))
                gridRootScale = Vector3.one * newUniform;

            using (new EditorGUILayout.HorizontalScope())
            {
                // Paint and Erase are mutually exclusive — toggling one off-by-default
                // disables clicks entirely, so accidental drags over the grid do nothing.
                bool newPaint = GUILayout.Toggle(paintMode, "Paint", "Button", GUILayout.Width(70));
                if (newPaint != paintMode)
                {
                    paintMode = newPaint;
                    if (paintMode) eraseMode = false;
                }
                bool newErase = GUILayout.Toggle(eraseMode, "Erase", "Button", GUILayout.Width(70));
                if (newErase != eraseMode)
                {
                    eraseMode = newErase;
                    if (eraseMode) paintMode = false;
                }
                bool newPrev = GUILayout.Toggle(initialPreview, "Initial preview", "Button", GUILayout.Width(140));
                if (newPrev != initialPreview) initialPreview = newPrev;
                string statusLabel;
                if (eraseMode) statusLabel = "Click cells to clear them.";
                else if (paintMode) statusLabel = $"Painting palette idx {currentPaletteIdx}.";
                else statusLabel = "Neither Paint nor Erase active — clicks do nothing.";
                EditorGUILayout.LabelField(statusLabel);
            }

            if (palette != null && palette.Count > 0)
            {
                EditorGUILayout.LabelField("Palette:", EditorStyles.miniLabel);
                DrawPaletteSelectableSwatches();
            }

            EnsureCellsArray();
            DrawClickableGrid();

            if (HasCells)
            {
                int filled = 0;
                foreach (var v in cells) if (v >= 0) filled++;
                EditorGUILayout.HelpBox($"Cells filled: {filled} / {CellCount}", MessageType.None);
            }
        }

        private void DrawClickableGrid()
        {
            float availableWidth = position.width - 40f;
            float cellPx = Mathf.Clamp(Mathf.Floor(availableWidth / gridSize), 6f, 24f);
            float totalSize = cellPx * gridSize;

            Rect gridRect = GUILayoutUtility.GetRect(totalSize, totalSize);
            EditorGUI.DrawRect(gridRect, new Color(0.08f, 0.08f, 0.1f));

            // Cells.
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int colorIdx = cells[z * gridSize + x];
                    if (colorIdx < 0) continue;
                    Color c = (colorIdx < palette.Count && palette[colorIdx] != null)
                        ? palette[colorIdx].DisplayColor
                        : Color.magenta;
                    if (initialPreview && !IsCellOnSilhouette(x, z))
                        c = new Color(0.42f, 0.42f, 0.46f);
                    EditorGUI.DrawRect(CellRect(gridRect, x, z, cellPx), c);
                }
            }

            // Grid lines (subtle).
            Color lineCol = new Color(1f, 1f, 1f, 0.06f);
            for (int i = 1; i < gridSize; i++)
            {
                float v = i * cellPx;
                EditorGUI.DrawRect(new Rect(gridRect.x + v, gridRect.y, 1, totalSize), lineCol);
                EditorGUI.DrawRect(new Rect(gridRect.x, gridRect.y + v, totalSize, 1), lineCol);
            }

            // Painting (LMB down/drag). Requires Paint OR Erase to be active and
            // not be in Initial preview mode (which is read-only).
            var e = Event.current;
            bool toolActive = paintMode || eraseMode;
            bool isPaintEvent = e.isMouse
                                && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                                && e.button == 0
                                && toolActive
                                && !initialPreview
                                && gridRect.Contains(e.mousePosition);
            if (isPaintEvent)
            {
                int xx = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / cellPx);
                int zz = gridSize - 1 - Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / cellPx);
                if (xx >= 0 && xx < gridSize && zz >= 0 && zz < gridSize)
                {
                    int idx = zz * gridSize + xx;
                    int newColor = eraseMode ? -1 : currentPaletteIdx;
                    if (cells[idx] != newColor)
                    {
                        cells[idx] = newColor;
                        pendingPreviewRefresh = true;
                        e.Use();
                        Repaint();
                    }
                }
            }
        }

        private Rect CellRect(Rect gridRect, int x, int z, float cellPx)
        {
            // Flip Y so z=0 is at the BOTTOM of the drawn grid (matches gameplay).
            float px = gridRect.x + x * cellPx;
            float py = gridRect.y + (gridSize - 1 - z) * cellPx;
            return new Rect(px, py, cellPx, cellPx);
        }

        private bool IsCellOnSilhouette(int x, int z)
        {
            (int dx, int dz)[] n4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var n in n4)
            {
                int nx = x + n.dx, nz = z + n.dz;
                if (nx < 0 || nx >= gridSize || nz < 0 || nz >= gridSize) return true;
                if (cells[nz * gridSize + nx] < 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Compares the painted-pixel count against the sum of all shooter shot counts.
        /// A level is only solvable when these match — every shot has a target and every
        /// box has a bullet. Shown as a colored banner so the level designer can't miss it.
        /// </summary>
        private void DrawBulletBudgetValidation()
        {
            int filledPixels = 0;
            if (cells != null) foreach (var v in cells) if (v >= 0) filledPixels++;

            int totalShots = 0;
            if (columns != null)
                foreach (var col in columns)
                    if (col != null && col.Shooters != null)
                        foreach (var s in col.Shooters) totalShots += s.ShotCount;

            if (filledPixels == 0 && totalShots == 0)
            {
                EditorGUILayout.HelpBox("No pixels painted yet — paint cells and auto-generate columns to populate.",
                    MessageType.None);
                return;
            }

            if (filledPixels == totalShots)
            {
                EditorGUILayout.HelpBox(
                    $"✓ Bullet budget OK — {totalShots} shots = {filledPixels} painted pixels. Level is solvable.",
                    MessageType.Info);
            }
            else
            {
                int diff = totalShots - filledPixels;
                string explain = diff > 0
                    ? $"{diff} extra shot(s) with no box to hit — leftover shooters will sit idle."
                    : $"{-diff} missing shot(s) — not enough bullets to clear every box, level UNSOLVABLE.";
                EditorGUILayout.HelpBox(
                    $"⚠ Bullet budget mismatch.\n" +
                    $"   Painted pixels:  {filledPixels}\n" +
                    $"   Total shots:     {totalShots}\n" +
                    $"   {explain}\n" +
                    "Re-run 'Auto-generate columns from grid' or hand-edit columns until they match.",
                    MessageType.Error);
            }
        }

        // ─── Columns & capacities ─────────────────────────────────────
        private void DrawStep_Columns()
        {
            bool gated = !HasCells;
            EditorGUILayout.LabelField("Columns & capacities", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Max shots / shooter", GUILayout.Width(140));
                    shotsPerShooter = Mathf.Max(1, EditorGUILayout.IntField(shotsPerShooter, GUILayout.Width(60)));
                }
                if (GUILayout.Button("Auto-generate columns from grid"))
                {
                    GenerateColumnsFromGrid(shotsPerShooter);
                    pendingPreviewRefresh = true;
                }
            }
            conveyorSlotCapacity = Mathf.Max(1, EditorGUILayout.IntField("Conveyor capacity", conveyorSlotCapacity));
            reserveSlotCapacity = Mathf.Max(1, EditorGUILayout.IntField("Reserve capacity", reserveSlotCapacity));
            DrawBulletBudgetValidation();
            if (columns != null && columns.Count > 0)
            {
                int sh = 0, shots = 0;
                foreach (var col in columns)
                    foreach (var s in col.Shooters) { sh++; shots += s.ShotCount; }
                EditorGUILayout.HelpBox($"Columns: {columns.Count}, shooters: {sh}, total shots: {shots}", MessageType.None);
            }
        }

        // ─── Scene preview (uses runtime gameplay scripts) ────────────
        private void DrawScenePreviewSection()
        {
            EditorGUILayout.LabelField("Scene preview", EditorStyles.boldLabel);
            DrawSceneSetupDiagnostics();
            using (new EditorGUI.DisabledScope(targetAsset == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh preview in scene", GUILayout.Height(28)))
                    {
                        inFinalStateView = false; // explicit refresh leaves final-state view.
                        RefreshScenePreview(persistToDisk: false);
                    }
                    string finalLabel = inFinalStateView ? "Exit final state" : "Show final state";
                    if (GUILayout.Button(finalLabel, GUILayout.Height(28)))
                    {
                        inFinalStateView = !inFinalStateView;
                        RefreshScenePreview(persistToDisk: false);
                    }
                }
            }
            EditorGUILayout.HelpBox(
                "The preview auto-refreshes in the open scene whenever you change a value above. " +
                "Use 'Refresh' to force a rebuild, or 'Show final state' to see every box already in its Hit appearance — the level's cleared-out look.",
                MessageType.None);
            if (!string.IsNullOrEmpty(lastRefreshStatus))
                EditorGUILayout.HelpBox(lastRefreshStatus, lastRefreshStatusType);
        }

        /// <summary>
        /// Walks the open scene and reports anything that would make Build() fail —
        /// missing LevelLoader, missing prefab refs on LevelLoader / GridController, etc.
        /// </summary>
        private void DrawSceneSetupDiagnostics()
        {
#if UNITY_2023_1_OR_NEWER
            var loader = Object.FindFirstObjectByType<LevelLoader>(FindObjectsInactive.Include);
            var grid = Object.FindFirstObjectByType<GridController>(FindObjectsInactive.Include);
#else
            var loader = Object.FindObjectOfType<LevelLoader>();
            var grid = Object.FindObjectOfType<GridController>();
#endif

            if (loader == null)
            {
                EditorGUILayout.HelpBox(
                    "No LevelLoader found in the open scene. Open the LevelEditor scene that has " +
                    "LevelLoader + GridController + ConveyorController + ReserveController + " +
                    "GameController set up.",
                    MessageType.Warning);
                return;
            }

            var problems = new List<string>();

            // LevelLoader refs.
            CheckRef(loader, "grid",          "LevelLoader.grid (GridController)",       problems);
            CheckRef(loader, "conveyor",      "LevelLoader.conveyor (ConveyorController)", problems);
            CheckRef(loader, "reserve",       "LevelLoader.reserve (ReserveController)",   problems);
            CheckRef(loader, "gameController","LevelLoader.gameController (GameController)", problems);
            CheckRef(loader, "shooterPrefab", "LevelLoader.shooterPrefab",                problems);
            CheckRef(loader, "columnPrefab",  "LevelLoader.columnPrefab",                 problems);
            CheckRef(loader, "columnsRoot",   "LevelLoader.columnsRoot (Transform)",      problems);

            // GridController refs.
            if (grid != null)
            {
                CheckRef(grid, "boxPrefab", "GridController.boxPrefab", problems);
                CheckRef(grid, "gridRoot",  "GridController.gridRoot",  problems);
            }

            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"Scene OK — LevelLoader '{loader.name}' is ready to build.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Missing references on the scene — the preview will NullReference until you " +
                    "fix these in the inspector:\n  • " + string.Join("\n  • ", problems),
                    MessageType.Error);
                if (GUILayout.Button("Select offending GameObject"))
                {
                    Selection.activeObject = loader.gameObject;
                    EditorGUIUtility.PingObject(loader.gameObject);
                }
            }
        }

        private static void CheckRef(object target, string fieldName, string displayName, List<string> problems)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null)
                {
                    // Cast to UnityEngine.Object so we catch both real null AND
                    // Unity's "fake null" (destroyed/missing) for SerializeField refs.
                    var uo = f.GetValue(target) as UnityEngine.Object;
                    if (uo == null) problems.Add(displayName);
                    return;
                }
                t = t.BaseType;
            }
            problems.Add($"{displayName} — field not found on {target.GetType().Name}");
        }

        /// <summary>
        /// Find LevelLoader in the open scene, push wizard state into the bound LevelData,
        /// clear the columns root (to avoid duplicates), then call loader.Build().
        /// Returns true if a loader was found and Build ran without throwing.
        /// </summary>
        private bool RefreshScenePreview(bool persistToDisk)
        {
            Debug.Log($"[LevelWizard] RefreshScenePreview(persistToDisk={persistToDisk}) targetAsset={(targetAsset != null ? targetAsset.name : "<null>")}");
            if (targetAsset == null) { Debug.LogWarning("[LevelWizard] RefreshScenePreview aborted: no targetAsset."); return false; }

            // Flush wizard state into the bound asset before rebuilding from it.
            Debug.Log("[LevelWizard] RefreshScenePreview → SaveToAsset()");
            SaveToAsset();
            EditorUtility.SetDirty(targetAsset);
            if (persistToDisk) AssetDatabase.SaveAssets();

#if UNITY_2023_1_OR_NEWER
            var loader = Object.FindFirstObjectByType<LevelLoader>(FindObjectsInactive.Include);
            var gridCtrl = Object.FindFirstObjectByType<GridController>(FindObjectsInactive.Include);
#else
            var loader = Object.FindObjectOfType<LevelLoader>();
            var gridCtrl = Object.FindObjectOfType<GridController>();
#endif
            Debug.Log($"[LevelWizard] RefreshScenePreview: loader={(loader != null ? loader.name : "<null>")}, gridCtrl={(gridCtrl != null ? gridCtrl.name : "<null>")}");
            if (loader == null)
            {
                SetStatus("No LevelLoader in open scene.", MessageType.Warning);
                return false;
            }

            // Bind the asset on the loader (private field).
            SetField(loader, "levelData", targetAsset);
            Debug.Log("[LevelWizard] RefreshScenePreview: bound levelData on loader.");

            // SpawnColumns appends children to columnsRoot; calling Build twice would
            // duplicate them. Clear it ourselves before letting Build run.
            var columnsRootField = loader.GetType().GetField("columnsRoot",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (columnsRootField != null && columnsRootField.GetValue(loader) is Transform columnsRoot && columnsRoot != null)
            {
                Debug.Log($"[LevelWizard] RefreshScenePreview: clearing {columnsRoot.childCount} column children.");
                for (int i = columnsRoot.childCount - 1; i >= 0; i--)
                    DestroyImmediate(columnsRoot.GetChild(i).gameObject);
            }

            try
            {
                Debug.Log("[LevelWizard] RefreshScenePreview → loader.Build()");
                loader.Build();
                Debug.Log("[LevelWizard] RefreshScenePreview: Build() returned without throwing.");
            }
            catch (System.Exception ex)
            {
                SetStatus($"Build threw: {ex.GetType().Name} — {ex.Message}\nSee console for stack.",
                          MessageType.Error);
                Debug.LogException(ex);
                return false;
            }

            // Post-build report: how many boxes / columns actually got into the scene.
            int boxesSpawned = 0;
            if (gridCtrl != null)
            {
                var boxes = gridCtrl.GetComponentsInChildren<Box>(includeInactive: true);
                boxesSpawned = boxes.Length;
            }
            int dataCells = 0;
            foreach (var c in targetAsset.Grid.Cells) if (!c.IsEmpty) dataCells++;
            int colsSpawned = 0;
            if (columnsRootField != null && columnsRootField.GetValue(loader) is Transform colsRoot && colsRoot != null)
                colsSpawned = colsRoot.childCount;

            Debug.Log($"[LevelWizard] RefreshScenePreview: boxesSpawned={boxesSpawned}, dataCells={dataCells}, colsSpawned={colsSpawned}");
            if (boxesSpawned == 0 && dataCells > 0)
            {
                SetStatus(
                    $"Build ran but no boxes appeared. Data has {dataCells} cells. " +
                    "Likely causes:\n" +
                    "  • GridController.boxPrefab missing a MeshRenderer / mesh\n" +
                    "  • gridRoot has zero scale or is outside the camera view\n" +
                    "  • cells reference colors with no ColorData → check console",
                    MessageType.Warning);
            }
            else if (inFinalStateView && gridCtrl != null)
            {
                // Build just put every box back into Locked/Frontier; re-apply the final
                // state visual so the auto-refresh doesn't visibly clobber it.
                int hadHit, fallback, noColor;
                ApplyFinalStateVisuals(gridCtrl, out hadHit, out fallback, out noColor);
                SetStatus(
                    $"Final state active: {boxesSpawned} boxes — {hadHit} via BoxHitMaterial, " +
                    $"{fallback} via DisplayColor tint" + (noColor > 0 ? $", {noColor} no ColorData" : "") +
                    ".  (Press 'Exit final state' to return to edit view.)",
                    MessageType.Info);
            }
            else
            {
                SetStatus(
                    $"Built {boxesSpawned} boxes, {colsSpawned} columns from {targetAsset.name}.",
                    MessageType.Info);
            }

            SceneView.RepaintAll();
            return true;
        }

        /// <summary>
        /// Flips every Box under the GridController to its Hit state and, when the
        /// per-color BoxHitMaterial is missing, falls back to tinting the box's renderers
        /// with DisplayColor via a MaterialPropertyBlock (no material leaks).
        /// </summary>
        private static void ApplyFinalStateVisuals(GridController gridCtrl,
            out int hadHitMat, out int usedFallback, out int noColor)
        {
            hadHitMat = usedFallback = noColor = 0;
            var boxes = gridCtrl.GetComponentsInChildren<Box>(includeInactive: true);
            var props = new MaterialPropertyBlock();
            foreach (var b in boxes)
            {
                if (b == null) continue;
                b.TakeHit();
                var cd = b.Color;
                if (cd == null) { noColor++; continue; }
                if (cd.BoxHitMaterial != null) { hadHitMat++; continue; }

                Color tint = cd.DisplayColor;
                var rends = b.GetComponentsInChildren<MeshRenderer>(includeInactive: false);
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(props);
                    props.SetColor("_BaseColor", tint);
                    props.SetColor("_Color", tint);
                    r.SetPropertyBlock(props);
                }
                usedFallback++;
            }
        }

        private void SetStatus(string msg, MessageType type)
        {
            lastRefreshStatus = msg;
            lastRefreshStatusType = type;
        }

        // ─── Asset I/O ────────────────────────────────────────────────
        private void LoadFromAsset()
        {
            Debug.Log($"[LevelWizard] LoadFromAsset() targetAsset={(targetAsset != null ? targetAsset.name : "<null>")}");
            if (targetAsset == null) return;
            gridSize = Mathf.Max(1, targetAsset.Grid.Size);
            gridRootPosition = targetAsset.Grid.RootPosition;
            gridRootScale = targetAsset.Grid.RootScale;
            Debug.Log($"[LevelWizard] LoadFromAsset: gridSize={gridSize}, gridRootPosition={gridRootPosition}, gridRootScale={gridRootScale}");
            // Defensive: an asset created elsewhere may have a zero-scale GridData,
            // which would render the runtime grid invisible. Snap to identity scale.
            if (gridRootScale.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[LevelWizard] LoadFromAsset: gridRootScale was ~zero, snapping to Vector3.one.");
                gridRootScale = Vector3.one;
            }
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
            int filled = 0;
            if (cells != null) foreach (var v in cells) if (v >= 0) filled++;
            Debug.Log($"[LevelWizard] LoadFromAsset done. palette={palette.Count}, columns={columns.Count}, " +
                      $"conveyorCap={conveyorSlotCapacity}, reserveCap={reserveSlotCapacity}, filledCells={filled}");
            pendingPreviewRefresh = true;
            Repaint();
        }

        private void SaveToAsset()
        {
            if (targetAsset == null) return;
            EnsureCellsArray();
            // Never write a zero scale — that would make the runtime grid invisible.
            if (gridRootScale.sqrMagnitude < 0.0001f) gridRootScale = Vector3.one;

            var grid = targetAsset.Grid;
            SetField(grid, "size", gridSize);
            SetField(grid, "rootPosition", gridRootPosition);
            SetField(grid, "rootScale", gridRootScale);

            var boxCells = new List<BoxCellData>();
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int colorIdx = cells[z * gridSize + x];
                    if (colorIdx < 0) continue;
                    if (colorIdx >= palette.Count || palette[colorIdx] == null) continue;
                    var bc = new BoxCellData();
                    SetField(bc, "gridX", x);
                    SetField(bc, "gridZ", z);
                    SetField(bc, "isEmpty", false);
                    SetField(bc, "color", palette[colorIdx]);
                    boxCells.Add(bc);
                }
            }
            SetField(grid, "cells", boxCells);

            SetField(targetAsset, "columns", new List<ColumnData>(columns));
            SetField(targetAsset, "conveyorSlotCapacity", conveyorSlotCapacity);
            SetField(targetAsset, "reserveSlotCapacity", reserveSlotCapacity);
        }

        private void GenerateColumnsFromGrid(int maxShotsPerShooter)
        {
            EnsureCellsArray();
            maxShotsPerShooter = Mathf.Max(1, maxShotsPerShooter);

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
                    SetField(sd, "color", gameplayColor);
                    SetField(sd, "shotCount", shots);
                    shooterList.Add(sd);
                }
                var col = new ColumnData();
                SetField(col, "shooters", shooterList);
                newColumns.Add(col);
            }
            columns = newColumns;
        }

        // ─── Palette swatches ─────────────────────────────────────────
        private void DrawPaletteSelectableSwatches()
        {
            const int PerRow = 10;
            for (int i = 0; i < palette.Count; i++)
            {
                if (i % PerRow == 0) EditorGUILayout.BeginHorizontal();
                var cd = palette[i];
                var col = cd != null ? cd.DisplayColor : Color.magenta;
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = col;
                bool selected = (i == currentPaletteIdx && paintMode && !eraseMode);
                string label = selected ? $"[{i}]" : i.ToString();
                if (GUILayout.Button(label, swatchStyle))
                {
                    // Picking a swatch implies you want to paint — switch into Paint
                    // mode automatically so the next click actually adds a box.
                    currentPaletteIdx = i;
                    paintMode = true;
                    eraseMode = false;
                }
                GUI.backgroundColor = prev;
                if (i % PerRow == PerRow - 1 || i == palette.Count - 1) EditorGUILayout.EndHorizontal();
            }
        }

        // ─── ColorData / Material factories ───────────────────────────
        private static ColorData GetOrCreateColorData(string hex, Color baseColor)
        {
            string colorDir = $"{ColorsDir}/{hex}";
            EnsureDir(colorDir);
            string path = $"{colorDir}/Color_{hex}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ColorData>(path);
            if (existing != null) return existing;

            // Hit material is the only per-color box material now; the shared "unhit"
            // material lives on GridController and is the same for every color.
            var hit        = CreateMaterial(MatBoxesDir,    hex, "Hit",     baseColor);
            var shooterMat = CreateMaterial(MatShootersDir, hex, "Shooter", baseColor);
            var bulletMat  = CreateMaterial(MatBulletsDir,  hex, "Bullet",  baseColor);

            var cd = ScriptableObject.CreateInstance<ColorData>();
            SetField(cd, "colorId", hex);
            SetField(cd, "displayColor", baseColor);
            SetField(cd, "boxHitMaterial", hit);
            SetField(cd, "shooterMaterial", shooterMat);
            SetField(cd, "bulletMaterial", bulletMat);
            AssetDatabase.CreateAsset(cd, path);
            return cd;
        }

        private static Material CreateMaterial(string baseDir, string hex, string name, Color color)
        {
            string dir = $"{baseDir}/{hex}";
            EnsureDir(dir);
            string path = $"{dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ─── Generic helpers ──────────────────────────────────────────
        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            var name = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetField(object target, string fieldName, object value)
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
    }
}
#endif
