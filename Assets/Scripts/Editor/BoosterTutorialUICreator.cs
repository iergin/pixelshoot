#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Boosters;
using PixelShoot.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>Dummy booster-tutorial UI: a dark spotlight overlay + an instruction panel,
    /// wired to a BoosterTutorialController. The booster button is raised above the dark at
    /// runtime; the world-space conveyor text stays lit via a spotlight hole.</summary>
    public static class BoosterTutorialUICreator
    {
        private const string CanvasName = "[PixelShoot Tutorial Canvas]";
        private const string RootName   = "BoosterTutorial";
        private const string OverlayMatPath = "Assets/_Game/Materials/M_SpotlightOverlay.mat";

        [MenuItem("Generator/Create Booster Tutorial UI")]
        public static void Create()
        {
            var canvasGo = SettingsUICreator.GetOrCreateOverlayCanvas(CanvasName, sortingOrder: 150);
            var existing = canvasGo.transform.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = SettingsUICreator.CreateUI(RootName, canvasGo.transform);
            SettingsUICreator.StretchToParent(root);

            // Dark spotlight overlay (blocks everything below; the button is raised above it at
            // runtime, and the world conveyor text is kept lit via a hole). RawImage renders a
            // full 0..1-UV quad without needing a sprite — required by the spotlight shader.
            var dark = SettingsUICreator.CreateUI("DarkOverlay", root.transform);
            SettingsUICreator.StretchToParent(dark);
            var darkImg = dark.AddComponent<RawImage>();
            darkImg.color = Color.white; // shader supplies the dark color
            var overlayMat = AssetDatabase.LoadAssetAtPath<Material>(OverlayMatPath);
            if (overlayMat != null) darkImg.material = overlayMat;
            else Debug.LogWarning($"[BoosterTutorialUICreator] Missing {OverlayMatPath}; overlay will render plain white.");

            var spotlight = dark.AddComponent<SpotlightOverlay>();
            var soSpot = new SerializedObject(spotlight);
            SettingsUICreator.SetRef(soSpot, "cam", ResolveGameCamera());
            // Hole target = the world-space conveyor capacity text's RectTransform.
            var holeTarget = ResolveConveyorTextRect();
            if (holeTarget != null)
            {
                var holesProp = soSpot.FindProperty("holes");
                holesProp.arraySize = 1;
                holesProp.GetArrayElementAtIndex(0).objectReferenceValue = holeTarget;
            }
            else
            {
                Debug.LogWarning("[BoosterTutorialUICreator] Could not find the conveyor text; assign SpotlightOverlay.holes manually.");
            }
            soSpot.ApplyModifiedProperties();
            dark.SetActive(false);

            // Instruction panel (created AFTER the overlay → above it).
            var panel = SettingsUICreator.CreateUI("TutorialPanel", root.transform);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f); panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(680f, 260f);
            panelRT.anchoredPosition = new Vector2(0f, 120f);
            panel.AddComponent<Image>().color = new Color(0.13f, 0.11f, 0.17f, 0.98f);
            var text = SettingsUICreator.CreateText("Text", panel.transform, "Tap the booster to use it!", 26, TextAlignmentOptions.Center, FontStyles.Bold);
            SettingsUICreator.StretchToParent(text);
            var textRT = text.GetComponent<RectTransform>();
            textRT.offsetMin = new Vector2(24, 24); textRT.offsetMax = new Vector2(-24, -24);
            panel.SetActive(false);

            var ctl = canvasGo.GetComponent<BoosterTutorialController>() ?? canvasGo.AddComponent<BoosterTutorialController>();
            var so = new SerializedObject(ctl);
            SettingsUICreator.SetRef(so, "manager", FindInScene<BoosterManager>());
            SettingsUICreator.SetRef(so, "darkOverlay", dark);
            SettingsUICreator.SetRef(so, "tutorialPanel", panel);
            SettingsUICreator.SetRef(so, "tutorialText", text.GetComponent<TMP_Text>());
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(canvasGo);
            Selection.activeObject = ctl;
            EditorGUIUtility.PingObject(ctl);
            Debug.Log("[BoosterTutorialUICreator] Tutorial UI built. Re-run 'Create Booster Bar UI' so buttons pick up the tutorial reference.");
        }

        private static T FindInScene<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        /// <summary>Camera the world text is drawn through — prefer the tagged MainCamera.</summary>
        private static Camera ResolveGameCamera()
        {
            if (Camera.main != null) return Camera.main;
            return FindInScene<Camera>();
        }

        /// <summary>RectTransform of the world-space conveyor capacity text (the hole target).</summary>
        private static RectTransform ResolveConveyorTextRect()
        {
            var hud = FindInScene<PixelShoot.UI.ConveyorCapacityHUD>();
            if (hud == null) return null;
            var so = new SerializedObject(hud);
            var label = so.FindProperty("label")?.objectReferenceValue as Component;
            return label != null ? label.GetComponent<RectTransform>() : null;
        }
    }
}
#endif
