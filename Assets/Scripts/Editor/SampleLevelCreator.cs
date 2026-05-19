#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.EditorTools
{
    public static class SampleLevelCreator
    {
        private const string BaseDir = "Assets/_Game";
        private const string ColorsDir = BaseDir + "/Colors";
        private const string MaterialsDir = BaseDir + "/Materials";
        private const string LevelsDir = BaseDir + "/Levels";

        [MenuItem("PixelShoot/Create Sample Level Data")]
        public static void CreateSampleLevel()
        {
            EnsureDir(BaseDir);
            EnsureDir(ColorsDir);
            EnsureDir(MaterialsDir);
            EnsureDir(LevelsDir);

            var red = CreateColor("Red", new Color(0.95f, 0.25f, 0.25f));
            var blue = CreateColor("Blue", new Color(0.25f, 0.5f, 0.95f));
            var green = CreateColor("Green", new Color(0.3f, 0.85f, 0.4f));

            var level = ScriptableObject.CreateInstance<LevelData>();
            ConfigureLevel(level, red, blue, green);

            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsDir}/Level_01.asset");
            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
            Debug.Log($"Sample level created at {path}");
        }

        private static ColorData CreateColor(string name, Color baseColor)
        {
            var existingPath = $"{ColorsDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ColorData>(existingPath);
            if (existing != null) return existing;

            // Unhit (Frontier) = vivid base color. Hit = faded — destroyed boxes fade out.
            var unhit = CreateMaterial($"Box_{name}_Unhit", baseColor);
            var hit = CreateMaterial($"Box_{name}_Hit", FadedVariant(baseColor));
            var shooterMat = CreateMaterial($"Shooter_{name}", baseColor);
            var bulletMat = CreateMaterial($"Bullet_{name}", baseColor);

            var color = ScriptableObject.CreateInstance<ColorData>();
            SetField(color, "colorId", name.ToLowerInvariant());
            SetField(color, "displayColor", baseColor);
            SetField(color, "boxUnhitMaterial", unhit);
            SetField(color, "boxHitMaterial", hit);
            SetField(color, "shooterMaterial", shooterMat);
            SetField(color, "bulletMaterial", bulletMat);

            AssetDatabase.CreateAsset(color, existingPath);
            return color;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // Try URP/Lit first, fall back to Standard
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Desaturated + pulled toward light gray, so the unhit state reads as "ghost" of the real color.
        private static Color FadedVariant(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s *= 0.25f;
            v = Mathf.Lerp(v, 0.85f, 0.5f);
            var faded = Color.HSVToRGB(h, s, v);
            faded.a = c.a;
            return faded;
        }

        private static void ConfigureLevel(LevelData level, ColorData red, ColorData blue, ColorData green)
        {
            // 7x7 grid with 12 boxes laid along the bottom-right L-shape.
            // 4 red + 4 blue + 4 green = 12 boxes.
            var grid = new GridData();
            SetField(grid, "size", 7);
            SetField(grid, "rootScale", Vector3.one);

            var cells = new List<BoxCellData>();
            void Add(int x, int z, ColorData c) => cells.Add(MakeCell(x, z, c));

            // Red: bottom row left half
            Add(0, 0, red); Add(1, 0, red); Add(2, 0, red); Add(3, 0, red);
            // Blue: bottom row right + corner climb
            Add(4, 0, blue); Add(5, 0, blue); Add(6, 0, blue); Add(6, 1, blue);
            // Green: right edge upper
            Add(6, 2, green); Add(6, 3, green); Add(6, 4, green); Add(6, 5, green);

            SetField(grid, "cells", cells);
            SetField(level, "grid", grid);

            // 3 columns x 2 shooters each, shotCount=2 -> 12 bullets matches 12 boxes.
            var columns = new List<ColumnData>
            {
                MakeColumn(red, red),
                MakeColumn(blue, blue),
                MakeColumn(green, green),
            };
            SetField(level, "columns", columns);

            SetField(level, "conveyorSlotCapacity", 5);
            SetField(level, "reserveSlotCapacity", 5);
        }

        private static BoxCellData MakeCell(int x, int z, ColorData c)
        {
            var cell = new BoxCellData();
            SetField(cell, "gridX", x);
            SetField(cell, "gridZ", z);
            SetField(cell, "isEmpty", false);
            SetField(cell, "color", c);
            return cell;
        }

        private static ColumnData MakeColumn(params ColorData[] shooterColors)
        {
            var col = new ColumnData();
            var shooters = new List<ShooterData>();
            foreach (var c in shooterColors)
            {
                var s = new ShooterData();
                SetField(s, "color", c);
                SetField(s, "shotCount", 2);
                shooters.Add(s);
            }
            SetField(col, "shooters", shooters);
            return col;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(dir);
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
