#if UNITY_EDITOR
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click dummy Settings UI. Spawns a Screen-Space-Overlay canvas (or
    /// reuses an existing one), drops a centered panel with a Sound toggle
    /// and a Privacy Policy button, and a floating gear-style open button.
    /// Hooks everything onto a <see cref="SettingsController"/> via SerializedObject.
    /// </summary>
    public static class SettingsUICreator
    {
        private const string CanvasName = "[PixelShoot Settings Canvas]";
        private const string PanelName  = "SettingsPanel";

        [MenuItem("Generator/Create Settings Panel UI")]
        public static void CreateSettingsUI()
        {
            var canvasGo = GetOrCreateOverlayCanvas(CanvasName, sortingOrder: 90);

            // Wipe any old panel to keep the rebuild deterministic.
            var existingPanel = canvasGo.transform.Find(PanelName);
            if (existingPanel != null) Object.DestroyImmediate(existingPanel.gameObject);

            // Dimmed full-screen wrapper.
            var panel = CreateUI(PanelName, canvasGo.transform);
            StretchToParent(panel);
            var dim = panel.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            // Centered card.
            var card = CreateUI("Card", panel.transform);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(520f, 540f);
            cardRT.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.13f, 0.14f, 0.18f, 0.97f);

            // Header: title + close.
            var header = CreateUI("Header", card.transform);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1); headerRT.sizeDelta = new Vector2(0, 64f);

            var title = CreateText("Title", header.transform, "SETTINGS", 26, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchToParent(title);

            var closeGo = CreateButton("CloseButton", header.transform, "✕", 22);
            var closeRT = closeGo.GetComponent<RectTransform>();
            closeRT.anchorMin = closeRT.anchorMax = new Vector2(1, 0.5f);
            closeRT.pivot     = new Vector2(1, 0.5f);
            closeRT.sizeDelta = new Vector2(56f, 56f);
            closeRT.anchoredPosition = new Vector2(-8f, 0f);
            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.onClick.AddListener(() => panel.SetActive(false));

            // Body: vertical layout.
            var body = CreateUI("Body", card.transform);
            var bodyRT = body.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0, 0); bodyRT.anchorMax = new Vector2(1, 1);
            bodyRT.offsetMin = new Vector2(28, 28); bodyRT.offsetMax = new Vector2(-28, -76);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16; vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            // Sound row: label + toggle.
            var soundRow = CreateUI("SoundRow", body.transform);
            soundRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 72f);
            var soundRowImg = soundRow.AddComponent<Image>();
            soundRowImg.color = new Color(1f, 1f, 1f, 0.06f);
            var soundLabelGo = CreateText("SoundLabel", soundRow.transform, "Sound: ON", 20, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            var soundLabelRT = soundLabelGo.GetComponent<RectTransform>();
            soundLabelRT.anchorMin = new Vector2(0, 0); soundLabelRT.anchorMax = new Vector2(0.6f, 1);
            soundLabelRT.offsetMin = new Vector2(16, 0); soundLabelRT.offsetMax = Vector2.zero;
            var soundToggleGo = CreateToggle("SoundToggle", soundRow.transform, true);
            var soundToggleRT = soundToggleGo.GetComponent<RectTransform>();
            soundToggleRT.anchorMin = soundToggleRT.anchorMax = new Vector2(1, 0.5f);
            soundToggleRT.pivot = new Vector2(1, 0.5f);
            soundToggleRT.sizeDelta = new Vector2(80f, 40f);
            soundToggleRT.anchoredPosition = new Vector2(-16f, 0f);

            // Privacy policy button.
            var ppBtnGo = CreateButton("PrivacyPolicyButton", body.transform, "Privacy Policy", 18);
            ppBtnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 72f);

            // Floating gear button outside the panel.
            var gear = EnsureFloatingOpenButton(canvasGo, panel, "⚙ Settings", new Vector2(20f, 20f), TextAlignmentOptions.Center);

            // SettingsController.
            var settings = canvasGo.GetComponent<SettingsController>() ?? canvasGo.AddComponent<SettingsController>();
            var so = new SerializedObject(settings);
            SetRef(so, "panel", panel);
            SetRef(so, "openButton", gear.GetComponent<Button>());
            SetRef(so, "closeButton", closeBtn);
            SetRef(so, "soundToggle", soundToggleGo.GetComponent<Toggle>());
            SetRef(so, "soundLabel", soundLabelGo.GetComponent<TMP_Text>());
            SetRef(so, "privacyPolicyButton", ppBtnGo.GetComponent<Button>());
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(canvasGo);
            Selection.activeObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            Debug.Log("[SettingsUICreator] Settings panel built and wired to SettingsController.");
        }

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
