using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Quick "switch scene" buttons on the main toolbar (by the Play controls), built with Unity 6.3's
    /// OFFICIAL <see cref="MainToolbarElement"/> API — no reflection, so Unity doesn't flag or hide them.
    ///
    /// <para>One button per scene in Build Settings (in order); if none are set, every .unity in the
    /// project (alphabetical). Click opens it (prompting to save unsaved changes first); the active
    /// scene is marked with ● and its button disabled; all buttons are disabled in Play mode.</para>
    ///
    /// <para><b>Placement:</b> <see cref="MainToolbarDockPosition.Middle"/> = beside Play. Switch to
    /// <c>.Left</c> or <c>.Right</c> if you'd rather dock it elsewhere.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class SceneQuickSwitchToolbar
    {
        private const string ElementId = "PixelShoot/SceneSwitch";

        static SceneQuickSwitchToolbar()
        {
            // Rebuild the buttons (labels / enabled state) whenever the open scene or play state changes.
            EditorSceneManager.sceneOpened += (_, __) => Refresh();
            EditorSceneManager.newSceneCreated += (_, __, ___) => Refresh();
            EditorApplication.playModeStateChanged += _ => Refresh();
        }

        private static void Refresh() => MainToolbar.Refresh(ElementId);

        [MainToolbarElement(ElementId, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static IEnumerable<MainToolbarElement> SceneButtons()
        {
            string activePath = SceneManager.GetActiveScene().path;
            bool playing = EditorApplication.isPlaying;

            foreach (var (name, path) in GetScenes())
            {
                bool isActive = path == activePath;
                string label = isActive ? $"● {name}" : name;
                string scenePath = path; // capture for the click closure

                yield return new MainToolbarButton(new MainToolbarContent(label, path), () => OpenScene(scenePath))
                {
                    enabled = !playing && !isActive, // can't reopen the current scene, and not in Play mode
                };
            }
        }

        // Build Settings scenes (enabled, in order); fall back to every scene asset in the project.
        private static List<(string name, string path)> GetScenes()
        {
            var list = new List<(string, string)>();

            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && !string.IsNullOrEmpty(s.path))
                    list.Add((Path.GetFileNameWithoutExtension(s.path), s.path));

            if (list.Count == 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Assets/"))
                        list.Add((Path.GetFileNameWithoutExtension(path), path));
                }
                list = list.OrderBy(e => e.Item1).ToList();
            }
            return list;
        }

        private static void OpenScene(string path)
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
