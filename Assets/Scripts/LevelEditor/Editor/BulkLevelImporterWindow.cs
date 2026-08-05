#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Grid;

namespace PixelShoot.LevelEditor.EditorTools
{
    /// <summary>
    /// Bulk level importer: point it at a FOLDER of level-designer JSON files and it creates one
    /// <see cref="LevelData"/> asset per file, using the SAME conversion as the single-file Level
    /// Wizard (<see cref="LevelJsonImporter"/> + <see cref="RLECodec"/> + shared ColorData factory).
    /// Palette colours / materials are created (and reused by hex) exactly like the Wizard, so bulk
    /// and single imports produce identical assets. Open via <b>PixelShoot ▸ Bulk Import Levels</b>.
    /// </summary>
    public class BulkLevelImporterWindow : EditorWindow
    {
        private const string LevelsDir = "Assets/_Game/Levels";
        private static readonly Regex ConveyorWidthRx = new Regex("\"conveyorWidth\"\\s*:\\s*(\\d+)");

        [SerializeField] private string sourceFolder = "";
        [SerializeField] private int fallbackConveyorCapacity = 5; // used when config.conveyorWidth is missing
        [SerializeField] private int reserveCapacity = 5;
        [SerializeField] private bool useConfigConveyorWidth = true;
        [SerializeField] private bool overwriteExisting = true;

        [Header("Auto-fit (like the Level Wizard's 'Fit grid to GridArea')")]
        [SerializeField] private bool autoFitToGridArea = true;
        [SerializeField] private string gridAreaName = "GridArea";
        [SerializeField] private float autoFitFill = 1f;

        [Header("Tones (like the Wizard's 'Recalculate tones (random)')")]
        [SerializeField] private bool randomizeTones = true;
        [SerializeField, Range(0f, 1f)] private float toneNormalRatio = ToneDistributor.DefaultNormalRatio;
        [SerializeField, Range(0f, 1f)] private float toneDarkRatio = ToneDistributor.DefaultDarkRatio;

        private Vector2 scroll;
        private string log = "";
        private AutoFitContext autoFitCtx; // scene lookups resolved once per import run

        [MenuItem("PixelShoot/Bulk Import Levels (JSON folder)")]
        public static void Open()
        {
            var w = GetWindow<BulkLevelImporterWindow>("Bulk Level Import");
            w.minSize = new Vector2(460, 420);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Bulk Level Importer", new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            EditorGUILayout.HelpBox(
                "Pick a FOLDER of level JSONs. One LevelData asset is created per .json (same conversion " +
                "as the Level Wizard: palette → ColorData, rle → grid, sortColumns → buses, bombs/keys). " +
                "Files without palette/rle/gridSize are skipped. Asset name = JSON file name.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("Source folder", sourceFolder);
                if (GUILayout.Button("Browse…", GUILayout.Width(90)))
                {
                    string start = (!string.IsNullOrEmpty(sourceFolder) && Directory.Exists(sourceFolder)) ? sourceFolder : Application.dataPath;
                    string p = EditorUtility.OpenFolderPanel("Select folder of level JSONs", start, "");
                    if (!string.IsNullOrEmpty(p)) sourceFolder = p;
                }
            }

            useConfigConveyorWidth = EditorGUILayout.Toggle(
                new GUIContent("Use config.conveyorWidth", "Read each level's conveyor capacity from its JSON 'config.conveyorWidth'. Off = always use the fallback below."),
                useConfigConveyorWidth);
            fallbackConveyorCapacity = Mathf.Max(1, EditorGUILayout.IntField("Conveyor capacity (fallback)", fallbackConveyorCapacity));
            reserveCapacity = Mathf.Max(1, EditorGUILayout.IntField("Reserve capacity", reserveCapacity));
            overwriteExisting = EditorGUILayout.Toggle(
                new GUIContent("Overwrite existing", "On = overwrite an existing LevelData with the same name. Off = skip files whose asset already exists."),
                overwriteExisting);

