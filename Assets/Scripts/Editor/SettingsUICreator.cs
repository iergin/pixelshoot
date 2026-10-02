#if UNITY_EDITOR
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Shared editor UI-building primitives (overlay canvas, panels, text, buttons, toggles,
    /// SerializedObject ref wiring) used by the various "Generator/…" creators. The old
    /// "Create Settings Panel UI" menu was removed when Settings became a <c>SettingsPopup</c>
    /// (BasePopup) prefab wired through PopupService instead of a scene-panel controller.
    /// </summary>
    public static class SettingsUICreator
    {
        // ── Shared primitives below ─────────────────────────────────────────
        internal static GameObject GetOrCreateOverlayCanvas(string name, int sortingOrder)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return go;
        }

        internal static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
#else
            var es = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
#endif
            if (es != null) return;
            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        internal static GameObject EnsureFloatingOpenButton(GameObject canvas, GameObject panel,
            string label, Vector2 cornerOffset, TextAlignmentOptions align)
        {
            string name = "Open" + panel.name + "Button";
            var existing = canvas.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var btn = CreateButton(name, canvas.transform, label, 18);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(160f, 64f);
            rt.anchoredPosition = cornerOffset;
            btn.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(true));
            return btn;
        }

        internal static GameObject CreateUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static GameObject CreateText(string name, Transform parent, string text, float size,
            TextAlignmentOptions align, FontStyles style)
        {
            var go = CreateUI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align;
            t.fontStyle = style; t.color = Color.white; t.raycastTarget = false;
            return go;
        }

        internal static GameObject CreateButton(string name, Transform parent, string label, float labelSize)
        {
            var go = CreateUI(name, parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.62f, 0.18f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var txt = CreateText("Label", go.transform, label, labelSize, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchToParent(txt);
            return go;
        }

        internal static GameObject CreateToggle(string name, Transform parent, bool initialOn)
        {
            var go = CreateUI(name, parent);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var toggle = go.AddComponent<Toggle>();

            var check = CreateUI("Checkmark", go.transform);
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.85f, 0.4f, 1f);
            var checkRT = check.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0, 0); checkRT.anchorMax = new Vector2(1, 1);
            checkRT.offsetMin = new Vector2(4, 4); checkRT.offsetMax = new Vector2(-4, -4);

            toggle.targetGraphic = bg;
            toggle.graphic = checkImg;
            toggle.isOn = initialOn;
            return go;
        }

        internal static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        internal static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Generator] No serialized field '{field}' on {so.targetObject.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
#endif
