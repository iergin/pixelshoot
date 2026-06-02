#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    /// One-click dummy Shop UI under a Screen-Space-Overlay canvas. Builds a centered
    /// dark panel with a title, a close button, a coin-balance HUD, and one item per
    /// offer found in <c>Assets/_Game/Offers</c>. Each item gets a <see cref="ShopOfferButton"/>
    /// wired to its offer, title / price labels, Buy button, and an OWNED overlay.
    /// </summary>
    public static class ShopUICreator
    {
        private const string OffersDir = "Assets/_Game/SO/Shop";
        private const string CanvasName = "[PixelShoot Shop Canvas]";
        private const string PanelName  = "ShopPanel";

        [MenuItem("PixelShoot/Create Shop UI in Scene")]
        public static void CreateShopUI()
        {
            // 1) Get (or create) an overlay canvas.
            var existingCanvas = GameObject.Find(CanvasName);
            var canvasGo = existingCanvas != null ? existingCanvas : CreateOverlayCanvas();
            if (existingCanvas == null) Undo.RegisterCreatedObjectUndo(canvasGo, "Create Shop Canvas");

            // 2) If the panel already exists, nuke and rebuild for a clean state.
            var oldPanel = canvasGo.transform.Find(PanelName);
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

            // 3) Gather offers.
            var offers = LoadAllOffers();
            if (offers.Count == 0)
            {
                Debug.LogWarning($"[ShopUICreator] No ShopOffer assets found under {OffersDir}. Create one (Create ▶ PixelShoot ▶ Shop ▶ Basic Offer) and rerun.");
                offers.Add(EnsureDummyBasicOffer());
            }

            // 4) Build the dimmed full-screen panel.
            var panel = CreateUI(PanelName, canvasGo.transform);
            StretchToParent(panel);
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.6f);
            panel.AddComponent<GraphicRaycaster>(); // ensures clicks behind don't leak through

            // 5) Centered card container.
            var card = CreateUI("Card", panel.transform);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(560f, 720f);
            cardRT.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.12f, 0.13f, 0.16f, 0.96f);

            // Header (title + close).
            var header = CreateUI("Header", card.transform);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot     = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 64f);
            headerRT.anchoredPosition = new Vector2(0, 0);

            var title = CreateText("Title", header.transform, "SHOP", 28, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchToParent(title);

            var closeBtnGo = CreateButton("CloseButton", header.transform, "✕", 22);
            var closeRT = closeBtnGo.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 0.5f);
            closeRT.anchorMax = new Vector2(1, 0.5f);
            closeRT.pivot     = new Vector2(1, 0.5f);
            closeRT.sizeDelta = new Vector2(56f, 56f);
            closeRT.anchoredPosition = new Vector2(-8f, 0f);
            closeBtnGo.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(false));

            // Coin HUD bar (top of card).
            var hud = CreateUI("CoinHUD", card.transform);
            var hudRT = hud.GetComponent<RectTransform>();
            hudRT.anchorMin = new Vector2(0, 1);
            hudRT.anchorMax = new Vector2(1, 1);
            hudRT.pivot     = new Vector2(0.5f, 1);
            hudRT.sizeDelta = new Vector2(0, 48f);
            hudRT.anchoredPosition = new Vector2(0, -64f);
            var hudBg = hud.AddComponent<Image>();
            hudBg.color = new Color(1f, 0.85f, 0.3f, 0.15f);
            var coinText = CreateText("CoinText", hud.transform, "0", 22, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchToParent(coinText);
            var coinLabel = hud.AddComponent<CoinLabel>();
            SetSerializedRef(coinLabel, "tmpText", coinText.GetComponent<TMP_Text>());

            // Scrollable list of offers. We keep it simple — vertical layout group inside a content rect.
            var listRoot = CreateUI("OffersList", card.transform);
            var listRT = listRoot.GetComponent<RectTransform>();
            listRT.anchorMin = new Vector2(0, 0);
            listRT.anchorMax = new Vector2(1, 1);
            listRT.offsetMin = new Vector2(16, 16);
            listRT.offsetMax = new Vector2(-16, -120);
            var vlg = listRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            foreach (var offer in offers)
                CreateOfferItem(listRoot.transform, offer);

            // 6) Floating button outside the panel to re-open it after closing.
            EnsureToggleButton(canvasGo, panel);

            Selection.activeObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            EditorUtility.SetDirty(canvasGo);

            Debug.Log($"[ShopUICreator] Built shop UI with {offers.Count} offer item(s) under '{CanvasName}'.");
        }

        // ───────────────────────────────────────────────────────────────
        private static GameObject CreateOverlayCanvas()
        {
            var go = new GameObject(CanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above gameplay HUD by default

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // Ensure an EventSystem exists so buttons receive clicks.
#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
#else
            var existing = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
#endif
            if (existing == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
            return go;
        }

        private static void CreateOfferItem(Transform parent, ShopOffer offer)
        {
            var item = CreateUI($"Offer_{offer.OfferId}", parent);
            var itemRT = item.GetComponent<RectTransform>();
            itemRT.sizeDelta = new Vector2(0, 110f);
            var itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(1f, 1f, 1f, 0.06f);

            // Title (top-left)
            var title = CreateText("Title", item.transform, offer.DisplayName, 22, TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(0.65f, 1);
            titleRT.pivot = new Vector2(0, 1);
            titleRT.sizeDelta = new Vector2(0, 36f);
            titleRT.anchoredPosition = new Vector2(16, -10);

            // Sub line: "+N coins"
            var subStr = offer.GrantedCoins > 0 ? $"+{offer.GrantedCoins} coins" : "";
            var sub = CreateText("Sub", item.transform, subStr, 16, TextAlignmentOptions.Left, FontStyles.Normal);
            var subRT = sub.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0, 1);
            subRT.anchorMax = new Vector2(0.65f, 1);
            subRT.pivot = new Vector2(0, 1);
            subRT.sizeDelta = new Vector2(0, 24f);
            subRT.anchoredPosition = new Vector2(16, -46);

            // Buy button (right)
            var buyGo = CreateButton("BuyButton", item.transform, "Buy", 18);
            var buyRT = buyGo.GetComponent<RectTransform>();
            buyRT.anchorMin = new Vector2(1, 0.5f);
            buyRT.anchorMax = new Vector2(1, 0.5f);
            buyRT.pivot     = new Vector2(1, 0.5f);
            buyRT.sizeDelta = new Vector2(140f, 64f);
            buyRT.anchoredPosition = new Vector2(-16f, 0f);

            // Price label sits above the buy button.
            var price = CreateText("Price", item.transform, "$0.99", 14, TextAlignmentOptions.Right, FontStyles.Normal);
            var priceRT = price.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(1, 1);
            priceRT.anchorMax = new Vector2(1, 1);
            priceRT.pivot     = new Vector2(1, 1);
            priceRT.sizeDelta = new Vector2(160f, 22f);
            priceRT.anchoredPosition = new Vector2(-16f, -10f);

            // OWNED overlay
            var owned = CreateUI("OwnedOverlay", item.transform);
            StretchToParent(owned);
            var ownedImg = owned.AddComponent<Image>();
            ownedImg.color = new Color(0f, 0f, 0f, 0.55f);
            var ownedLabel = CreateText("OwnedText", owned.transform, "OWNED", 22, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchToParent(ownedLabel);
            owned.SetActive(false);

            // Hook ShopOfferButton.
            var ctl = item.AddComponent<ShopOfferButton>();
            SetSerializedRef(ctl, "offer", offer);
            SetSerializedRef(ctl, "buyButton", buyGo.GetComponent<Button>());
            SetSerializedRef(ctl, "titleLabel", title.GetComponent<TMP_Text>());
            SetSerializedRef(ctl, "priceLabel", price.GetComponent<TMP_Text>());
            SetSerializedRef(ctl, "ownedOverlay", owned);
        }

        private static void EnsureToggleButton(GameObject canvas, GameObject panel)
        {
            const string ToggleName = "OpenShopButton";
            var existing = canvas.transform.Find(ToggleName);
            if (existing != null) return;

            var btn = CreateButton(ToggleName, canvas.transform, "🛒 Shop", 18);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(160f, 64f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
            btn.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(true));
        }

        // ── Primitive UI builders ───────────────────────────────────────
        private static GameObject CreateUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateText(string name, Transform parent, string text, float size,
            TextAlignmentOptions align, FontStyles style)
        {
            var go = CreateUI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.fontStyle = style;
            t.color = Color.white;
            t.raycastTarget = false;
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string label, float labelSize)
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

        private static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Asset gathering ─────────────────────────────────────────────
        private static List<ShopOffer> LoadAllOffers()
        {
            var list = new List<ShopOffer>();
            if (!AssetDatabase.IsValidFolder(OffersDir)) return list;
            var guids = AssetDatabase.FindAssets("t:ShopOffer", new[] { OffersDir });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var offer = AssetDatabase.LoadAssetAtPath<ShopOffer>(path);
                if (offer != null) list.Add(offer);
            }
            // StarterOffer (and any other promo SO) goes to the top so the player sees
            // the special deal first; the rest follow by GrantedCoins ascending.
            list.Sort((a, b) =>
            {
                int sa = a is StarterOffer ? 0 : 1;
                int sb = b is StarterOffer ? 0 : 1;
                if (sa != sb) return sa.CompareTo(sb);
                return a.GrantedCoins.CompareTo(b.GrantedCoins);
            });
            return list;
        }

        private static BasicOffer EnsureDummyBasicOffer()
        {
            if (!AssetDatabase.IsValidFolder(OffersDir))
            {
                var parent = Path.GetDirectoryName(OffersDir).Replace('\\', '/');
                var name = Path.GetFileName(OffersDir);
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, name);
            }
            string path = $"{OffersDir}/BasicStarter.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BasicOffer>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<BasicOffer>();
            SetPrivateField(asset, "offerId",      "basic_starter");
            SetPrivateField(asset, "productId",    "com.pixelshoot.basic");
            SetPrivateField(asset, "displayName",  "Starter Pack");
            SetPrivateField(asset, "grantedCoins", 5000);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ── Reflection helpers ─────────────────────────────────────────
        private static void SetSerializedRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[ShopUICreator] No serialized field '{field}' on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(target, value); return; }
                t = t.BaseType;
            }
        }
    }
}
#endif