            EditorGUILayout.Space(4);
            autoFitToGridArea = EditorGUILayout.Toggle(
                new GUIContent("Auto-fit to GridArea", "Scale + centre each level's grid to fit inside the scene's GridArea plane — same as the Level Wizard's 'Fit grid to GridArea' button. Needs the scene with GridArea + GridController OPEN."),
                autoFitToGridArea);
            using (new EditorGUI.DisabledScope(!autoFitToGridArea))
            {
                gridAreaName = EditorGUILayout.TextField(new GUIContent("Fit target (scene object)", "Name of the scene plane the grid is fit into."), gridAreaName);
                autoFitFill = EditorGUILayout.Slider(new GUIContent("Fill", "1 = touch the edges; <1 leaves a margin."), autoFitFill, 0.5f, 1f);
                if (GUILayout.Button("Check GridArea in open scene", GUILayout.Height(20)))
                {
                    var ctx = BuildAutoFitContext();
                    EditorUtility.DisplayDialog("Auto-fit check",
                        ctx.valid
                            ? $"Found '{gridAreaName}' — size {ctx.areaSize.x:0.##}×{ctx.areaSize.z:0.##}. Auto-fit will work."
                            : $"'{gridAreaName}' (or a GridController) NOT found in the open scene. Open the Game scene first, or auto-fit will fall back to scale 1 / position 0.",
                        "OK");
                }
            }

