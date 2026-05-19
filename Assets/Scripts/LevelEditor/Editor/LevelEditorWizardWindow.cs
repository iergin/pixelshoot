#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.LevelEditor.EditorTools
{
    /// <summary>
    /// Step-by-step level authoring window. Each step unlocks the next once
    /// its prerequisite is satisfied: level asset → palette → RLE → tones → columns.
    /// Save / Load are always available once an asset is bound.
    /// </summary>
    public class LevelEditorWizardWindow : EditorWindow
    {
        private const string ColorsDir = "Assets/_Game/Colors";
        private const string MatBoxesDir = "Assets/_Game/Materials/Boxes";
        private const string MatShootersDir = "Assets/_Game/Materials/Shooters";
        private const string MatBulletsDir = "Assets/_Game/Materials/Bullets";
        private const string LevelsDir = "Assets/_Game/Levels";
        private static readonly Regex HexColorRegex = new Regex("#([0-9A-Fa-f]{6})");

        private LevelEditorController controller;
        private string levelName = "Level_01";
        private string importBuffer = "";
        private int gridSize = 30;
        private int shotsPerShooter = 5;
        private Vector2 scroll;
        private string lastImportStatus = "";
        private static GUIStyle swatchStyle;

        [MenuItem("PixelShoot/Level Editor Wizard")]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWizardWindow>("Level Wizard");
            w.minSize = new Vector2(440, 720);
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Repaint;
        }

        private void OnGUI()
        {
            EnsureController();
            if (swatchStyle == null)
            {
                swatchStyle = new GUIStyle(GUI.skin.button)
                {
                    fixedWidth = 34, fixedHeight = 34, fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("PixelShoot — Level Authoring Wizard", new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            EditorGUILayout.Space(4);

            DrawStep1_Level();
            DrawSectionBreak();
            DrawStep2_Import();
            DrawSectionBreak();
            DrawStep3_Columns();
            DrawSectionBreak();
            DrawSaveLoadClear();

            EditorGUILayout.EndScrollView();
        }

        private void EnsureController()
        {
            if (controller == null)
                controller = FindObjectOfType<LevelEditorController>();
        }

        private static void DrawSectionBreak()
        {
            EditorGUILayout.Space(10);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.2f));
            EditorGUILayout.Space(6);
        }

        // ─── STEP 1 ───────────────────────────────────────────────────────
        private void DrawStep1_Level()
        {
            EditorGUILayout.LabelField("Step 1 — Level", EditorStyles.boldLabel);
            if (controller == null)
            {
                EditorGUILayout.HelpBox("Level Editor scene is not open. Open it to begin.", MessageType.Warning);
                if (GUILayout.Button("Open Level Editor Scene"))
                {
                    LevelEditorSceneSetup.OpenScene();
                    controller = FindObjectOfType<LevelEditorController>();
                }
                return;
            }

            levelName = EditorGUILayout.TextField("Level name", levelName);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create new asset")) CreateLevelAsset();
                if (GUILayout.Button("Load existing…")) LoadLevelAssetPicker();
            }

            if (controller.targetAsset != null)
                EditorGUILayout.HelpBox($"Editing: {controller.targetAsset.name}", MessageType.Info);
            else
                EditorGUILayout.HelpBox("No asset bound yet — create one or load existing.", MessageType.None);
        }

        private void CreateLevelAsset()
        {
            if (string.IsNullOrWhiteSpace(levelName)) return;
            EnsureDir(LevelsDir);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsDir}/{levelName}.asset");
            var asset = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Undo.RecordObject(controller, "Set asset");
            controller.targetAsset = asset;
            EditorUtility.SetDirty(controller);
        }

        private void LoadLevelAssetPicker()
        {
            string absolute = EditorUtility.OpenFilePanel("Load LevelData", LevelsDir, "asset");
            if (string.IsNullOrEmpty(absolute)) return;
            string relative = absolute.StartsWith(Application.dataPath)
                ? "Assets" + absolute.Substring(Application.dataPath.Length)
                : absolute;
            var asset = AssetDatabase.LoadAssetAtPath<LevelData>(relative);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Load", "Could not load a LevelData at that path.", "OK");
                return;
            }
            Undo.RecordObject(controller, "Load asset");
            controller.targetAsset = asset;
            controller.LoadFromAsset();
            EditorUtility.SetDirty(controller);
        }

        // ─── STEP 2 ───────────────────────────────────────────────────────
        private void DrawStep2_Import()
        {
            bool gated = controller == null || controller.targetAsset == null;
            EditorGUILayout.LabelField("Step 2 — Import (palette + RLE in one paste)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                EditorGUILayout.LabelField("Paste the full encoder export (PALETTE and/or RLE_ROWS):", EditorStyles.miniLabel);
                importBuffer = EditorGUILayout.TextArea(importBuffer, GUILayout.MinHeight(160));
                if (GUILayout.Button("Import all (auto-detect)", GUILayout.Height(28))) ImportAll();
            }

            if (!string.IsNullOrEmpty(lastImportStatus))
                EditorGUILayout.HelpBox(lastImportStatus, MessageType.Info);

            if (controller != null && controller.palette != null && controller.palette.Count > 0)
            {
                EditorGUILayout.LabelField("Palette:", EditorStyles.miniLabel);
                DrawSwatchRow(false);
            }
        }

        private void ImportAll()
        {
            string text = importBuffer ?? "";
            int paletteCount = 0;
            int filledCells = 0;
            int detectedGrid = 0;

            // 1) Palette: collect every hex code present (any order; comments tolerated).
            var matches = HexColorRegex.Matches(text);
            if (matches.Count > 0)
            {
                // Per-color subfolders are created inside the helpers below.
                var newPalette = new List<ColorData>(matches.Count);
                foreach (Match m in matches)
                {
                    string hex = m.Groups[1].Value.ToUpperInvariant();
                    Color col = ColorUtility.TryParseHtmlString("#" + hex, out var parsed) ? parsed : Color.magenta;
                    newPalette.Add(GetOrCreateColorData(hex, col));
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Undo.RecordObject(controller, "Import palette");
                controller.palette = newPalette;
                controller.currentPaletteIdx = 0;
                paletteCount = newPalette.Count;
            }

            // 2) Grid size: try to auto-detect from RLE text.
            if (RLECodec.TryDetectGridSize(text, out int detected))
            {
                detectedGrid = detected;
                gridSize = detected;
                controller.gridSize = detected;
            }

            // 3) RLE: decode if any rows are present.
            if (controller.gridSize > 0 && RLECodec.TryDecode(text, controller.gridSize, out var decoded))
            {
                Undo.RecordObject(controller, "Import RLE");
                controller.cells = decoded;
                foreach (var v in decoded) if (v >= 0) filledCells++;
            }

            controller.Rebuild();
            EditorUtility.SetDirty(controller);

            // Status summary
            var parts = new List<string>();
            if (paletteCount > 0) parts.Add($"{paletteCount} colors");
            if (detectedGrid > 0) parts.Add($"{detectedGrid}×{detectedGrid} grid");
            if (filledCells > 0) parts.Add($"{filledCells} filled cells");
            lastImportStatus = parts.Count == 0
                ? "Nothing detected in the pasted text — make sure it contains hex colors and/or an RLE_ROWS array."
                : "Imported: " + string.Join(", ", parts) + ".";
        }

        // ─── STEP 3 ───────────────────────────────────────────────────────
        private void DrawStep3_Columns()
        {
            bool gated = controller == null || !controller.HasCells;
            EditorGUILayout.LabelField("Step 3 — Columns", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Max shots per shooter", GUILayout.Width(160));
                    shotsPerShooter = Mathf.Max(1, EditorGUILayout.IntField(shotsPerShooter, GUILayout.Width(60)));
                }
                if (GUILayout.Button("Auto-generate columns from grid"))
                {
                    Undo.RecordObject(controller, "Auto generate columns");
                    controller.GenerateColumnsFromGrid(shotsPerShooter);
                    EditorUtility.SetDirty(controller);
                }
            }
            if (controller != null && controller.columns != null && controller.columns.Count > 0)
            {
                int sh = 0, shots = 0;
                foreach (var col in controller.columns)
                    foreach (var s in col.Shooters) { sh++; shots += s.ShotCount; }
                EditorGUILayout.HelpBox($"Columns: {controller.columns.Count}, shooters: {sh}, total shots: {shots}", MessageType.None);
            }
        }

        // ─── Save / Load / Clear ──────────────────────────────────────────
        private void DrawSaveLoadClear()
        {
            EditorGUILayout.LabelField("Save / Load / Clear", EditorStyles.boldLabel);
            bool noAsset = controller == null || controller.targetAsset == null;
            bool noController = controller == null;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(noAsset))
                {
                    if (GUILayout.Button("Save to asset", GUILayout.Height(28)))
                    {
                        Undo.RecordObject(controller.targetAsset, "Save level");
                        controller.SaveToAsset();
                        EditorUtility.SetDirty(controller.targetAsset);
                        AssetDatabase.SaveAssets();
                    }
                    if (GUILayout.Button("Load from asset", GUILayout.Height(28)))
                    {
                        Undo.RecordObject(controller, "Load level");
                        controller.LoadFromAsset();
                        EditorUtility.SetDirty(controller);
                    }
                }
                using (new EditorGUI.DisabledScope(noController))
                {
                    if (GUILayout.Button("Clear scene", GUILayout.Height(28)))
                    {
                        if (EditorUtility.DisplayDialog("Clear scene",
                            "This empties the grid (and visuals) in the scene. The asset on disk is not touched until you Save. Continue?",
                            "Yes", "No"))
                        {
                            Undo.RecordObject(controller, "Clear scene");
                            controller.NewGrid();
                            controller.columns?.Clear();
                            importBuffer = "";
                            lastImportStatus = "";
                            EditorUtility.SetDirty(controller);
                        }
                    }
                }
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────
        private void DrawSwatchRow(bool _unused = false)
        {
            if (controller == null || controller.palette == null) return;
            const int PerRow = 10;
            for (int i = 0; i < controller.palette.Count; i++)
            {
                if (i % PerRow == 0) EditorGUILayout.BeginHorizontal();
                var cd = controller.palette[i];
                var col = cd != null ? cd.DisplayColor : Color.magenta;
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = col;
                // Read-only display: clicking a swatch is a no-op, the level is built from RLE only.
                GUILayout.Button(i.ToString(), swatchStyle);
                GUI.backgroundColor = prev;
                if (i % PerRow == PerRow - 1 || i == controller.palette.Count - 1) EditorGUILayout.EndHorizontal();
            }
        }

        private static ColorData GetOrCreateColorData(string hex, Color baseColor)
        {
            // Per-color subfolder for the ScriptableObject too.
            string colorDir = $"{ColorsDir}/{hex}";
            EnsureDir(colorDir);
            string path = $"{colorDir}/Color_{hex}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ColorData>(path);
            if (existing != null) return existing;

            var unhit      = CreateMaterial(MatBoxesDir,    hex, "Unhit",   baseColor);
            var hit        = CreateMaterial(MatBoxesDir,    hex, "Hit",     FadedVariant(baseColor));
            var shooterMat = CreateMaterial(MatShootersDir, hex, "Shooter", baseColor);
            var bulletMat  = CreateMaterial(MatBulletsDir,  hex, "Bullet",  baseColor);

            var cd = ScriptableObject.CreateInstance<ColorData>();
            SetField(cd, "colorId", hex);
            SetField(cd, "displayColor", baseColor);
            SetField(cd, "boxUnhitMaterial", unhit);
            SetField(cd, "boxHitMaterial", hit);
            SetField(cd, "shooterMaterial", shooterMat);
            SetField(cd, "bulletMaterial", bulletMat);
            AssetDatabase.CreateAsset(cd, path);
            return cd;
        }

        /// <summary>baseDir/hex/name.mat — categorised first by usage (boxes/shooters/bullets), then by colour code.</summary>
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

        private static Color FadedVariant(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s *= 0.25f;
            v = Mathf.Lerp(v, 0.85f, 0.5f);
            var faded = Color.HSVToRGB(h, s, v);
            faded.a = c.a;
            return faded;
        }

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
        }
    }
}
#endif
