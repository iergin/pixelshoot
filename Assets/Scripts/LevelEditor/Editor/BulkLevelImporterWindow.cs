#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

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

        private Vector2 scroll;
        private string log = "";

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

            var sb = new StringBuilder();
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
            LevelEditorWizardWindow.SetField(grid, "rootPosition", Vector3.zero);
            LevelEditorWizardWindow.SetField(grid, "rootScale", Vector3.one);

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
                    LevelEditorWizardWindow.SetField(bc, "tone", Tone.Normal); // matches a fresh Wizard import
                    LevelEditorWizardWindow.SetField(bc, "isBomb", bombSet.Contains(flat));
                    LevelEditorWizardWindow.SetField(bc, "keyId", keyMap.TryGetValue(flat, out var k) ? k : 0);
                    boxCells.Add(bc);
                }
            }
            LevelEditorWizardWindow.SetField(grid, "cells", boxCells);

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