            EditorGUILayout.Space(4);
            randomizeTones = EditorGUILayout.Toggle(
                new GUIContent("Randomize tones", "Assign random Normal/Dark/Light tones per level — same as clicking the Wizard's 'Recalculate tones (random)' once. Off = all Normal."),
                randomizeTones);
            using (new EditorGUI.DisabledScope(!randomizeTones))
            {
                toneNormalRatio = EditorGUILayout.Slider("Normal ratio", toneNormalRatio, 0f, 1f);
                toneDarkRatio = EditorGUILayout.Slider("Dark ratio", toneDarkRatio, 0f, Mathf.Max(0f, 1f - toneNormalRatio));
                EditorGUILayout.LabelField($"Light ratio (remainder): {Mathf.Clamp01(1f - toneNormalRatio - toneDarkRatio):0.##}", EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField($"Output → {LevelsDir}/<filename>.asset", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(sourceFolder) || !Directory.Exists(sourceFolder)))
                if (GUILayout.Button("Import all JSONs → LevelData", GUILayout.Height(30)))
                    ImportFolder();

            if (!string.IsNullOrEmpty(log))
            {
                EditorGUILayout.Space(6);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(180));
                EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void ImportFolder()
        {
            var files = Directory.GetFiles(sourceFolder, "*.json", SearchOption.TopDirectoryOnly);
            System.Array.Sort(files);
            EnsureLevelsDir();

            // Resolve the GridArea / GridController ONCE (scene lookups are expensive to repeat).
            autoFitCtx = autoFitToGridArea ? BuildAutoFitContext() : default;
            string fitNote = autoFitToGridArea
                ? (autoFitCtx.valid ? $"Auto-fit ON → '{gridAreaName}'." : $"Auto-fit ON but '{gridAreaName}'/GridController NOT in the open scene → using scale 1 / pos 0.")
                : "Auto-fit OFF → scale 1 / pos 0.";

            var sb = new StringBuilder();
            sb.AppendLine(fitNote);
            sb.AppendLine();
            int made = 0, skipped = 0, failed = 0;

            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string name = SanitizeAssetName(Path.GetFileNameWithoutExtension(file));
                    EditorUtility.DisplayProgressBar("Bulk Import Levels", $"{name}  ({i + 1}/{files.Length})", (float)i / Mathf.Max(1, files.Length));

                    string json;
                    try { json = File.ReadAllText(file); }
                    catch (System.Exception ex) { sb.AppendLine($"✗ {name}: read error — {ex.Message}"); failed++; continue; }

                    if (!LevelJsonImporter.LooksLikeJson(json)) { sb.AppendLine($"– {name}: not a JSON object, skipped"); skipped++; continue; }

                    var parsed = LevelJsonImporter.Parse(json);
                    if (!parsed.Ok || parsed.GridSize <= 0 || parsed.PaletteHex.Count == 0 || string.IsNullOrEmpty(parsed.RleArrayText))
                    { sb.AppendLine($"– {name}: no palette/rle/gridSize (report file?), skipped"); skipped++; continue; }

                    string err = BuildLevelAsset(name, json, parsed);
                    if (err == null) { made++; sb.AppendLine($"✓ {name}  ({parsed.GridSize}×{parsed.GridSize}, {parsed.Columns.Count} cols)"); }
                    else if (err == SkipExists) { skipped++; sb.AppendLine($"– {name}: already exists (overwrite off), skipped"); }
                    else { failed++; sb.AppendLine($"✗ {name}: {err}"); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log = $"Done — created/updated: {made}, skipped: {skipped}, failed: {failed}  (of {files.Length} files)\n\n" + sb;
            Debug.Log("[BulkLevelImport] " + log);
            Repaint();
        }

        private const string SkipExists = "__skip_exists__";

        /// <summary>Returns null on success, <see cref="SkipExists"/> when skipped, or an error message.</summary>
        private string BuildLevelAsset(string name, string json, LevelJsonImporter.Result parsed)
        {
            int gridSize = parsed.GridSize;
            if (!RLECodec.TryDecode(parsed.RleArrayText, gridSize, out int[] cells))
                return "RLE decode failed";

            // Palette → ColorData (preserve null slots so colorIndex stays aligned with rle/columns).
            var palette = new List<ColorData>(parsed.PaletteHex.Count);
            foreach (var hex in parsed.PaletteHex)
            {
                if (string.IsNullOrEmpty(hex)) { palette.Add(null); continue; }
                Color col = ColorUtility.TryParseHtmlString("#" + hex, out var pc) ? pc : Color.magenta;
                palette.Add(LevelEditorWizardWindow.GetOrCreateColorData(hex, col));
            }

            // bombs / keys: image [x, y] (y from top) → flat index (z = gridSize-1-y), matching RLECodec.
            var bombSet = new HashSet<int>();
            foreach (var (bx, by) in parsed.Bombs)
            {
                int idx = CoordToFlat(bx, by, gridSize);
                if (idx >= 0) bombSet.Add(idx);
            }
            var keyMap = new Dictionary<int, int>();
            for (int i = 0; i < parsed.Keys.Count; i++)
            {
                int idx = CoordToFlat(parsed.Keys[i].x, parsed.Keys[i].y, gridSize);
                if (idx >= 0) keyMap[idx] = i + 1; // i-th key → key id i+1 (a lock's "lock":N references it)
            }

            // Create / load the asset.
            string path = $"{LevelsDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (asset != null && !overwriteExisting) return SkipExists;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            // ── Grid ──
            var grid = asset.Grid; // GridData field is initialised on the instance
            LevelEditorWizardWindow.SetField(grid, "size", gridSize);
            // Auto-fit the grid to the scene's GridArea (scale + centre on the filled content), exactly
            // like the Level Wizard's 'Fit grid to GridArea'. Falls back to identity if fit isn't possible.
            Vector3 rootPos = Vector3.zero, rootScale = Vector3.one;
            if (autoFitToGridArea) ComputeFit(autoFitCtx, gridSize, cells, out rootPos, out rootScale);
            LevelEditorWizardWindow.SetField(grid, "rootPosition", rootPos);
            LevelEditorWizardWindow.SetField(grid, "rootScale", rootScale);

            // Tones: random Normal/Dark/Light per colour group — same as one click of the Wizard's
            // 'Recalculate tones (random)'. All-Normal when the toggle is off.
            var toneArr = new Tone[gridSize * gridSize]; // defaults to Tone.Normal (0)
            if (randomizeTones)
            {
                var byColor = new Dictionary<int, List<int>>();
                for (int i = 0; i < cells.Length; i++)
                {
                    int c = cells[i];
                    if (c < 0) continue;
                    if (!byColor.TryGetValue(c, out var lst)) { lst = new List<int>(); byColor[c] = lst; }
                    lst.Add(i);
                }
                ToneDistributor.Distribute(byColor, toneArr, toneNormalRatio, toneDarkRatio, null); // null seed = random
            }

            var boxCells = new List<BoxCellData>();
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int flat = z * gridSize + x;
                    int colorIdx = cells[flat];
                    if (colorIdx < 0 || colorIdx >= palette.Count || palette[colorIdx] == null) continue;
                    var bc = new BoxCellData();
                    LevelEditorWizardWindow.SetField(bc, "gridX", x);
                    LevelEditorWizardWindow.SetField(bc, "gridZ", z);
                    LevelEditorWizardWindow.SetField(bc, "isEmpty", false);
                    LevelEditorWizardWindow.SetField(bc, "color", palette[colorIdx]);
                    LevelEditorWizardWindow.SetField(bc, "tone", toneArr[flat]); // randomized (or Normal if toggle off)
                    LevelEditorWizardWindow.SetField(bc, "isBomb", bombSet.Contains(flat));
                    LevelEditorWizardWindow.SetField(bc, "keyId", keyMap.TryGetValue(flat, out var k) ? k : 0);
                    boxCells.Add(bc);
                }
            }
            LevelEditorWizardWindow.SetField(grid, "cells", boxCells);
            // (toneArr is filled just above the boxCells loop; nothing to do here.)

            // ── Columns (sortColumns → buses). JSON lists top→bottom; ColumnData stores bottom→top. ──
            var columns = new List<ColumnData>();
            if (parsed.Columns != null)
            {
                foreach (var col in parsed.Columns)
                {
                    var shooters = new List<ShooterData>();
                    foreach (var sh in col)
                    {
                        var sd = new ShooterData();
                        if (sh.IsLock)
                        {
                            LevelEditorWizardWindow.SetField(sd, "isLock", true);
                            LevelEditorWizardWindow.SetField(sd, "keyId", sh.KeyId);
                            shooters.Add(sd);
                            continue;
                        }
                        if (sh.ColorIndex < 0 || sh.ColorIndex >= palette.Count || palette[sh.ColorIndex] == null) continue;
                        LevelEditorWizardWindow.SetField(sd, "color", palette[sh.ColorIndex]);
                        LevelEditorWizardWindow.SetField(sd, "shotCount", sh.Count);
                        LevelEditorWizardWindow.SetField(sd, "isSurprise", sh.IsSurprise);
                        LevelEditorWizardWindow.SetField(sd, "linkGroupId", sh.LinkGroupId);
                        shooters.Add(sd);
                    }
                    shooters.Reverse();
                    var cd = new ColumnData();
                    LevelEditorWizardWindow.SetField(cd, "shooters", shooters);
                    columns.Add(cd);
                }
            }
            LevelEditorWizardWindow.SetField(asset, "columns", columns);

            // ── Capacities + source JSON ──
            int conveyor = fallbackConveyorCapacity;
            if (useConfigConveyorWidth)
            {
                var m = ConveyorWidthRx.Match(json);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int cw) && cw > 0) conveyor = cw;
            }
            LevelEditorWizardWindow.SetField(asset, "conveyorSlotCapacity", conveyor);
            LevelEditorWizardWindow.SetField(asset, "reserveSlotCapacity", reserveCapacity);
            LevelEditorWizardWindow.SetField(asset, "sourceJson", json);

