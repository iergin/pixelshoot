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
        // Throwaway in-memory clone used to feed the scene preview without ever mutating
        // the real bound asset. Never saved to disk; destroyed when the window closes.
        [System.NonSerialized] private LevelData previewAsset;
        [SerializeField] private int gridSize = 30;
        [SerializeField] private Vector3 gridRootPosition = Vector3.zero;
        [SerializeField] private Vector3 gridRootScale = Vector3.one;
        // Auto-fit: on import, scale + centre the grid to fit inside the "GridArea" plane.
        [SerializeField] private bool autoFitOnImport = true;
        [SerializeField] private string gridAreaName = "GridArea";
        [Tooltip("1 = grid touches the GridArea edges; <1 leaves a margin.")]
        [SerializeField] private float autoFitFill = 1f;
        // Fallback target span (world units) used only when no GridArea object is found.
        [SerializeField] private float autoFitWorldSize = 6f;
        [SerializeField] private int conveyorSlotCapacity = 5;
        [SerializeField] private int reserveSlotCapacity = 5;
        [SerializeField] private List<ColorData> palette = new List<ColorData>();
        [SerializeField] private List<ColumnData> columns = new List<ColumnData>();
        [SerializeField] private int[] cells; // -1 = empty; else palette index
        [SerializeField] private byte[] tones; // Tone enum per cell. Parallel to cells[]. 0=Normal,1=Dark,2=Light.
        [SerializeField] private bool[] bombs;  // True at cells flagged as bombs. Parallel to cells[].
        [SerializeField] private int[] keys;    // Key group id per cell (0 = none). Parallel to cells[].
        [SerializeField] private int currentPaletteIdx = 0;
        [SerializeField] private int currentKeyId = 1;
        [System.NonSerialized] private GUIStyle keyLabelStyle; // lazy — draws key ids on the grid canvas
        [SerializeField] private bool eraseMode = false;
        [SerializeField] private bool paintMode = false;
        [SerializeField] private bool bombMode  = false;
        [SerializeField] private bool keyMode   = false;
        [SerializeField] private bool initialPreview = false;
        // True while "Show Final state" is active. Persists across auto-refreshes so the
        // visual doesn't snap back to Locked/Frontier the moment something else triggers a Build.
        [SerializeField] private bool inFinalStateView = false;

        // ─── Transient UI state ───────────────────────────────────────
        private string levelName = "Level_01";
        private string importBuffer = "";
        // The raw designer JSON of the last import — persisted onto the asset (SourceJson)
        // on Save and handed back on Load so it can be copied again.
        private string importedJson = "";
        // shotsPerShooter field removed — columns now come from JSON import (sortColumns).
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
            if (HasCells)
            {
                EnsureTonesArray();
                return;
            }
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
            EnsureTonesArray();
        }

        /// <summary>Keep tones[] AND bombs[] parallel to cells[] without losing any prior assignments.</summary>
        private void EnsureTonesArray()
        {
            if (cells == null) return;
            if (tones == null || tones.Length != cells.Length)
            {
                var old = tones;
                tones = new byte[cells.Length]; // default 0 = Normal
                if (old != null)
                {
                    int min = Mathf.Min(old.Length, tones.Length);
                    for (int i = 0; i < min; i++) tones[i] = old[i];
                }
            }
            if (bombs == null || bombs.Length != cells.Length)
            {
                var old = bombs;
                bombs = new bool[cells.Length];
                if (old != null)
                {
                    int min = Mathf.Min(old.Length, bombs.Length);
                    for (int i = 0; i < min; i++) bombs[i] = old[i];
                }
            }
            if (keys == null || keys.Length != cells.Length)
            {
                var old = keys;
                keys = new int[cells.Length];
                if (old != null)
                {
                    int min = Mathf.Min(old.Length, keys.Length);
                    for (int i = 0; i < min; i++) keys[i] = old[i];
                }
            }
        }

        private Tone GetTone(int idx) => (Tone)(tones != null && idx >= 0 && idx < tones.Length ? tones[idx] : (byte)0);
        private void SetTone(int idx, Tone t)
        {
            EnsureTonesArray();
            if (tones == null || idx < 0 || idx >= tones.Length) return;
            tones[idx] = (byte)t;
        }
        private bool GetBomb(int idx) => bombs != null && idx >= 0 && idx < bombs.Length && bombs[idx];
        private int GetKey(int idx) => keys != null && idx >= 0 && idx < keys.Length ? keys[idx] : 0;

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
            DrawSectionBreak();
            DrawStep_Tones();

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
            // Auto-refresh only rebuilds the scene from a throwaway preview clone. The
            // real bound asset is NEVER touched here — its contents change only when the
            // user explicitly presses Save (which also asks for confirmation).
            RefreshScenePreview(persistToDisk: false);
        }

        private void OnDisable()
        {
            // Drop the throwaway preview clone so it doesn't leak between window sessions.
            if (previewAsset != null) { DestroyImmediate(previewAsset); previewAsset = null; }
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
                // Asset with this name exists → confirm overwrite before touching it.
                bool overwrite = EditorUtility.DisplayDialog(
                    "Üzerine yaz?",
                    $"'{levelName}.asset' zaten mevcut.\n\nİçeriği şu anki wizard durumuyla üzerine yazılsın mı? Bu işlem geri alınamaz.",
                    "Üzerine yaz", "İptal");
                if (!overwrite) { SetStatus("Save iptal edildi — asset değişmedi.", MessageType.Info); return; }
                targetAsset = onDisk;
            }
            else
            {
                // New asset → confirm creation.
                bool create = EditorUtility.DisplayDialog(
                    "Yeni level kaydedilsin mi?",
                    $"'{levelName}.asset' adında yeni bir level oluşturulup\n{LevelsDir} altına kaydedilecek. Emin misiniz?",
                    "Kaydet", "İptal");
                if (!create) { SetStatus("Save iptal edildi — hiçbir asset oluşturulmadı.", MessageType.Info); return; }

                // Create new asset with the requested name (no GenerateUniqueAssetPath —
                // we *want* the exact filename, and we already proved nothing's there).
                var asset = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(asset, path);
                targetAsset = asset;
            }

            SaveToAsset();
            EditorUtility.SetDirty(targetAsset);
            AssetDatabase.SaveAssets();

            // Drop a sidecar .json next to the asset so the source is always an openable
            // file (easy to grab when the blob is huge). Only when we actually have one.
            string jsonMsg = "";
            if (!string.IsNullOrEmpty(importedJson))
            {
                try
                {
                    string jsonPath = $"{LevelsDir}/{levelName}.json";
                    File.WriteAllText(jsonPath, importedJson);
                    AssetDatabase.ImportAsset(jsonPath);
                    jsonMsg = $" (+ {levelName}.json)";
                }
                catch (System.Exception ex) { Debug.LogWarning($"[LevelWizard] Sidecar JSON write failed: {ex.Message}"); }
            }
            SetStatus($"Kaydedildi → '{levelName}.asset'{jsonMsg}.", MessageType.Info);
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
            tones = new byte[CellCount]; // all Normal
            bombs = new bool[CellCount];
            keys = new int[CellCount];
            columns?.Clear();
            palette?.Clear();
            currentPaletteIdx = 0;
            importBuffer = "";
            importedJson = "";
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
            // Force a reimport so Load ALWAYS reflects what's on DISK. AssetDatabase caches the
            // loaded ScriptableObject, and Unity carries live objects across domain reloads, so
            // an asset left dirty in memory by an earlier (unsaved) edit would otherwise come
            // back with phantom locks/changes. Reimport discards that unsaved in-memory state.
            if (System.IO.File.Exists(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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
                EditorGUILayout.LabelField("Paste the level designer JSON (gridSize / palette / rle / sortColumns):", EditorStyles.miniLabel);
                // File-based import/export — the easy path when the JSON is too long to paste.
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("📂 Load JSON from file…", GUILayout.Height(24))) LoadJsonFromFile();
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(importBuffer)))
                        if (GUILayout.Button("💾 Save JSON to file…", GUILayout.Height(24))) SaveJsonToFile();
                }
                importBuffer = EditorGUILayout.TextArea(importBuffer, GUILayout.MinHeight(140));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import all (auto-detect)", GUILayout.Height(28))) ImportAll();
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(importBuffer)))
                    {
                        if (GUILayout.Button("Copy JSON to clipboard", GUILayout.Height(28), GUILayout.Width(200)))
                        {
                            EditorGUIUtility.systemCopyBuffer = importBuffer;
                            SetStatus("JSON panoya kopyalandı.", MessageType.Info);
                        }
                    }
                }
            }
            // Tell the user whether the loaded level carries its original designer JSON.
            if (targetAsset != null)
            {
                if (!string.IsNullOrEmpty(importedJson))
                    EditorGUILayout.HelpBox("Bu level'ın oluşturulduğu JSON yukarıda — seçip ya da 'Copy JSON' ile kopyalayabilirsin.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("Bu asset'te saklı bir kaynak JSON yok (bu özellikten önce oluşturulmuş olabilir). Yeni bir JSON import edip Save'lersen bir dahaki Load'da geri verilir.", MessageType.None);
            }
            if (!string.IsNullOrEmpty(lastImportStatus))
                EditorGUILayout.HelpBox(lastImportStatus, MessageType.Info);
        }

        // Pick a .json file on disk, drop it into the buffer, and import immediately —
        // avoids pasting a huge blob into the text area.
        private void LoadJsonFromFile()
        {
            string start = System.IO.Directory.Exists(LevelsDir) ? LevelsDir : Application.dataPath;
            string path = EditorUtility.OpenFilePanel("Import level JSON", start, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                importBuffer = File.ReadAllText(path);
                ImportAll();
                SetStatus($"JSON dosyadan yüklendi: {System.IO.Path.GetFileName(path)}", MessageType.Info);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Load JSON", $"Dosya okunamadı:\n{ex.Message}", "OK");
            }
        }

        // Write the current JSON buffer out to a .json file you choose.
        private void SaveJsonToFile()
        {
            if (string.IsNullOrEmpty(importBuffer)) return;
            string defaultName = (string.IsNullOrWhiteSpace(levelName) ? "level" : levelName) + ".json";
            string start = System.IO.Directory.Exists(LevelsDir) ? LevelsDir : Application.dataPath;
            string path = EditorUtility.SaveFilePanel("Export level JSON", start, defaultName, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.WriteAllText(path, importBuffer);
                AssetDatabase.Refresh();
                SetStatus($"JSON dosyaya yazıldı: {System.IO.Path.GetFileName(path)}", MessageType.Info);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Save JSON", $"Dosya yazılamadı:\n{ex.Message}", "OK");
            }
        }

        private void ImportAll()
        {
            string text = importBuffer ?? "";
            int paletteCount = 0;
            int filledCells = 0;
            int detectedGrid = 0;
            int columnsCount = 0;
            int shooterCount = 0;

            // ── NEW: structured JSON path (palette + rle + sortColumns in one blob) ──
            bool isJson = LevelJsonImporter.LooksLikeJson(text);
            // Remember the raw JSON so Save can stash it on the asset for later copying.
            if (isJson) importedJson = text;
            LevelJsonImporter.Result parsed = isJson ? LevelJsonImporter.Parse(text) : null;
            string rleText = (parsed != null && !string.IsNullOrEmpty(parsed.RleArrayText)) ? parsed.RleArrayText : text;
            int gridFromJson = parsed != null ? parsed.GridSize : 0;

            // Palette: prefer JSON-ordered list to keep colorIndex stable; fall back to hex regex.
            var paletteHex = new List<string>();
            if (parsed != null && parsed.PaletteHex.Count > 0)
                paletteHex.AddRange(parsed.PaletteHex);
            else
                foreach (Match m in HexColorRegex.Matches(text))
                    paletteHex.Add(m.Groups[1].Value.ToUpperInvariant());

            if (paletteHex.Count > 0)
            {
                var newPalette = new List<ColorData>(paletteHex.Count);
                foreach (var hex in paletteHex)
                {
                    // Preserve null slots (unused palette indices) so later colours keep
                    // their exact colorIndex used by the RLE / sortColumns.
                    if (string.IsNullOrEmpty(hex)) { newPalette.Add(null); continue; }
                    Color col = ColorUtility.TryParseHtmlString("#" + hex, out var pc) ? pc : Color.magenta;
                    newPalette.Add(GetOrCreateColorData(hex, col));
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                palette = newPalette;
                currentPaletteIdx = 0;
                paletteCount = newPalette.Count;
            }

            if (gridFromJson > 0)
            {
                detectedGrid = gridFromJson;
                gridSize = gridFromJson;
            }
            else if (RLECodec.TryDetectGridSize(text, out int detected))
            {
                detectedGrid = detected;
                gridSize = detected;
            }
            EnsureCellsArray();

            if (gridSize > 0 && RLECodec.TryDecode(rleText, gridSize, out var decoded))
            {
                cells = decoded;
                // New cells → tones must be re-randomized. Default to Normal until the user clicks Recalculate.
                tones = new byte[cells.Length];
                bombs = new bool[cells.Length];
                keys = new int[cells.Length];
                foreach (var v in decoded) if (v >= 0) filledCells++;

                // Bombs & keys now come straight from the JSON as [x, y] image coordinates.
                // Map each to the flat grid index (image row y → z = gridSize-1-y, per RLECodec).
                if (parsed != null)
                {
                    foreach (var (bx, by) in parsed.Bombs)
                    {
                        int idx = CoordToFlat(bx, by);
                        if (idx >= 0) bombs[idx] = true;
                    }
                    // The i-th key cell gets key id i+1; a lock's "lock": N references that id.
                    for (int i = 0; i < parsed.Keys.Count; i++)
                    {
                        int idx = CoordToFlat(parsed.Keys[i].x, parsed.Keys[i].y);
                        if (idx >= 0) keys[idx] = i + 1;
                    }
                }
            }

            // Auto-fit AFTER cells are decoded so it measures the filled pixels, not the
            // empty grid. Fine-tune with the scale slider / position later.
            if (autoFitOnImport && gridSize > 0) ApplyAutoFit();

            // Columns straight from JSON (replaces the old auto-generate path).
            if (parsed != null && parsed.Columns != null && parsed.Columns.Count > 0)
            {
                var newColumns = new List<ColumnData>();
                foreach (var col in parsed.Columns)
                {
                    var shooters = new List<ShooterData>();
                    foreach (var sh in col)
                    {
                        var sd = new ShooterData();
                        if (sh.IsLock)
                        {
                            // A lock barrier stack item — no colour / shots.
                            SetField(sd, "isLock", true);
                            SetField(sd, "keyId", sh.KeyId);
                            shooters.Add(sd);
                            continue;
                        }
                        if (sh.ColorIndex < 0 || sh.ColorIndex >= palette.Count) continue;
                        SetField(sd, "color", palette[sh.ColorIndex]);
                        SetField(sd, "shotCount", sh.Count);
                        SetField(sd, "isSurprise", sh.IsSurprise);
                        SetField(sd, "linkGroupId", sh.LinkGroupId);
                        shooters.Add(sd);
                        shooterCount++;
                    }
                    // JSON sortColumns are listed TOP-to-bottom (first entry = the clickable top
                    // bus), but ColumnData stores bottom-to-top (index 0 = bottom, last = top).
                    // Reverse so a surprise/lock authored near the top actually lands near the top
                    // — otherwise a surprise placed last in the JSON would sit on top and auto-
                    // reveal at spawn (via RefreshTop).
                    shooters.Reverse();

                    var cd = new ColumnData();
                    SetField(cd, "shooters", shooters);
                    newColumns.Add(cd);
                }
                columns = newColumns;
                columnsCount = newColumns.Count;
            }

            var parts = new List<string>();
            if (paletteCount > 0) parts.Add($"{paletteCount} colors");
            if (detectedGrid > 0) parts.Add($"{detectedGrid}×{detectedGrid} grid");
            if (filledCells > 0) parts.Add($"{filledCells} filled cells");
            if (columnsCount > 0) parts.Add($"{columnsCount} columns / {shooterCount} shooters");
            if (parsed != null && parsed.Bombs.Count > 0) parts.Add($"{parsed.Bombs.Count} bombs");
            if (parsed != null && parsed.Keys.Count > 0)  parts.Add($"{parsed.Keys.Count} keys");
            lastImportStatus = parts.Count == 0
                ? "Nothing detected in the pasted text — paste the JSON export from the level designer tool."
                : "Imported: " + string.Join(", ", parts) + ". (Press Save to persist to disk.)";
            // No disk write here — the user has to press Save explicitly. Auto-refresh
            // will rebuild the scene from the in-memory asset state.
            pendingPreviewRefresh = true;
            Repaint();
        }

        /// <summary>Map an [x, y] image coordinate (y = row from the top) to the flat cells[]
        /// index, matching RLECodec's orientation (image row y → z = gridSize-1-y). Returns -1
        /// if out of range.</summary>
        private int CoordToFlat(int x, int y)
        {
            if (x < 0 || x >= gridSize || y < 0 || y >= gridSize) return -1;
            int z = gridSize - 1 - y;
            return z * gridSize + x;
        }

        // ─── Grid & painting ─────────────────────────────────────────
        // The GridController's cellSize governs how many world units one cell spans before
        // rootScale. Read it from the scene loader/grid if present; default to 1.
        private float SceneCellSize()
        {
#if UNITY_2023_1_OR_NEWER
            var gc = Object.FindFirstObjectByType<GridController>(FindObjectsInactive.Include);
#else
            var gc = Object.FindObjectOfType<GridController>();
#endif
            return gc != null && gc.CellSize > 0.0001f ? gc.CellSize : 1f;
        }

        /// <summary>
        /// Scale + centre the grid so it fits inside the scene's "GridArea" object.
        /// The grid is square (gridSize × cellSize per side); we fit it to the SMALLER of
        /// the area's world width/depth so it stays fully inside, then centre the root on
        /// the area. Falls back to <see cref="autoFitWorldSize"/> if no GridArea is found.
        /// </summary>
        private void ApplyAutoFit()
        {
            if (gridSize <= 0) return;
            float cell = SceneCellSize();

            // Bounding box of the filled cells; fall back to the full grid if empty.
            if (!GetContentBounds(out int minX, out int maxX, out int minZ, out int maxZ))
            { minX = minZ = 0; maxX = maxZ = gridSize - 1; }

            float contentW = (maxX - minX + 1) * cell;   // local width  of the filled pixels
            float contentH = (maxZ - minZ + 1) * cell;   // local height of the filled pixels
            if (contentW < 1e-4f || contentH < 1e-4f) return;

            // Content centroid in gridRoot-local space (the grid is centred on the root).
            float half = (gridSize - 1) * 0.5f;
            Vector3 contentCenterLocal = new Vector3(
                ((minX + maxX) * 0.5f - half) * cell, 0f,
                ((minZ + maxZ) * 0.5f - half) * cell);

            // Parent scale so world-span math holds even if gridRoot is nested/scaled.
            Transform gridRoot = SceneGridRoot();
            Transform parent = gridRoot != null ? gridRoot.parent : null;
            Vector3 pl = parent != null ? parent.lossyScale : Vector3.one;
            float pSx = Mathf.Abs(pl.x) < 1e-4f ? 1f : Mathf.Abs(pl.x);
            float pSz = Mathf.Abs(pl.z) < 1e-4f ? 1f : Mathf.Abs(pl.z);

            var area = FindGridArea();
            float s;
            if (area != null && TryGetWorldExtents(area, out Vector3 areaSize, out Vector3 areaCenter))
            {
                float fill = Mathf.Clamp01(autoFitFill);
                // Fit the filled content inside the area — whichever axis binds sets the scale.
                float sByX = areaSize.x / (pSx * contentW);
                float sByZ = areaSize.z / (pSz * contentH);
                s = Mathf.Max(0.001f, Mathf.Min(sByX, sByZ) * fill);
                gridRootScale = Vector3.one * s;

                // Position so the CONTENT centre lands on the area centre (X/Z); keep Y.
                Vector3 areaCenterLocal = parent != null ? parent.InverseTransformPoint(areaCenter) : areaCenter;
                gridRootPosition = new Vector3(
                    areaCenterLocal.x - s * contentCenterLocal.x,
                    gridRootPosition.y,
                    areaCenterLocal.z - s * contentCenterLocal.z);

                pendingPreviewRefresh = true;
                SetStatus($"Auto-fit → '{area.name}': content {maxX - minX + 1}×{maxZ - minZ + 1} cells, scale {s:0.###}, centred.", MessageType.Info);
            }
            else
            {
                // No GridArea → fit content to the fixed world-size number, centre on origin.
                s = Mathf.Max(0.001f, autoFitWorldSize / (Mathf.Max(pSx, pSz) * Mathf.Max(contentW, contentH)));
                gridRootScale = Vector3.one * s;
                gridRootPosition = new Vector3(-s * contentCenterLocal.x, gridRootPosition.y, -s * contentCenterLocal.z);
                pendingPreviewRefresh = true;
                SetStatus($"Auto-fit: no '{gridAreaName}' found — used {autoFitWorldSize} units. Scale {s:0.###}.", MessageType.Warning);
            }
        }

        // Bounding box (inclusive) of filled cells. Returns false if the grid is empty.
        private bool GetContentBounds(out int minX, out int maxX, out int minZ, out int maxZ)
        {
            minX = minZ = int.MaxValue; maxX = maxZ = int.MinValue;
            if (cells == null) return false;
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    if (cells[z * gridSize + x] < 0) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }
            }
            return maxX >= minX && maxZ >= minZ;
        }

        private Transform SceneGridRoot()
        {
#if UNITY_2023_1_OR_NEWER
            var gc = Object.FindFirstObjectByType<GridController>(FindObjectsInactive.Include);
#else
            var gc = Object.FindObjectOfType<GridController>();
#endif
            return gc != null ? gc.GridRoot : null;
        }

        // Find the GridArea object by name (searches inactive too).
        private GameObject FindGridArea()
        {
            if (string.IsNullOrEmpty(gridAreaName)) return null;
            var go = GameObject.Find(gridAreaName);
            if (go != null) return go;
#if UNITY_2023_1_OR_NEWER
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
#else
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
#endif
                if (t.name == gridAreaName) return t.gameObject;
            return null;
        }

        // World-space size + centre of the area: prefer the Renderer bounds (works for any
        // plane/quad/mesh), else fall back to a MeshFilter's bounds scaled by the transform.
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
                        tones = new byte[CellCount];
                        bombs = new bool[CellCount];
                        keys = new int[CellCount];
                        pendingPreviewRefresh = true;
                    }
                }
            }
            gridRootPosition = EditorGUILayout.Vector3Field("Grid root position", gridRootPosition);
            // Uniform scale slider — every axis tracks the same value so the grid scales evenly.
            float currentUniform = gridRootScale.x > 0.0001f ? gridRootScale.x : 1f;
            float newUniform = EditorGUILayout.Slider("Grid root scale", currentUniform, 0.01f, 10f);
            if (!Mathf.Approximately(newUniform, currentUniform))
                gridRootScale = Vector3.one * newUniform;

            // Auto-fit: scale + centre the grid to fit inside the scene's GridArea plane.
            // The scale slider / position above are then just fine-tuning.
            using (new EditorGUILayout.HorizontalScope())
            {
                gridAreaName = EditorGUILayout.TextField(
                    new GUIContent("Fit target (scene object)", "Name of the scene object (e.g. a plane) the grid is scaled + centred to fit inside."),
                    gridAreaName, GUILayout.Width(320));
                autoFitFill = EditorGUILayout.Slider(new GUIContent("Fill", "1 = touch the edges; <1 leaves a margin."), autoFitFill, 0.5f, 1f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Fit grid to '{gridAreaName}'", GUILayout.Width(220))) ApplyAutoFit();
                autoFitOnImport = GUILayout.Toggle(autoFitOnImport, "auto on import", GUILayout.Width(120));
                EditorGUILayout.LabelField(new GUIContent("(fallback units)", "Used only if no such object exists in the scene."), GUILayout.Width(100));
                autoFitWorldSize = EditorGUILayout.FloatField(autoFitWorldSize, GUILayout.Width(60));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // Paint, Erase, Bomb and Key are mutually exclusive — toggling one off-by-default
                // disables clicks entirely, so accidental drags over the grid do nothing.
                bool newPaint = GUILayout.Toggle(paintMode, "Paint", "Button", GUILayout.Width(64));
                if (newPaint != paintMode)
                {
                    paintMode = newPaint;
                    if (paintMode) { eraseMode = false; bombMode = false; keyMode = false; }
                }
                bool newErase = GUILayout.Toggle(eraseMode, "Erase", "Button", GUILayout.Width(64));
                if (newErase != eraseMode)
                {
                    eraseMode = newErase;
                    if (eraseMode) { paintMode = false; bombMode = false; keyMode = false; }
                }
                bool newBomb = GUILayout.Toggle(bombMode, "Bomb", "Button", GUILayout.Width(64));
                if (newBomb != bombMode)
                {
                    bombMode = newBomb;
                    if (bombMode) { paintMode = false; eraseMode = false; keyMode = false; }
                }
                bool newKey = GUILayout.Toggle(keyMode, "Key", "Button", GUILayout.Width(50));
                if (newKey != keyMode)
                {
                    keyMode = newKey;
                    if (keyMode) { paintMode = false; eraseMode = false; bombMode = false; }
                }
                bool newPrev = GUILayout.Toggle(initialPreview, "Init", "Button", GUILayout.Width(50));
                if (newPrev != initialPreview) initialPreview = newPrev;
                string statusLabel;
                if (eraseMode) statusLabel = "Click cells to clear them.";
                else if (paintMode) statusLabel = $"Painting palette idx {currentPaletteIdx}.";
                else if (bombMode) statusLabel = "Click cells to toggle bomb flag.";
                else if (keyMode) statusLabel = $"Click filled cells to assign key id {currentKeyId} (click again to clear).";
                else statusLabel = "No tool active — clicks do nothing.";
                EditorGUILayout.LabelField(statusLabel);
            }
            if (keyMode)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Key id to paint", GUILayout.Width(100));
                    currentKeyId = Mathf.Max(1, EditorGUILayout.IntField(currentKeyId, GUILayout.Width(50)));
                    EditorGUILayout.LabelField("(matches a lock barrier's key id)", EditorStyles.miniLabel);
                }
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

            // Cells — tinted by the cell's tone so the designer can iterate visually.
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int flat = z * gridSize + x;
                    int colorIdx = cells[flat];
                    if (colorIdx < 0) continue;
                    Color c = (colorIdx < palette.Count && palette[colorIdx] != null)
                        ? palette[colorIdx].DisplayColor
                        : Color.magenta;
                    if (initialPreview && !IsCellOnSilhouette(x, z))
                        c = new Color(0.42f, 0.42f, 0.46f);
                    c = ToneShifter.Apply(c, GetTone(flat));
                    var rect = CellRect(gridRect, x, z, cellPx);
                    EditorGUI.DrawRect(rect, c);
                    // Bomb marker: a black dot in the middle so the designer can spot them at a glance.
                    if (GetBomb(flat))
                    {
                        float dotSize = Mathf.Max(2f, cellPx * 0.5f);
                        float pad = (cellPx - dotSize) * 0.5f;
                        EditorGUI.DrawRect(new Rect(rect.x + pad, rect.y + pad, dotSize, dotSize),
                            new Color(0f, 0f, 0f, 0.85f));
                    }
                    // Key marker: a yellow border + the key id drawn in the cell so the designer
                    // can read which group each cell belongs to (and match it to a lock's key id).
                    int cellKey = GetKey(flat);
                    if (cellKey > 0)
                    {
                        Color keyCol = new Color(1f, 0.85f, 0.1f, 1f);
                        float th = Mathf.Max(1f, cellPx * 0.18f);
                        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, th), keyCol);
                        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - th, rect.width, th), keyCol);
                        EditorGUI.DrawRect(new Rect(rect.x, rect.y, th, rect.height), keyCol);
                        EditorGUI.DrawRect(new Rect(rect.xMax - th, rect.y, th, rect.height), keyCol);
                        if (cellPx >= 12f) // only when the cell is big enough to read
                        {
                            if (keyLabelStyle == null)
                                keyLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                                {
                                    alignment = TextAnchor.MiddleCenter,
                                    normal = { textColor = Color.black }
                                };
                            GUI.Label(rect, cellKey.ToString(), keyLabelStyle);
                        }
                    }
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

            // Painting (LMB down/drag). Requires Paint, Erase, Bomb or Key to be active and
            // not be in Initial preview mode (which is read-only).
            var e = Event.current;
            bool toolActive = paintMode || eraseMode || bombMode || keyMode;
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
                    if (bombMode)
                    {
                        // Bomb tool: toggle the flag (only on filled cells — empty cells can't bomb).
                        if (cells[idx] >= 0 && e.type == EventType.MouseDown)
                        {
                            EnsureTonesArray();
                            bombs[idx] = !bombs[idx];
                            pendingPreviewRefresh = true;
                            e.Use();
                            Repaint();
                        }
                    }
                    else if (keyMode)
                    {
                        // Key tool: assign currentKeyId; clicking a cell that already has it clears it.
                        if (cells[idx] >= 0 && e.type == EventType.MouseDown)
                        {
                            EnsureTonesArray();
                            keys[idx] = keys[idx] == currentKeyId ? 0 : currentKeyId;
                            pendingPreviewRefresh = true;
                            e.Use();
                            Repaint();
                        }
                    }
                    else
                    {
                        int newColor = eraseMode ? -1 : currentPaletteIdx;
                        if (cells[idx] != newColor)
                        {
                            cells[idx] = newColor;
                            // Erased cells lose their bomb + key flags too.
                            if (newColor < 0)
                            {
                                EnsureTonesArray();
                                if (bombs != null) bombs[idx] = false;
                                if (keys != null) keys[idx] = 0;
                            }
                            pendingPreviewRefresh = true;
                            e.Use();
                            Repaint();
                        }
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

        // Matches the runtime GridController.RecomputeFrontier: a cell is shootable iff at least
        // one of the 4 cardinal rays reaches the grid edge with no filled cell in the way (a
        // straight lane). An interior empty pocket doesn't make a buried cell shootable.
        private bool IsCellOnSilhouette(int x, int z)
        {
            return CellLaneClear(x, z, 1, 0) || CellLaneClear(x, z, -1, 0)
                || CellLaneClear(x, z, 0, 1) || CellLaneClear(x, z, 0, -1);
        }

        private bool CellLaneClear(int x, int z, int dx, int dz)
        {
            for (int cx = x + dx, cz = z + dz; cx >= 0 && cx < gridSize && cz >= 0 && cz < gridSize; cx += dx, cz += dz)
                if (cells[cz * gridSize + cx] >= 0) return false; // a filled cell blocks the lane
            return true;
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

            // Locks are barriers, not buses: they don't shoot, so they must NOT count toward the
            // bullet budget (otherwise every lock's placeholder shotCount reads as a phantom shot).
            int totalShots = 0;
            if (columns != null)
                foreach (var col in columns)
                    if (col != null && col.Shooters != null)
                        foreach (var s in col.Shooters)
                            if (!s.IsLock) totalShots += s.ShotCount;

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
                    "Re-import the JSON with the correct sortColumns or hand-edit them until they match.",
                    MessageType.Error);
            }
        }

        // ─── Columns & capacities ─────────────────────────────────────
        private void DrawStep_Columns()
        {
            EditorGUILayout.LabelField("Columns & capacities", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Columns are imported from the level JSON's 'sortColumns' field — there's no auto-generator anymore. " +
                "Re-import the JSON to update them.",
                MessageType.None);
            conveyorSlotCapacity = Mathf.Max(1, EditorGUILayout.IntField("Conveyor capacity", conveyorSlotCapacity));
            reserveSlotCapacity = Mathf.Max(1, EditorGUILayout.IntField("Reserve capacity", reserveSlotCapacity));
            DrawBulletBudgetValidation();
            if (columns != null && columns.Count > 0)
            {
                int buses = 0, locks = 0, shots = 0;
                foreach (var col in columns)
                    foreach (var s in col.Shooters)
                        if (s.IsLock) locks++;
                        else { buses++; shots += s.ShotCount; }
                EditorGUILayout.HelpBox(
                    $"Columns: {columns.Count}, buses: {buses}, locks: {locks}, total bus shots: {shots}",
                    MessageType.None);
            }

            DrawLockKeyValidation();
            DrawShooterDetailEditor();
        }

        /// <summary>
        /// Always-visible integrity check for the lock ↔ key pairing (NOT behind the per-shooter
        /// foldout). A lock opens only when a key with the SAME id is collected, and a key is only
        /// consumed (flies + reveals its boxes) when its lock opens — so the ids must match up:
        ///   • lock with keyId ≤ 0        → can never open (misconfigured)
        ///   • lock keyId with no key cell → key never collected → lock never opens
        ///   • key cell with no lock       → key never consumed → its hidden boxes never reveal
        /// </summary>
        private void DrawLockKeyValidation()
        {
            if (columns == null) return;

            int zeroKeyLocks = 0;
            var lockIds = new HashSet<int>();
            foreach (var col in columns)
                if (col?.Shooters != null)
                    foreach (var s in col.Shooters)
                        if (s.IsLock)
                        {
                            if (s.KeyId <= 0) zeroKeyLocks++;
                            else lockIds.Add(s.KeyId);
                        }

            var keyIds = new HashSet<int>();
            if (keys != null) foreach (var k in keys) if (k > 0) keyIds.Add(k);

            if (zeroKeyLocks == 0 && lockIds.Count == 0 && keyIds.Count == 0)
                return; // this level uses no locks/keys — nothing to check

            if (zeroKeyLocks > 0)
                EditorGUILayout.HelpBox(
                    $"🔒 {zeroKeyLocks} lock(s) have key id ≤ 0 — a lock needs a POSITIVE key id or it can never open. " +
                    "Set each lock's key id in the per-shooter editor to match its key cells.",
                    MessageType.Error);

            var orphanLocks = new List<int>();
            foreach (var id in lockIds) if (!keyIds.Contains(id)) orphanLocks.Add(id);
            if (orphanLocks.Count > 0)
                EditorGUILayout.HelpBox(
                    $"🔒 Lock(s) waiting on key id(s) {string.Join(", ", orphanLocks)} but NO key cells are painted with those ids. " +
                    "Paint matching key cells (Key tool) or those locks can never open.",
                    MessageType.Error);

            var orphanKeys = new List<int>();
            foreach (var id in keyIds) if (!lockIds.Contains(id)) orphanKeys.Add(id);
            if (orphanKeys.Count > 0)
                EditorGUILayout.HelpBox(
                    $"🔑 Key cells with id(s) {string.Join(", ", orphanKeys)} have NO lock waiting on them — " +
                    "the key is never collected, so its hidden boxes never reveal. Add a lock with that key id, " +
                    "or fix the ids so lock and key match.",
                    MessageType.Error);

            if (zeroKeyLocks == 0 && orphanLocks.Count == 0 && orphanKeys.Count == 0)
                EditorGUILayout.HelpBox(
                    $"✓ Lock/Key OK — key id(s) {string.Join(", ", lockIds)} matched on both locks and key cells.",
                    MessageType.Info);
        }

        [SerializeField] private bool showShooterEditor;

        /// <summary>
        /// Per-shooter editor for the imported columns: tweak count, flip the surprise
        /// flag, and set link group ids by hand — without re-exporting JSON. Edits write
        /// straight onto the ShooterData objects via reflection (same path as import).
        /// </summary>
        private void DrawShooterDetailEditor()
        {
            if (columns == null || columns.Count == 0) return;

            showShooterEditor = EditorGUILayout.Foldout(showShooterEditor, "Per-shooter editor (surprise / link / count)", true);
            if (!showShooterEditor) return;

            EditorGUILayout.HelpBox(
                "Rows are shown top-to-bottom — the FIRST row in each column is the TOP (clickable) bus. " +
                "Surprise = spawns hidden until it surfaces. Link group id > 0 ties buses together (≥2 per id); 0 = unlinked.",
                MessageType.None);

            // Link-group integrity summary.
            var linkCounts = new Dictionary<int, int>();
            foreach (var col in columns)
                foreach (var s in col.Shooters)
                    if (s.LinkGroupId > 0)
                        linkCounts[s.LinkGroupId] = linkCounts.TryGetValue(s.LinkGroupId, out var n) ? n + 1 : 1;
            if (linkCounts.Count > 0)
            {
                var bad = new List<int>();
                foreach (var kv in linkCounts) if (kv.Value < 2) bad.Add(kv.Key);
                if (bad.Count > 0)
                    EditorGUILayout.HelpBox($"Link group(s) with a single member (need ≥2): {string.Join(", ", bad)}.", MessageType.Warning);
            }

            // (Lock/Key integrity is validated in DrawLockKeyValidation, shown always — not behind
            // this foldout — so a broken lock↔key pairing can't hide.)

            // Deferred structural edits — applied AFTER the draw loop so we never mutate a
            // List while enumerating it. Only one button can fire per GUI frame.
            int pendingCol = -1, pendingInsertAt = -1, pendingRemoveAt = -1;

            for (int ci = 0; ci < columns.Count; ci++)
            {
                var col = columns[ci];
                if (col == null || col.Shooters == null) continue;
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Column {ci}  ({col.Shooters.Count} buses, top→bottom)", EditorStyles.miniBoldLabel);
                    // Insert a lock at the very top of this column.
                    if (GUILayout.Button("+ Lock (top)", GUILayout.Width(90)))
                    {
                        pendingCol = ci;
                        pendingInsertAt = col.Shooters.Count; // append = topmost
                    }
                }

                // Draw TOP (last data index) FIRST so the list reads top-to-bottom like the
                // real column. The underlying data order (index 0 = bottom) is unchanged.
                for (int si = col.Shooters.Count - 1; si >= 0; si--)
                {
                    var sd = col.Shooters[si];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isLock = sd.IsLock;

                        // Swatch: lock badge or bus colour.
                        var prev = GUI.color;
                        GUI.color = isLock ? new Color(0.85f, 0.72f, 0.2f) : (sd.Color != null ? sd.Color.DisplayColor : Color.magenta);
                        GUILayout.Box(new GUIContent(isLock ? "L" : ""), GUILayout.Width(18), GUILayout.Height(18));
                        GUI.color = prev;

                        bool isTop = si == col.Shooters.Count - 1;
                        EditorGUILayout.LabelField(isTop ? $"[{si}] TOP" : $"[{si}]", GUILayout.Width(64));

                        // Lock toggle — turns this stack item into a barrier (no colour/shots).
                        bool newIsLock = GUILayout.Toggle(isLock, "Lock", "Button", GUILayout.Width(50));
                        if (newIsLock != isLock) SetField(sd, "isLock", newIsLock);

                        if (newIsLock)
                        {
                            EditorGUILayout.LabelField("key", GUILayout.Width(28));
                            // Clamp to ≥1: a lock must wait on a real key (id 0 would open instantly).
                            // This also auto-heals any existing keyId-0 lock the moment its row draws.
                            int newKey = Mathf.Max(1, EditorGUILayout.IntField(sd.KeyId, GUILayout.Width(40)));
                            if (newKey != sd.KeyId) SetField(sd, "keyId", newKey);
                        }
                        else
                        {
                            // Count.
                            EditorGUILayout.LabelField("shots", GUILayout.Width(36));
                            int newCount = Mathf.Max(1, EditorGUILayout.IntField(sd.ShotCount, GUILayout.Width(48)));
                            if (newCount != sd.ShotCount) SetField(sd, "shotCount", newCount);

                            // Surprise.
                            bool newSurprise = GUILayout.Toggle(sd.IsSurprise, "Surprise", "Button", GUILayout.Width(80));
                            if (newSurprise != sd.IsSurprise) SetField(sd, "isSurprise", newSurprise);

                            // Link group id.
                            EditorGUILayout.LabelField("link", GUILayout.Width(30));
                            int newLink = Mathf.Max(0, EditorGUILayout.IntField(sd.LinkGroupId, GUILayout.Width(40)));
                            if (newLink != sd.LinkGroupId) SetField(sd, "linkGroupId", newLink);
                        }

                        GUILayout.FlexibleSpace();

                        // Insert a NEW lock item into the gap above / below this row (data
                        // index si is bottom-relative; "above" in the column = higher index).
                        if (GUILayout.Button(new GUIContent("+L↑", "Insert a lock ABOVE this item"), GUILayout.Width(34)))
                        {
                            pendingCol = ci;
                            pendingInsertAt = si + 1;
                        }
                        if (GUILayout.Button(new GUIContent("+L↓", "Insert a lock BELOW this item"), GUILayout.Width(34)))
                        {
                            pendingCol = ci;
                            pendingInsertAt = si;
                        }
                        if (GUILayout.Button(new GUIContent("✕", "Remove this item"), GUILayout.Width(24)))
                        {
                            pendingCol = ci;
                            pendingRemoveAt = si;
                        }
                    }
                }
            }

            // Apply the one deferred structural edit, then rebuild the preview.
            if (pendingCol >= 0 && pendingCol < columns.Count &&
                columns[pendingCol].Shooters is List<ShooterData> backing)
            {
                if (pendingRemoveAt >= 0 && pendingRemoveAt < backing.Count)
                {
                    backing.RemoveAt(pendingRemoveAt);
                    pendingPreviewRefresh = true;
                }
                else if (pendingInsertAt >= 0 && pendingInsertAt <= backing.Count)
                {
                    var lockSd = new ShooterData();
                    SetField(lockSd, "isLock", true);
                    SetField(lockSd, "keyId", 1); // a lock needs a real (>0) key id; default 1, edit per-row
                    backing.Insert(pendingInsertAt, lockSd);
                    pendingPreviewRefresh = true;
                }
            }
        }

        // ─── Tones (per-cell light / normal / dark variation) ─────────
        // Default ratios — kept editable in the inspector via two number fields. Light is
        // implicit (1 - normal - dark) so they always sum to 1.
        [SerializeField, Range(0f, 1f)] private float toneNormalRatio = ToneDistributor.DefaultNormalRatio;
        [SerializeField, Range(0f, 1f)] private float toneDarkRatio   = ToneDistributor.DefaultDarkRatio;

        private void DrawStep_Tones()
        {
            EditorGUILayout.LabelField("Tones (per-cell variation)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                toneNormalRatio = EditorGUILayout.Slider("Normal ratio", toneNormalRatio, 0f, 1f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                toneDarkRatio = EditorGUILayout.Slider("Dark ratio", toneDarkRatio, 0f, Mathf.Max(0f, 1f - toneNormalRatio));
            }
            float lightRatio = Mathf.Clamp01(1f - toneNormalRatio - toneDarkRatio);
            EditorGUILayout.LabelField($"Light ratio (residual): {lightRatio:F3}", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(!HasCells))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Recalculate tones (random)", GUILayout.Height(28)))
                    {
                        RecalculateTones(useFixedSeed: false);
                        pendingPreviewRefresh = true;
                    }
                    if (GUILayout.Button("Reset to Normal", GUILayout.Width(140), GUILayout.Height(28)))
                    {
                        EnsureCellsArray();
                        if (tones != null) for (int i = 0; i < tones.Length; i++) tones[i] = 0;
                        pendingPreviewRefresh = true;
                    }
                }
            }

            if (HasCells) DrawToneSummary();
        }

        /// <summary>
        /// Per-color rounding + shuffle (see <see cref="ToneDistributor"/>). Each color
        /// group gets exactly `round(N*normal)` Normal cells, `round(N*dark)` Dark cells,
        /// and the rest Light — never over- nor under-shoots.
        /// </summary>
        private void RecalculateTones(bool useFixedSeed)
        {
            EnsureCellsArray();
            if (tones == null) return;

            // Group flat indices by palette color index (skip empties = -1).
            var byColor = new Dictionary<int, List<int>>();
            for (int i = 0; i < cells.Length; i++)
            {
                int c = cells[i];
                if (c < 0) continue;
                if (!byColor.TryGetValue(c, out var list)) { list = new List<int>(); byColor[c] = list; }
                list.Add(i);
            }

            // Wipe before assigning so unused indices are deterministic.
            for (int i = 0; i < tones.Length; i++) tones[i] = (byte)Tone.Normal;

            var tonesArr = new Tone[tones.Length];
            ToneDistributor.Distribute(byColor, tonesArr, toneNormalRatio, toneDarkRatio,
                useFixedSeed ? 12345 : (int?)null);
            for (int i = 0; i < tones.Length; i++) tones[i] = (byte)tonesArr[i];
        }

        private void DrawToneSummary()
        {
            int normal = 0, dark = 0, light = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] < 0) continue;
                switch ((Tone)tones[i])
                {
                    case Tone.Normal: normal++; break;
                    case Tone.Dark:   dark++;   break;
                    case Tone.Light:  light++;  break;
                }
            }
            int total = normal + dark + light;
            if (total == 0) return;
            EditorGUILayout.HelpBox(
                $"Tone distribution — Normal: {normal} ({(float)normal/total:P1}), " +
                $"Dark: {dark} ({(float)dark/total:P1}), " +
                $"Light: {light} ({(float)light/total:P1}). Total: {total}.",
                MessageType.None);
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

            // IMPORTANT: never mutate the real bound asset here. Build the scene from a
            // THROWAWAY clone of the wizard state so the ScriptableObject's contents only
            // ever change when the user explicitly presses Save.
            if (previewAsset == null)
            {
                previewAsset = ScriptableObject.CreateInstance<LevelData>();
                previewAsset.hideFlags = HideFlags.HideAndDontSave;
            }
            WriteInto(previewAsset);
            // persistToDisk is intentionally ignored now — the preview never writes the
            // real asset to disk; only SmartSave does.

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

            // Bind the PREVIEW clone on the loader for the build (restored to the real
            // asset before we return, so the scene never holds the temp reference).
            SetField(loader, "levelData", previewAsset);
            Debug.Log("[LevelWizard] RefreshScenePreview: bound preview clone on loader.");

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
                SetField(loader, "levelData", targetAsset); // restore real ref
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
            foreach (var c in previewAsset.Grid.Cells) if (!c.IsEmpty) dataCells++;
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

            // Restore the loader's reference to the real asset — the temp clone was only
            // needed to feed Build() without dirtying the bound ScriptableObject.
            SetField(loader, "levelData", targetAsset);

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
            for (int i = 0; i < tones.Length; i++) tones[i] = 0; // reset to Normal
            if (bombs != null) for (int i = 0; i < bombs.Length; i++) bombs[i] = false;
            if (keys != null) for (int i = 0; i < keys.Length; i++) keys[i] = 0;
            foreach (var bc in targetAsset.Grid.Cells)
            {
                if (bc.IsEmpty) continue;
                int idx = palette.IndexOf(bc.Color);
                if (idx < 0) { palette.Add(bc.Color); idx = palette.Count - 1; }
                if (bc.GridX < 0 || bc.GridX >= gridSize || bc.GridZ < 0 || bc.GridZ >= gridSize) continue;
                int flat = bc.GridZ * gridSize + bc.GridX;
                cells[flat] = idx;
                tones[flat] = (byte)bc.Tone;
                if (bombs != null && flat < bombs.Length) bombs[flat] = bc.IsBomb;
                if (keys != null && flat < keys.Length) keys[flat] = bc.KeyId;
            }
            // DEEP copy — the wizard must edit its own ColumnData/ShooterData instances, never
            // the asset's. A shallow copy shares those objects, so unsaved edits (adding a lock,
            // changing shots) mutate the bound asset in memory and survive a reload-without-save.
            columns = CloneColumns(targetAsset.Columns);
            conveyorSlotCapacity = targetAsset.ConveyorSlotCapacity;
            reserveSlotCapacity = targetAsset.ReserveSlotCapacity;
            // Hand back the original designer JSON (if the level was imported from one) so
            // it shows in the Import box and can be copied again.
            importedJson = targetAsset.SourceJson ?? "";
            if (!string.IsNullOrEmpty(importedJson)) importBuffer = importedJson;
            int filled = 0;
            if (cells != null) foreach (var v in cells) if (v >= 0) filled++;
            Debug.Log($"[LevelWizard] LoadFromAsset done. palette={palette.Count}, columns={columns.Count}, " +
                      $"conveyorCap={conveyorSlotCapacity}, reserveCap={reserveSlotCapacity}, filledCells={filled}");
            pendingPreviewRefresh = true;
            Repaint();
        }

        // Persist the wizard state into the REAL bound asset (only from SmartSave).
        private void SaveToAsset() => WriteInto(targetAsset);

        /// <summary>
        /// Deep-clones a list of ColumnData (and their nested ShooterData) via JsonUtility so
        /// the wizard's working copy is fully independent of the source. UnityEngine.Object
        /// fields (e.g. ShooterData.color → a ColorData asset) are kept as the SAME reference,
        /// which is what we want — only the plain data containers are duplicated.
        /// </summary>
        private static List<ColumnData> CloneColumns(IEnumerable<ColumnData> src)
        {
            var list = new List<ColumnData>();
            if (src == null) return list;
            foreach (var c in src)
                list.Add(c == null ? new ColumnData()
                                   : JsonUtility.FromJson<ColumnData>(JsonUtility.ToJson(c)));
            return list;
        }

        /// <summary>
        /// Serialize the current wizard state into <paramref name="dest"/>. Used for both
        /// the real bound asset (on Save) and a throwaway preview clone (on auto-refresh),
        /// so the scene preview never has to mutate the real ScriptableObject.
        /// </summary>
        private void WriteInto(LevelData dest)
        {
            if (dest == null) return;
            EnsureCellsArray();
            // Never write a zero scale — that would make the runtime grid invisible.
            if (gridRootScale.sqrMagnitude < 0.0001f) gridRootScale = Vector3.one;

            var grid = dest.Grid;
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
                    int flat = z * gridSize + x;
                    SetField(bc, "tone", (Tone)tones[flat]);
                    SetField(bc, "isBomb", bombs != null && flat < bombs.Length && bombs[flat]);
                    SetField(bc, "keyId", keys != null && flat < keys.Length ? keys[flat] : 0);
                    boxCells.Add(bc);
                }
            }
            SetField(grid, "cells", boxCells);

            // DEEP copy on the way out too, so the destination asset owns independent objects
            // and later wizard edits don't leak back into it (matters for the real asset on Save;
            // harmless for the throwaway preview clone).
            SetField(dest, "columns", CloneColumns(columns));
            SetField(dest, "conveyorSlotCapacity", conveyorSlotCapacity);
            SetField(dest, "reserveSlotCapacity", reserveSlotCapacity);
            // Preserve the original designer JSON on the asset. Only overwrite when we
            // actually have one, so manual edits + re-save don't wipe a previously stored
            // blob.
            if (!string.IsNullOrEmpty(importedJson)) SetField(dest, "sourceJson", importedJson);
        }

        // GenerateColumnsFromGrid removed — columns now come from JSON's sortColumns field.

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
