#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.LevelEditor.EditorTools
{
    /// <summary>
    /// Adds an <b>Export JSON to Desktop</b> button to the LevelData inspector. It writes the level's
    /// stored source JSON (the original designer export kept on <see cref="LevelData.SourceJson"/> at
    /// import) to <c>~/Desktop/&lt;name&gt;.json</c> — the same round-trip the Level Wizard's "Export
    /// level JSON" does, but straight from the asset. Works on a multi-selection (exports each).
    /// </summary>
    [CustomEditor(typeof(LevelData))]
    [CanEditMultipleObjects]
    public class LevelDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            var level = (LevelData)target;
            bool hasJson = level != null && !string.IsNullOrEmpty(level.SourceJson);

            if (!hasJson && targets.Length == 1)
                EditorGUILayout.HelpBox(
                    "No source JSON stored on this level (hand-made, or imported before sourceJson was saved). " +
                    "Re-import it from JSON to enable export.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!AnyHasJson()))
            {
                if (GUILayout.Button(targets.Length > 1
                        ? $"Export {targets.Length} JSONs to Desktop"
                        : "Export JSON to Desktop", GUILayout.Height(28)))
                    ExportSelection();
            }
        }

        private bool AnyHasJson()
        {
            foreach (var o in targets)
                if (o is LevelData l && !string.IsNullOrEmpty(l.SourceJson)) return true;
            return false;
        }

        private void ExportSelection()
        {
            string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);

            int written = 0, skipped = 0;
            string lastPath = null;
            foreach (var o in targets)
            {
                if (!(o is LevelData level) || string.IsNullOrEmpty(level.SourceJson)) { skipped++; continue; }

                string baseName = !string.IsNullOrEmpty(level.LevelName) ? level.LevelName : level.name;
                string file = SanitizeFileName(baseName) + ".json";
                string path = Path.Combine(desktop, file);
                try
                {
                    File.WriteAllText(path, level.SourceJson);
                    written++;
                    lastPath = path;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LevelData] Export failed for '{level.name}': {ex.Message}", level);
                    skipped++;
                }
            }

            if (written > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[LevelData] Exported {written} JSON(s) to Desktop" + (skipped > 0 ? $" ({skipped} skipped)." : "."));
                if (lastPath != null) EditorUtility.RevealInFinder(lastPath); // pop the folder so they see it
            }
            else
            {
                EditorUtility.DisplayDialog("Export JSON",
                    "Nothing exported — the selected level(s) have no stored source JSON.", "OK");
            }
        }

        private static string SanitizeFileName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "level";
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n;
        }
    }
}
#endif