            EditorUtility.SetDirty(asset);
            return null;
        }

        // ─── Auto-fit to GridArea (mirrors LevelEditorWizardWindow.ApplyAutoFit) ───
        private struct AutoFitContext
        {
            public bool valid;
            public float cell;          // GridController.CellSize
            public float pSx, pSz;      // gridRoot parent lossyScale (x/z)
            public Vector3 areaSize;    // GridArea world size
            public Vector3 areaCenter;  // GridArea world centre
            public Transform parent;    // gridRoot parent (for world→local)
        }

        // Resolve the scene GridController + GridArea once. Invalid if either is missing.
        private AutoFitContext BuildAutoFitContext()
        {
            var ctx = new AutoFitContext { cell = 1f, pSx = 1f, pSz = 1f };
#if UNITY_2023_1_OR_NEWER
            var gc = Object.FindFirstObjectByType<GridController>(FindObjectsInactive.Include);
#else
            var gc = Object.FindObjectOfType<GridController>();
#endif
            if (gc != null && gc.CellSize > 0.0001f) ctx.cell = gc.CellSize;
            Transform gridRoot = gc != null ? gc.GridRoot : null;
            ctx.parent = gridRoot != null ? gridRoot.parent : null;
            Vector3 pl = ctx.parent != null ? ctx.parent.lossyScale : Vector3.one;
            ctx.pSx = Mathf.Abs(pl.x) < 1e-4f ? 1f : Mathf.Abs(pl.x);
            ctx.pSz = Mathf.Abs(pl.z) < 1e-4f ? 1f : Mathf.Abs(pl.z);

            var area = FindByName(gridAreaName);
            if (gc != null && area != null && TryGetWorldExtents(area, out var sz, out var ctr))
            { ctx.areaSize = sz; ctx.areaCenter = ctr; ctx.valid = true; }
            return ctx;
        }

        // Compute rootScale + rootPosition so the filled content fits + centres inside the GridArea.
        private bool ComputeFit(AutoFitContext ctx, int gridSize, int[] cells, out Vector3 pos, out Vector3 scale)
        {
            pos = Vector3.zero; scale = Vector3.one;
            if (!ctx.valid || gridSize <= 0 || cells == null) return false;
            if (!ContentBounds(cells, gridSize, out int minX, out int maxX, out int minZ, out int maxZ))
            { minX = minZ = 0; maxX = maxZ = gridSize - 1; }

            float contentW = (maxX - minX + 1) * ctx.cell;
            float contentH = (maxZ - minZ + 1) * ctx.cell;
            if (contentW < 1e-4f || contentH < 1e-4f) return false;

            float half = (gridSize - 1) * 0.5f;
            Vector3 contentCenterLocal = new Vector3(
                ((minX + maxX) * 0.5f - half) * ctx.cell, 0f,
                ((minZ + maxZ) * 0.5f - half) * ctx.cell);

            float fill = Mathf.Clamp01(autoFitFill);
            float sByX = ctx.areaSize.x / (ctx.pSx * contentW);
            float sByZ = ctx.areaSize.z / (ctx.pSz * contentH);
            float s = Mathf.Max(0.001f, Mathf.Min(sByX, sByZ) * fill);
            scale = Vector3.one * s;

            Vector3 areaCenterLocal = ctx.parent != null ? ctx.parent.InverseTransformPoint(ctx.areaCenter) : ctx.areaCenter;
            pos = new Vector3(areaCenterLocal.x - s * contentCenterLocal.x, 0f, areaCenterLocal.z - s * contentCenterLocal.z);
            return true;
        }

        private static bool ContentBounds(int[] cells, int gridSize, out int minX, out int maxX, out int minZ, out int maxZ)
        {
            minX = minZ = int.MaxValue; maxX = maxZ = int.MinValue;
            if (cells == null) return false;
            for (int z = 0; z < gridSize; z++)
                for (int x = 0; x < gridSize; x++)
                {
                    if (cells[z * gridSize + x] < 0) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }
            return maxX >= minX && maxZ >= minZ;
        }

        private static GameObject FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var go = GameObject.Find(name);
            if (go != null) return go;
