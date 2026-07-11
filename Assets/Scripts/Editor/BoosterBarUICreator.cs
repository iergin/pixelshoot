#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Boosters;
using PixelShoot.Conveyor;
using PixelShoot.Data;

namespace PixelShoot.EditorTools
{
    /// <summary>Dummy bottom bar of 4 booster buttons, wired to a BoosterManager.</summary>
    public static class BoosterBarUICreator
    {
        private const string CanvasName = "[PixelShoot HUD Canvas]";
        private const string BarName    = "BoosterBar";
        private const string BoostersDir = "Assets/_Game/SO/Boosters";

        [MenuItem("Generator/Create Booster Bar UI")]
        public static void Create()
        {
            var canvasGo = SettingsUICreator.GetOrCreateOverlayCanvas(CanvasName, sortingOrder: 40);
            var existing = canvasGo.transform.Find(BarName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var boosters = LoadBoosters();
            if (boosters.Count == 0)
                Debug.LogWarning($"[BoosterBarUICreator] No BoosterData assets under {BoostersDir}. Buttons will be unassigned.");

            // Manager on the canvas.
            var manager = canvasGo.GetComponent<BoosterManager>() ?? canvasGo.AddComponent<BoosterManager>();
            var soM = new SerializedObject(manager);
            SettingsUICreator.SetRef(soM, "conveyor", FindInScene<ConveyorController>());
            SettingsUICreator.SetRef(soM, "purchasePopup", FindInScene<BoosterPurchaseController>());
            soM.ApplyModifiedProperties();

            // Bottom bar container.
            var bar = SettingsUICreator.CreateUI(BarName, canvasGo.transform);
            var barRT = bar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0f); barRT.anchorMax = new Vector2(0.5f, 0f);
            barRT.pivot = new Vector2(0.5f, 0f);
            barRT.sizeDelta = new Vector2(600f, 150f);
            barRT.anchoredPosition = new Vector2(0f, 20f);

            const int N = 4;
            float spacing = 145f;
            for (int i = 0; i < N; i++)
            {
                var data = i < boosters.Count ? boosters[i] : null;

                var btnGo = SettingsUICreator.CreateUI($"Booster{i}", bar.transform);
                var btnRT = btnGo.GetComponent<RectTransform>();
                btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
                btnRT.pivot = new Vector2(0.5f, 0.5f);
                btnRT.sizeDelta = new Vector2(130f, 130f);
                btnRT.anchoredPosition = new Vector2((i - (N - 1) / 2f) * spacing, 0f);
                var bg = btnGo.AddComponent<Image>();
                bg.color = new Color(0.18f, 0.16f, 0.22f, 0.95f);
                var button = btnGo.AddComponent<Button>();

                // Icon.
                var iconGo = SettingsUICreator.CreateUI("Icon", btnGo.transform);
                SettingsUICreator.StretchToParent(iconGo);
                var iconRT = iconGo.GetComponent<RectTransform>();
                iconRT.offsetMin = new Vector2(14, 14); iconRT.offsetMax = new Vector2(-14, -14);
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                if (data != null && data.Icon != null) icon.sprite = data.Icon;
                else icon.color = new Color(1f, 0.85f, 0.3f, 1f);

                // Count label (bottom-right corner).
                var count = SettingsUICreator.CreateText("Count", btnGo.transform, "x0", 20, TextAlignmentOptions.BottomRight, FontStyles.Bold);
                var countRT = count.GetComponent<RectTransform>();
                countRT.anchorMin = new Vector2(0, 0); countRT.anchorMax = new Vector2(1, 1);
                countRT.offsetMin = new Vector2(0, 2); countRT.offsetMax = new Vector2(-6, 0);
                count.GetComponent<TMP_Text>().raycastTarget = false;

                // Buy badge (top-right "+"), shown when count == 0.
                var badge = SettingsUICreator.CreateText("BuyBadge", btnGo.transform, "+", 26, TextAlignmentOptions.Center, FontStyles.Bold);
                var badgeRT = badge.GetComponent<RectTransform>();
                badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(1, 1);
                badgeRT.pivot = new Vector2(1, 1);
                badgeRT.sizeDelta = new Vector2(34f, 34f);
                badgeRT.anchoredPosition = new Vector2(-2f, -2f);
                var badgeTxt = badge.GetComponent<TMP_Text>();
                badgeTxt.color = new Color(0.3f, 1f, 0.4f, 1f);
                badgeTxt.raycastTarget = false;

                var bb = btnGo.AddComponent<BoosterButton>();
                var so = new SerializedObject(bb);
                SettingsUICreator.SetRef(so, "booster", data);
                SettingsUICreator.SetRef(so, "manager", manager);
                SettingsUICreator.SetRef(so, "button", button);
                SettingsUICreator.SetRef(so, "iconImage", icon);
                SettingsUICreator.SetRef(so, "countLabel", count.GetComponent<TMP_Text>());
                SettingsUICreator.SetRef(so, "buyBadge", badge);
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(canvasGo);
            Selection.activeObject = bar;
            EditorGUIUtility.PingObject(bar);
            Debug.Log("[BoosterBarUICreator] Booster bar built. Assign BoosterManager.flyTarget (the conveyor capacity text) manually.");
        }

        private static List<BoosterData> LoadBoosters()
        {
            var list = new List<BoosterData>();
            if (!AssetDatabase.IsValidFolder(BoostersDir)) return list;
            var guids = AssetDatabase.FindAssets("t:BoosterData", new[] { BoostersDir });
            System.Array.Sort(guids, (a, b) =>
                string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), System.StringComparison.Ordinal));
            foreach (var g in guids)
            {
                var d = AssetDatabase.LoadAssetAtPath<BoosterData>(AssetDatabase.GUIDToAssetPath(g));
                if (d != null) list.Add(d);
            }
            return list;
        }

        private static T FindInScene<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
#endif
