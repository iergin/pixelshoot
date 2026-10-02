#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click scaffold for the button-driven menu pager: builds a masked <b>Viewport</b> with a
    /// <see cref="MenuPager"/> on it and an empty <b>Content</b> strip inside. Drop your Home / Shop /
    /// Leaderboard pages straight into Content — MenuPager auto-sizes and positions them at runtime.
    /// If a <see cref="NavigationBar"/> exists in the scene it is auto-linked.
    /// </summary>
    public static class MenuPagerCreator
    {
        private const string ViewportName = "MenuPagerViewport";
        private const string ContentName  = "Content";

        [MenuItem("Generator/Create Menu Pager (Viewport + Content)")]
        public static void CreateMenuPager()
        {
            // Parent: the selected RectTransform if it's under a Canvas, else a fresh overlay canvas.
            Transform parent = ResolveParent();

            // Viewport — fills its parent, masks its children, hosts the MenuPager.
            var viewport = SettingsUICreator.CreateUI(ViewportName, parent);
            SettingsUICreator.StretchToParent(viewport);
            viewport.AddComponent<RectMask2D>();
            var pager = viewport.AddComponent<MenuPager>();

            // Content — holds the pages; stretch-fills the viewport. Each page you drop in gets
            // stretch-anchored to fill it (full-screen on any aspect), so no side-by-side strip.
            var content = SettingsUICreator.CreateUI(ContentName, viewport.transform);
            var crt = content.GetComponent<RectTransform>();
            SettingsUICreator.StretchToParent(content);
            crt.pivot = new Vector2(0.5f, 0.5f);

            // Wire the MenuPager (content / viewport / navBar).
            var so = new SerializedObject(pager);
            SettingsUICreator.SetRef(so, "content", crt);
            SettingsUICreator.SetRef(so, "viewport", viewport.GetComponent<RectTransform>());
            var navBar = FindNavBar();
            if (navBar != null) SettingsUICreator.SetRef(so, "navBar", navBar);
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(viewport, "Create Menu Pager");
            Selection.activeObject = content; // select Content so you can drop pages right in
            EditorGUIUtility.PingObject(content);
            Debug.Log($"[MenuPagerCreator] Built '{ViewportName}/{ContentName}'. Drop your pages into Content " +
                      $"IN TAB ORDER (Shop / Home / Leaderboard) — they stack and stretch-fill, no strip. " +
                      $"NavBar {(navBar != null ? "linked" : "NOT found — assign it")}.", content);
        }

        private static Transform ResolveParent()
        {
            var sel = Selection.activeGameObject;
            if (sel != null && sel.GetComponentInParent<Canvas>() != null)
                return sel.transform;
            return SettingsUICreator.GetOrCreateOverlayCanvas("[PixelShoot Menu Canvas]", sortingOrder: 10).transform;
        }

        private static NavigationBar FindNavBar()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<NavigationBar>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<NavigationBar>(true);
#endif
        }
    }
}
#endif