#if UNITY_2023_1_OR_NEWER
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
#else
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
#endif
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static bool TryGetWorldExtents(GameObject go, out Vector3 size, out Vector3 center)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null) { size = rend.bounds.size; center = rend.bounds.center; return true; }
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 ls = mf.sharedMesh.bounds.size;
                Vector3 sc = go.transform.lossyScale;
                size = new Vector3(Mathf.Abs(ls.x * sc.x), Mathf.Abs(ls.y * sc.y), Mathf.Abs(ls.z * sc.z));
                center = go.transform.TransformPoint(mf.sharedMesh.bounds.center);
                return true;
            }
            size = center = Vector3.zero;
            return false;
        }

        private static int CoordToFlat(int x, int y, int gridSize)
        {
            if (x < 0 || x >= gridSize || y < 0 || y >= gridSize) return -1;
            int z = gridSize - 1 - y;
            return z * gridSize + x;
        }

        // Keep asset filenames valid (strip characters Unity/OS dislike).
        private static string SanitizeAssetName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Level";
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Replace('.', '_');
        }

        private static void EnsureLevelsDir()
        {
            if (AssetDatabase.IsValidFolder(LevelsDir)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Game")) AssetDatabase.CreateFolder("Assets", "_Game");
            AssetDatabase.CreateFolder("Assets/_Game", "Levels");
        }
    }
}
#endif
