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
    [CustomEditor(typeof(LevelEditorController))]
    public class LevelEditorControllerInspector : Editor
    {
        private string rleBuffer = "";
        private string paletteBuffer = "";
        private bool showRle = false;
        private bool showPaletteImport = false;
        private int shotsPerShooter = 5;
        private static GUIStyle SwatchStyle;

        private static readonly Regex HexColorRegex = new Regex("#([0-9A-Fa-f]{6})");
        private const string ColorsDir = "Assets/_Game/Colors";
        private const string MaterialsDir = "Assets/_Game/Materials";

        public override void OnInspectorGUI()
        {
            var c = (LevelEditorController)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Painting", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newErase = GUILayout.Toggle(c.eraseMode, "Erase", "Button");
                if (newErase != c.eraseMode)
                {
                    Undo.RecordObject(c, "Toggle erase");
                    c.eraseMode = newErase;
                }
                EditorGUILayout.LabelField(c.eraseMode
                    ? "Click cells to clear them."
                    : $"Painting palette idx {c.currentPaletteIdx}.");
            }

            DrawPaletteSwatches(c);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New (clear)"))
                {
                    if (EditorUtility.DisplayDialog("Clear grid", "Clear the current grid?", "Yes", "No"))
                    {
                        Undo.RecordObject(c, "Clear grid");
                        c.NewGrid();
                        EditorUtility.SetDirty(c);
                    }
                }
                if (GUILayout.Button("Rebuild visuals"))
                {
                    c.Rebuild();
                }
                if (GUILayout.Button("Resize → keep cells"))
                {
                    Undo.RecordObject(c, "Resize grid");
                    c.EnsureCellsArray();
                    c.Rebuild();
                    EditorUtility.SetDirty(c);
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Shooters / Columns", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Max shots per shooter", GUILayout.Width(160));
                shotsPerShooter = EditorGUILayout.IntField(shotsPerShooter, GUILayout.Width(60));
                if (shotsPerShooter < 1) shotsPerShooter = 1;
                if (GUILayout.Button("Auto generate columns"))
                {
                    Undo.RecordObject(c, "Auto generate columns");
                    c.GenerateColumnsFromGrid(shotsPerShooter);
                    EditorUtility.SetDirty(c);
                }
            }
            if (c.columns != null && c.columns.Count > 0)
            {
                int totalShooters = 0, totalShots = 0;
                foreach (var col in c.columns)
                    foreach (var sh in col.Shooters) { totalShooters++; totalShots += sh.ShotCount; }
                EditorGUILayout.HelpBox($"Columns: {c.columns.Count}, shooters: {totalShooters}, total shots: {totalShots}", MessageType.None);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Asset", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(c.targetAsset == null))
                {
                    if (GUILayout.Button("Load from asset"))
                    {
                        Undo.RecordObject(c, "Load level");
                        c.LoadFromAsset();
                        EditorUtility.SetDirty(c);
                    }
                    if (GUILayout.Button("Save to asset"))
                    {
                        Undo.RecordObject(c.targetAsset, "Save level");
                        c.SaveToAsset();
                        EditorUtility.SetDirty(c.targetAsset);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            if (c.targetAsset == null)
                EditorGUILayout.HelpBox("Assign a LevelDataRLE asset to enable Load/Save. Create one via Create → PixelShoot → Level Data RLE.", MessageType.Info);

            EditorGUILayout.Space(8);
            showRle = EditorGUILayout.Foldout(showRle, "RLE Import / Export", true);
            if (showRle)
            {
                EditorGUILayout.LabelField("Paste RLE_ROWS block (or copy from grid):");
                rleBuffer = EditorGUILayout.TextArea(rleBuffer, GUILayout.MinHeight(80));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import → grid"))
                    {
                        if (RLECodec.TryDecode(rleBuffer, c.gridSize, out var decoded))
                        {
                            Undo.RecordObject(c, "Import RLE");
                            c.cells = decoded;
                            c.Rebuild();
                            EditorUtility.SetDirty(c);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("RLE", "Failed to parse RLE.", "OK");
                        }
                    }
                    if (GUILayout.Button("Export grid → text + copy"))
                    {
                        c.EnsureCellsArray();
                        rleBuffer = RLECodec.Encode(c.cells, c.gridSize);
                        EditorGUIUtility.systemCopyBuffer = rleBuffer;
                    }
                }
            }

            EditorGUILayout.Space(8);
            showPaletteImport = EditorGUILayout.Foldout(showPaletteImport, "Palette Import (hex list)", true);
            if (showPaletteImport)
            {
                EditorGUILayout.LabelField("Paste a PALETTE block like:\n  \"#110F12\",  // 0\n  \"#FEFEFE\",  // 1\n  ...", EditorStyles.miniLabel, GUILayout.MinHeight(48));
                paletteBuffer = EditorGUILayout.TextArea(paletteBuffer, GUILayout.MinHeight(80));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import palette → ColorData"))
                    {
                        ImportPaletteFromText(c, paletteBuffer);
                    }
                    if (GUILayout.Button("Clear field"))
                    {
                        paletteBuffer = "";
                    }
                }
                EditorGUILayout.HelpBox("Creates / reuses ColorData assets at Assets/_Game/Colors/Color_<HEX>.asset (with URP/Lit materials) and sets the controller palette. Order follows the order in the pasted text.", MessageType.Info);
            }

            // Stats
            EditorGUILayout.Space(8);
            if (c.HasCells)
            {
                int filled = 0;
                foreach (var v in c.cells) if (v >= 0) filled++;
                EditorGUILayout.HelpBox($"Cells filled: {filled} / {c.CellCount}", MessageType.None);
            }
        }

        private void ImportPaletteFromText(LevelEditorController c, string text)
        {
            var matches = HexColorRegex.Matches(text ?? "");
            if (matches.Count == 0)
            {
                EditorUtility.DisplayDialog("Palette", "No hex colors found (expected #RRGGBB).", "OK");
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

            Undo.RecordObject(c, "Import palette");
            c.palette = newPalette;
            c.currentPaletteIdx = Mathf.Clamp(c.currentPaletteIdx, 0, Mathf.Max(0, newPalette.Count - 1));
            c.Rebuild(); // refresh visuals using new materials
            EditorUtility.SetDirty(c);

            Debug.Log($"Palette import: created/reused {newPalette.Count} ColorData entries.");
        }

        private static ColorData GetOrCreateColorData(string hex, Color baseColor)
        {
            string path = $"{ColorsDir}/Color_{hex}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ColorData>(path);
            if (existing != null) return existing;

            // Unhit (Frontier) = vivid base color. Hit = faded — destroyed boxes fade out.
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

        // Desaturated + pulled toward light gray, so the unhit state reads as a "ghost" of the real color.
        private static Color FadedVariant(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s *= 0.25f;
            v = Mathf.Lerp(v, 0.85f, 0.5f);
            var faded = Color.HSVToRGB(h, s, v);
            faded.a = c.a;
            return faded;
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

        private void DrawPaletteSwatches(LevelEditorController c)
        {
            if (SwatchStyle == null)
            {
                SwatchStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter,
                    fixedWidth = 36,
                    fixedHeight = 36
                };
            }

            if (c.palette == null || c.palette.Count == 0)
            {
                EditorGUILayout.HelpBox("Palette is empty. Add ColorData entries to the palette list, or load an asset that has them.", MessageType.Warning);
                return;
            }

            const int PerRow = 8;
            for (int i = 0; i < c.palette.Count; i++)
            {
                if (i % PerRow == 0) EditorGUILayout.BeginHorizontal();
                var cd = c.palette[i];
                var col = cd != null ? cd.DisplayColor : Color.magenta;
                bool selected = (i == c.currentPaletteIdx && !c.eraseMode);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = col;
                string label = selected ? $"[{i}]" : i.ToString();
                if (GUILayout.Button(label, SwatchStyle))
                {
                    Undo.RecordObject(c, "Select palette");
                    c.currentPaletteIdx = i;
                    c.eraseMode = false;
                }
                GUI.backgroundColor = prev;
                if (i % PerRow == PerRow - 1 || i == c.palette.Count - 1) EditorGUILayout.EndHorizontal();
            }
        }

        private void OnSceneGUI()
        {
            var c = (LevelEditorController)target;
            var e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

            bool isPaintEvent = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                                && e.button == 0 && !e.alt && !e.shift && !e.control && !e.command;
            if (!isPaintEvent) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float planeY = c.gridRoot != null ? c.gridRoot.position.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (!plane.Raycast(ray, out float dist)) return;
            Vector3 worldPoint = ray.GetPoint(dist);
            if (!c.TryWorldToCell(worldPoint, out int x, out int z)) return;

            Undo.RecordObject(c, "Paint cell");
            c.Paint(x, z);
            EditorUtility.SetDirty(c);
            e.Use();
        }
    }
}
#endif
