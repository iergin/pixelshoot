#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Data;
using PixelShoot.Shop;
using PixelShoot.UI;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Spawns the "Starter Pack" promo panel that fires at Trigger 2 once
    /// NoAds has already been purchased. Auto-discovers the StarterOffer asset
    /// under <c>Assets/_Game/SO/Shop</c>, wires up a <see cref="ShopOfferButton"/>,
    /// and links the panel into <see cref="NoAdsPromoController.starterPromoPanel"/>
    /// on the existing canvas / controller.
    /// </summary>
    public static class StarterPackPromoUICreator
    {
        private const string CanvasName = "[PixelShoot Promo Canvas]";
        private const string PanelName  = "StarterPackPromoPanel";
        private const string OffersDir  = "Assets/_Game/SO/Shop";

        [MenuItem("Generator/Create Starter Pack Promo Panel UI")]
        public static void CreateStarterPackUI()
        {
            // Share the same canvas as the NoAds promo so the controller wires both side-by-side.
            var canvasGo = SettingsUICreator.GetOrCreateOverlayCanvas(CanvasName, sortingOrder: 110);

            var existingPanel = canvasGo.transform.Find(PanelName);
            if (existingPanel != null) Object.DestroyImmediate(existingPanel.gameObject);

            var starterOffer = LoadStarterOffer();
            if (starterOffer == null)
            {
                Debug.LogWarning($"[StarterPackPromoUICreator] No StarterOffer asset under {OffersDir}. " +
                                 "Create one via Create ▶ PixelShoot ▶ Shop ▶ Starter Offer, then rerun.");
            }

            // Dim background.
            var panel = SettingsUICreator.CreateUI(PanelName, canvasGo.transform);
            SettingsUICreator.StretchToParent(panel);
            var dim = panel.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.7f);

            // Centered card.
            var card = SettingsUICreator.CreateUI("Card", panel.transform);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(560f, 520f);
            cardRT.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.1f, 0.18f, 0.15f, 0.97f);

            // Header.
            var title = SettingsUICreator.CreateText("Title", card.transform, "STARTER PACK", 32,
                TextAlignmentOptions.Center, FontStyles.Bold);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1); titleRT.sizeDelta = new Vector2(0, 80f);

            // Sub headline.
            var sub = SettingsUICreator.CreateText("Sub", card.transform,
                "A limited-time bundle for new players. Grab it before it's gone!", 18,
                TextAlignmentOptions.Center, FontStyles.Normal);
            var subRT = sub.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.06f, 0.6f); subRT.anchorMax = new Vector2(0.94f, 0.78f);
            subRT.offsetMin = Vector2.zero; subRT.offsetMax = Vector2.zero;

            // Offer item with ShopOfferButton.
            var offerGo = SettingsUICreator.CreateUI("OfferItem", card.transform);
            var offerRT = offerGo.GetComponent<RectTransform>();
            offerRT.anchorMin = new Vector2(0.08f, 0.2f); offerRT.anchorMax = new Vector2(0.92f, 0.55f);
            offerRT.offsetMin = Vector2.zero; offerRT.offsetMax = Vector2.zero;
            var offerBg = offerGo.AddComponent<Image>();
            offerBg.color = new Color(1f, 1f, 1f, 0.06f);

            var titleLabel = SettingsUICreator.CreateText("Title", offerGo.transform,
                starterOffer != null ? starterOffer.DisplayName : "Starter Pack", 18,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var titleLabelRT = titleLabel.GetComponent<RectTransform>();
            titleLabelRT.anchorMin = new Vector2(0, 0.55f); titleLabelRT.anchorMax = new Vector2(0.5f, 1f);
            titleLabelRT.offsetMin = new Vector2(16, 0); titleLabelRT.offsetMax = Vector2.zero;

            var coinsLabel = SettingsUICreator.CreateText("Coins", offerGo.transform,
                starterOffer != null ? $"+{starterOffer.GrantedCoins} coins" : "+5000 coins", 16,
                TextAlignmentOptions.BottomLeft, FontStyles.Normal);
            var coinsLabelRT = coinsLabel.GetComponent<RectTransform>();
            coinsLabelRT.anchorMin = new Vector2(0, 0); coinsLabelRT.anchorMax = new Vector2(0.5f, 0.55f);
            coinsLabelRT.offsetMin = new Vector2(16, 8); coinsLabelRT.offsetMax = Vector2.zero;

            var priceLabel = SettingsUICreator.CreateText("Price", offerGo.transform, "?", 20,
                TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            var priceRT = priceLabel.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0.5f, 0.55f); priceRT.anchorMax = new Vector2(1, 1);
            priceRT.offsetMin = Vector2.zero; priceRT.offsetMax = new Vector2(-16, 0);

            var buyGo = SettingsUICreator.CreateButton("BuyButton", offerGo.transform, "CLAIM", 20);
            var buyRT = buyGo.GetComponent<RectTransform>();
            buyRT.anchorMin = new Vector2(0.5f, 0); buyRT.anchorMax = new Vector2(1, 0.55f);
            buyRT.offsetMin = new Vector2(8, 8); buyRT.offsetMax = new Vector2(-16, -4);

            // OWNED overlay.
            var ownedOverlay = SettingsUICreator.CreateUI("OwnedOverlay", offerGo.transform);
            SettingsUICreator.StretchToParent(ownedOverlay);
            var ownedImg = ownedOverlay.AddComponent<Image>();
            ownedImg.color = new Color(0f, 0f, 0f, 0.55f);
            var ownedLabel = SettingsUICreator.CreateText("Label", ownedOverlay.transform, "OWNED", 22,
                TextAlignmentOptions.Center, FontStyles.Bold);
            SettingsUICreator.StretchToParent(ownedLabel);
            ownedOverlay.SetActive(false);

            var offerCtl = offerGo.AddComponent<ShopOfferButton>();
            var soOffer = new SerializedObject(offerCtl);
            SettingsUICreator.SetRef(soOffer, "offer", starterOffer);
            SettingsUICreator.SetRef(soOffer, "buyButton", buyGo.GetComponent<Button>());
            SettingsUICreator.SetRef(soOffer, "titleLabel", titleLabel.GetComponent<TMP_Text>());
            SettingsUICreator.SetRef(soOffer, "priceLabel", priceLabel.GetComponent<TMP_Text>());
            SettingsUICreator.SetRef(soOffer, "ownedOverlay", ownedOverlay);
            soOffer.ApplyModifiedProperties();

            // "Maybe later" close.
            var closeGo = SettingsUICreator.CreateButton("MaybeLaterButton", card.transform, "Maybe later", 16);
            var closeRT = closeGo.GetComponent<RectTransform>();
            closeRT.anchorMin = closeRT.anchorMax = new Vector2(0.5f, 0);
            closeRT.pivot = new Vector2(0.5f, 0); closeRT.sizeDelta = new Vector2(200f, 56f);
            closeRT.anchoredPosition = new Vector2(0f, 16f);
            closeGo.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(false));

            // Wire into the existing NoAdsPromoController (the canvas should already have one
            // from the NoAds promo step; if not, add a fresh component on the canvas).
            var promo = canvasGo.GetComponent<NoAdsPromoController>() ?? canvasGo.AddComponent<NoAdsPromoController>();
            var so = new SerializedObject(promo);
            SettingsUICreator.SetRef(so, "starterPromoPanel", panel);
            so.ApplyModifiedProperties();

            panel.SetActive(false);
            EditorUtility.SetDirty(canvasGo);
            Selection.activeObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            Debug.Log("[StarterPackPromoUICreator] Starter Pack promo panel built and wired to NoAdsPromoController.starterPromoPanel.");
        }

        private static StarterOffer LoadStarterOffer()
        {
            if (!AssetDatabase.IsValidFolder(OffersDir)) return null;
            var guids = AssetDatabase.FindAssets("t:StarterOffer", new[] { OffersDir });
            if (guids == null || guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<StarterOffer>(path);
        }
    }
}
#endif
