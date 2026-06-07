#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Ads;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Shop;
using PixelShoot.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Spawns a dummy "Remove Ads" promo panel and wires it onto a
    /// <see cref="NoAdsPromoController"/>. The panel auto-discovers the NoAds
    /// offer asset, attaches a <see cref="ShopOfferButton"/> with its UI slots,
    /// and links the controller's other references (InterstitialController,
    /// GameController) when those components exist in the scene.
    /// </summary>
    public static class NoAdsPromoUICreator
    {
        private const string CanvasName = "[PixelShoot Promo Canvas]";
        private const string PanelName  = "NoAdsPromoPanel";
        private const string OffersDir  = "Assets/_Game/SO/Shop";

        [MenuItem("Generator/Create NoAds Promo Panel UI")]
        public static void CreateNoAdsPromoUI()
        {
            var canvasGo = SettingsUICreator.GetOrCreateOverlayCanvas(CanvasName, sortingOrder: 110);

            var existingPanel = canvasGo.transform.Find(PanelName);
            if (existingPanel != null) Object.DestroyImmediate(existingPanel.gameObject);

            // Locate the NoAds offer asset.
            var noAdsOffer = LoadNoAdsOffer();
            if (noAdsOffer == null)
            {
                Debug.LogWarning($"[NoAdsPromoUICreator] No NoAdsOffer asset under {OffersDir}. " +
                                 "Create one via Create ▶ PixelShoot ▶ Shop ▶ No Ads, then rerun.");
            }

            // Dimmed wrapper.
            var panel = SettingsUICreator.CreateUI(PanelName, canvasGo.transform);
            SettingsUICreator.StretchToParent(panel);
            var dim = panel.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.7f);

            // Centered card.
            var card = SettingsUICreator.CreateUI("Card", panel.transform);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(560f, 480f);
            cardRT.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.14f, 0.1f, 0.18f, 0.97f);

            // Header.
            var title = SettingsUICreator.CreateText("Title", card.transform, "REMOVE ADS", 32,
                TextAlignmentOptions.Center, FontStyles.Bold);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1); titleRT.sizeDelta = new Vector2(0, 80f);

            // Sub headline.
            var sub = SettingsUICreator.CreateText("Sub", card.transform,
                "No more interstitials. No more banners. Forever.", 18,
                TextAlignmentOptions.Center, FontStyles.Normal);
            var subRT = sub.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.06f, 0.55f); subRT.anchorMax = new Vector2(0.94f, 0.75f);
            subRT.offsetMin = Vector2.zero; subRT.offsetMax = Vector2.zero;

            // Offer button (uses ShopOfferButton).
            var offerGo = SettingsUICreator.CreateUI("OfferItem", card.transform);
            var offerRT = offerGo.GetComponent<RectTransform>();
            offerRT.anchorMin = new Vector2(0.08f, 0.18f); offerRT.anchorMax = new Vector2(0.92f, 0.5f);
            offerRT.offsetMin = Vector2.zero; offerRT.offsetMax = Vector2.zero;
            var offerBg = offerGo.AddComponent<Image>();
            offerBg.color = new Color(1f, 1f, 1f, 0.06f);

            var priceLabel = SettingsUICreator.CreateText("Price", offerGo.transform, "?", 20,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            var priceRT = priceLabel.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0, 0); priceRT.anchorMax = new Vector2(0.5f, 1);
            priceRT.offsetMin = new Vector2(16, 0); priceRT.offsetMax = Vector2.zero;

            var buyGo = SettingsUICreator.CreateButton("BuyButton", offerGo.transform, "BUY NOW", 20);
            var buyRT = buyGo.GetComponent<RectTransform>();
            buyRT.anchorMin = new Vector2(0.5f, 0); buyRT.anchorMax = new Vector2(1, 1);
            buyRT.offsetMin = new Vector2(8, 8); buyRT.offsetMax = new Vector2(-16, -8);

            // OWNED overlay (visible after purchase or if offer becomes unavailable).
            var ownedOverlay = SettingsUICreator.CreateUI("OwnedOverlay", offerGo.transform);
            SettingsUICreator.StretchToParent(ownedOverlay);
            var ownedImg = ownedOverlay.AddComponent<Image>();
            ownedImg.color = new Color(0f, 0f, 0f, 0.55f);
            var ownedLabel = SettingsUICreator.CreateText("Label", ownedOverlay.transform, "OWNED", 22,
                TextAlignmentOptions.Center, FontStyles.Bold);
            SettingsUICreator.StretchToParent(ownedLabel);
            ownedOverlay.SetActive(false);

            var titleLabel = SettingsUICreator.CreateText("Title", offerGo.transform,
                noAdsOffer != null ? noAdsOffer.DisplayName : "Remove Ads", 16,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var titleLabelRT = titleLabel.GetComponent<RectTransform>();
            titleLabelRT.anchorMin = new Vector2(0, 0.55f); titleLabelRT.anchorMax = new Vector2(0.5f, 1f);
            titleLabelRT.offsetMin = new Vector2(16, 0); titleLabelRT.offsetMax = Vector2.zero;

            var offerCtl = offerGo.AddComponent<ShopOfferButton>();
            var soOffer = new SerializedObject(offerCtl);
            SettingsUICreator.SetRef(soOffer, "offer", noAdsOffer);
            SettingsUICreator.SetRef(soOffer, "buyButton", buyGo.GetComponent<Button>());
            SettingsUICreator.SetRef(soOffer, "titleLabel", titleLabel.GetComponent<TMP_Text>());
            SettingsUICreator.SetRef(soOffer, "priceLabel", priceLabel.GetComponent<TMP_Text>());
            SettingsUICreator.SetRef(soOffer, "ownedOverlay", ownedOverlay);
            soOffer.ApplyModifiedProperties();

            // "No thanks" close button.
            var closeGo = SettingsUICreator.CreateButton("NoThanksButton", card.transform, "No thanks", 16);
            var closeRT = closeGo.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(0.5f, 0); closeRT.anchorMax = new Vector2(0.5f, 0);
            closeRT.pivot = new Vector2(0.5f, 0); closeRT.sizeDelta = new Vector2(180f, 56f);
            closeRT.anchoredPosition = new Vector2(0f, 16f);
            closeGo.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(false));

            // Wire onto NoAdsPromoController (create one on the canvas if missing).
            var promo = canvasGo.GetComponent<NoAdsPromoController>() ?? canvasGo.AddComponent<NoAdsPromoController>();
            var so = new SerializedObject(promo);
            SettingsUICreator.SetRef(so, "promoPanel", panel);
            SettingsUICreator.SetRef(so, "interstitial", FindInScene<InterstitialController>());
            SettingsUICreator.SetRef(so, "gameController", FindInScene<GameController>());
            so.ApplyModifiedProperties();

            panel.SetActive(false);
            EditorUtility.SetDirty(canvasGo);
            Selection.activeObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            Debug.Log("[NoAdsPromoUICreator] Promo panel built and wired to NoAdsPromoController.");
        }

        private static NoAdsOffer LoadNoAdsOffer()
        {
            if (!AssetDatabase.IsValidFolder(OffersDir)) return null;
            var guids = AssetDatabase.FindAssets("t:NoAdsOffer", new[] { OffersDir });
            if (guids == null || guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<NoAdsOffer>(path);
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
