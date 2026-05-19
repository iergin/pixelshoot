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
        private const string MaterialsDir = "Assets/_Game/Materials";
        private const string LevelsDir = "Assets/_Game/Levels";
        private static readonly Regex HexColorRegex = new Regex("#([0-9A-Fa-f]{6})");

        private LevelEditorController controller;
        private string levelName = "Level_01";
        private string paletteBuffer = "";
        private string rleBuffer = "";
        private int gridSize = 30;
        private int shotsPerShooter = 5;
        private int selectedPaletteIdx = 0;
        private Vector2 scroll;
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
            DrawStep2_Palette();
            DrawSectionBreak();
            DrawStep3_Rle();
            DrawSectionBreak();
            DrawStep4_Tones();
            DrawSectionBreak();
            DrawStep5_Columns();
            DrawSectionBreak();
            DrawSaveLoad();

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
        private void DrawStep2_Palette()
        {
            bool gated = controller == null || controller.targetAsset == null;
            EditorGUILayout.LabelField("Step 2 — Palette", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                EditorGUILayout.LabelField("Paste the PALETTE block (hex list):", EditorStyles.miniLabel);
                paletteBuffer = EditorGUILayout.TextArea(paletteBuffer, GUILayout.MinHeight(60));
                if (GUILayout.Button("Import palette → ColorData")) ImportPalette();
            }
            if (controller != null && controller.palette != null && controller.palette.Count > 0)
            {
                EditorGUILayout.HelpBox($"Palette: {controller.palette.Count} colors loaded.", MessageType.None);
                DrawSwatchRow(false);
            }
        }

        private void ImportPalette()
        {
            var matches = HexColorRegex.Matches(paletteBuffer ?? "");
            if (matches.Count == 0)
            {
                EditorUtility.DisplayDialog("Palette", "No hex colors found (#RRGGBB).", "OK");
                return;
            }
            EnsureDir(ColorsDir);
            EnsureDir(MaterialsDir);

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
            selectedPaletteIdx = 0;
            controller.Rebuild();
            EditorUtility.SetDirty(controller);
        }

        // ─── STEP 3 ───────────────────────────────────────────────────────
        private void DrawStep3_Rle()
        {
            bool gated = controller == null || controller.palette == null || controller.palette.Count == 0;
            EditorGUILayout.LabelField("Step 3 — RLE", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Grid size", GUILayout.Width(70));
                    gridSize = Mathf.Max(1, EditorGUILayout.IntField(gridSize, GUILayout.Width(60)));
                }
                EditorGUILayout.LabelField("Paste the RLE_ROWS block:", EditorStyles.miniLabel);
                rleBuffer = EditorGUILayout.TextArea(rleBuffer, GUILayout.MinHeight(80));
                if (GUILayout.Button("Import RLE → grid")) ImportRle();
            }
            if (controller != null && controller.HasCells)
            {
                int filled = 0;
                foreach (var v in controller.cells) if (v >= 0) filled++;
                EditorGUILayout.HelpBox($"Grid filled: {filled} / {controller.CellCount}", MessageType.None);
            }
        }

        private void ImportRle()
        {
            if (!RLECodec.TryDecode(rleBuffer, gridSize, out var decoded))
            {
                EditorUtility.DisplayDialog("RLE", "Failed to parse RLE text.", "OK");
                return;
            }
            Undo.RecordObject(controller, "Import RLE");
            controller.gridSize = gridSize;
            controller.cells = decoded;
            controller.Rebuild();
            EditorUtility.SetDirty(controller);
        }

        // ─── STEP 4 ───────────────────────────────────────────────────────
        private void DrawStep4_Tones()
        {
            bool gated = controller == null || controller.palette == null || controller.palette.Count == 0;
            EditorGUILayout.LabelField("Step 4 — Tones (optional)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(gated))
            {
                EditorGUILayout.LabelField("Pick a main color:", EditorStyles.miniLabel);
                DrawSwatchRow(true);
                bool selValid = controller != null && controller.palette != null
                                && selectedPaletteIdx >= 0 && selectedPaletteIdx < controller.palette.Count
                                && controller.palette[selectedPaletteIdx] != null;
                using (new EditorGUI.DisabledScope(!selValid))
                {
                    if (GUILayout.Button("Generate 2 tones (darker + lighter) for selected"))
                        GenerateToneVariants(selectedPaletteIdx);
                }
            }
            EditorGUILayout.HelpBox("Tone variants are linked to their main color via the Main Color field. A shooter of the main color will destroy every tone in that group.", MessageType.None);
        }

        // ─── STEP 5 ───────────────────────────────────────────────────────
        private void DrawStep5_Columns()
        {
            bool gated = controller == null || !controller.HasCells;
            EditorGUILayout.LabelField("Step 5 — Columns", EditorStyles.boldLabel);
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

        // ─── Save / Load ──────────────────────────────────────────────────
        private void DrawSaveLoad()
        {
            EditorGUILayout.LabelField("Save / Load", EditorStyles.boldLabel);
            bool gated = controller == null || controller.targetAsset == null;
            using (new EditorGUI.DisabledScope(gated))
            {
                using (new EditorGUILayout.HorizontalScope())
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
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────
        private void DrawSwatchRow(bool selectable)
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
                bool isSelected = selectable && i == selectedPaletteIdx;
                string label = isSelected ? $"[{i}]" : i.ToString();
                if (GUILayout.Button(label, swatchStyle))
                {
                    if (selectable)
                    {
                        selectedPaletteIdx = i;
                    }
                    else
                    {
                        Undo.RecordObject(controller, "Select palette");
                        controller.currentPaletteIdx = i;
                        controller.eraseMode = false;
                        EditorUtility.SetDirty(controller);
                    }
                }
                GUI.backgroundColor = prev;
                if (i % PerRow == PerRow - 1 || i == controller.palette.Count - 1) EditorGUILayout.EndHorizontal();
            }
        }

        private void GenerateToneVariants(int sourceIdx)
        {
            if (sourceIdx < 0 || sourceIdx >= controller.palette.Count) return;
            var main = controller.palette[sourceIdx];
            if (main == null) return;

            Color baseColor = main.DisplayColor;
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            Color darker = Color.HSVToRGB(h, s, Mathf.Clamp01(v * 0.65f));
            Color lighter = Color.HSVToRGB(h, s * 0.7f, Mathf.Clamp01(Mathf.Lerp(v, 1f, 0.45f) + 0.05f));

            EnsureDir(ColorsDir);
            EnsureDir(MaterialsDir);

            foreach (var toneColor in new[] { darker, lighter })
            {
                string hex = ColorUtility.ToHtmlStringRGB(toneColor);
                string path = $"{ColorsDir}/Color_{hex}.asset";
                var tone = AssetDatabase.LoadAssetAtPath<ColorData>(path);
                if (tone == null)
                {
                    tone = ScriptableObject.CreateInstance<ColorData>();
                    SetField(tone, "colorId", hex);
                    SetField(tone, "displayColor", toneColor);
                    SetField(tone, "boxUnhitMaterial", CreateMaterial($"Box_{hex}_Unhit", toneColor));
                    SetField(tone, "boxHitMaterial", CreateMaterial($"Box_{hex}_Hit", FadedVariant(toneColor)));
                    SetField(tone, "shooterMaterial", CreateMaterial($"Shooter_{hex}", toneColor));
                    SetField(tone, "bulletMaterial", CreateMaterial($"Bullet_{hex}", toneColor));
                    AssetDatabase.CreateAsset(tone, path);
                }
                SetField(tone, "mainColor", main);
                EditorUtility.SetDirty(tone);
                if (!controller.palette.Contains(tone)) controller.palette.Add(tone);
            }
            AssetDatabase.SaveAssets();

            Undo.RecordObject(controller, "Generate tones");
            controller.Rebuild();
            EditorUtility.SetDirty(controller);
        }

        private static ColorData GetOrCreateColorData(string hex, Color baseColor)
        {
            string path = $"{ColorsDir}/Color_{hex}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ColorData>(path);
            if (existing != null) return existing;
            var unhit = CreateMaterial($"Box_{hex}_Unhit", baseColor);
            var hit = CreateMaterial($"Box_{hex}_Hit", FadedVariant(baseColor));
            var shooterMat = CreateMaterial($"Shooter_{hex}", baseColor);
            var bulletMat = CreateMaterial($"Bullet_{hex}", baseColor);
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

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
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
