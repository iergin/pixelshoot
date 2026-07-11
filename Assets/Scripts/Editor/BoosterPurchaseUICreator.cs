#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Boosters;
using PixelShoot.Conveyor;
using PixelShoot.Shop;

namespace PixelShoot.EditorTools
{
    /// <summary>Dummy "buy this booster" popup wired to a BoosterPurchaseController.</summary>
    public static class BoosterPurchaseUICreator
    {
        private const string CanvasName = "[PixelShoot Promo Canvas]";
        private const string PanelName  = "BoosterPurchasePanel";

        [MenuItem("Generator/Create Booster Purchase Panel UI")]
        public static void Create()
        {
            var canvasGo = SettingsUICreator.GetOrCreateOverlayCanvas(CanvasName, sortingOrder: 130);
            var existing = canvasGo.transform.Find(PanelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // Dim full-screen root (toggled on/off).
            var panel = SettingsUICreator.CreateUI(PanelName, canvasGo.transform);
            SettingsUICreator.StretchToParent(panel);
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // Card.
            var card = SettingsUICreator.CreateUI("Card", panel.transform);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(600f, 620f);
            cardRT.anchoredPosition = Vector2.zero;
            card.AddComponent<Image>().color = new Color(0.13f, 0.11f, 0.17f, 0.98f);

            // Icon.
            var iconGo = SettingsUICreator.CreateUI("Icon", card.transform);
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 1f);
            iconRT.pivot = new Vector2(0.5f, 1f);
            iconRT.sizeDelta = new Vector2(160f, 160f);
            iconRT.anchoredPosition = new Vector2(0f, -30f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(1f, 0.85f, 0.3f, 1f);
            iconImg.preserveAspect = true;

            // Title.
            var title = SettingsUICreator.CreateText("Title", card.transform, "BOOSTER", 32, TextAlignmentOptions.Center, FontStyles.Bold);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1); titleRT.sizeDelta = new Vector2(0, 60f);
            titleRT.anchoredPosition = new Vector2(0f, -210f);

            // Description.
            var desc = SettingsUICreator.CreateText("Description", card.transform, "What this booster does.", 20, TextAlignmentOptions.Center, FontStyles.Normal);
            var descRT = desc.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.08f, 0.35f); descRT.anchorMax = new Vector2(0.92f, 0.6f);
            descRT.offsetMin = Vector2.zero; descRT.offsetMax = Vector2.zero;

            // Coin buy button.
            var coinGo = SettingsUICreator.CreateButton("BuyWithCoinsButton", card.transform, "Buy  500", 22);
            var coinRT = coinGo.GetComponent<RectTransform>();
            coinRT.anchorMin = new Vector2(0.1f, 0.18f); coinRT.anchorMax = new Vector2(0.9f, 0.3f);
            coinRT.offsetMin = Vector2.zero; coinRT.offsetMax = Vector2.zero;
            coinGo.GetComponent<Image>().color = new Color(0.95f, 0.78f, 0.2f, 1f);
            var coinCost = coinGo.GetComponentInChildren<TMP_Text>();

            // Ad button.
            var adGo = SettingsUICreator.CreateButton("WatchAdButton", card.transform, "Watch Ad", 22);
            var adRT = adGo.GetComponent<RectTransform>();
            adRT.anchorMin = new Vector2(0.1f, 0.04f); adRT.anchorMax = new Vector2(0.9f, 0.16f);
            adRT.offsetMin = Vector2.zero; adRT.offsetMax = Vector2.zero;
            adGo.GetComponent<Image>().color = new Color(0.3f, 0.72f, 0.4f, 1f);

            // Close (X) top-right.
            var closeGo = SettingsUICreator.CreateButton("CloseButton", card.transform, "X", 22);
            var closeRT = closeGo.GetComponent<RectTransform>();
            closeRT.anchorMin = closeRT.anchorMax = new Vector2(1, 1);
            closeRT.pivot = new Vector2(1, 1);
            closeRT.sizeDelta = new Vector2(64f, 64f);
            closeRT.anchoredPosition = new Vector2(-8f, -8f);

            // Controller on the always-active canvas, toggling the panel child.
            var ctl = canvasGo.GetComponent<BoosterPurchaseController>() ?? canvasGo.AddComponent<BoosterPurchaseController>();
            var so = new SerializedObject(ctl);
            SettingsUICreator.SetRef(so, "panel", panel);
            SettingsUICreator.SetRef(so, "titleLabel", title.GetComponent<TMP_Text>());
            SettingsUICreator.SetRef(so, "descriptionLabel", desc.GetComponent<TMP_Text>());
            SettingsUICreator.SetRef(so, "iconImage", iconImg);
            SettingsUICreator.SetRef(so, "adButton", adGo.GetComponent<Button>());
            SettingsUICreator.SetRef(so, "coinButton", coinGo.GetComponent<Button>());
            SettingsUICreator.SetRef(so, "coinCostLabel", coinCost);
            SettingsUICreator.SetRef(so, "closeButton", closeGo.GetComponent<Button>());
            SettingsUICreator.SetRef(so, "shop", FindInScene<ShopManager>());
            SettingsUICreator.SetRef(so, "conveyor", FindInScene<ConveyorController>());
            so.ApplyModifiedProperties();

            panel.SetActive(false);
            EditorUtility.SetDirty(canvasGo);
            Selection.activeObject = ctl;
            EditorGUIUtility.PingObject(ctl);
            Debug.Log("[BoosterPurchaseUICreator] Booster purchase popup built. Run 'Create Booster Bar UI' next.");
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
