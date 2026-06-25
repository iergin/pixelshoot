using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PixelShoot.Data;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Holds the list of available offers, talks to <see cref="IIAPService"/>,
    /// and routes successful purchases to <see cref="ShopOffer.OnPurchased"/>.
    ///
    /// <para>Also owns the shop panel + open / close buttons so the UI flow
    /// can be wired entirely from the inspector — no extra glue script
    /// needed.</para>
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Catalog")]
        [SerializeField] private List<ShopOffer> offers = new List<ShopOffer>();

        [Header("UI")]
        [Tooltip("Root panel containing the shop UI. Activated by OpenShop(), deactivated by CloseShop().")]
        [SerializeField] private GameObject shopPanel;
        [Tooltip("If set, open/close routes through the global UiPanelManager so the shop never overlaps another panel.")]
        [SerializeField] private PixelShoot.UI.UiPanel uiPanel;
        [Tooltip("Button that opens the shop. Wired automatically in Awake.")]
        [SerializeField] private Button openShopButton;
        [Tooltip("Optional close (X) button inside the panel. Wired automatically in Awake if set.")]
        [SerializeField] private Button closeShopButton;
        [Tooltip("If true, the shop panel starts hidden and only shows after pressing the open button.")]
        [SerializeField] private bool startHidden = true;

        private IIAPService iap;

        public IReadOnlyList<ShopOffer> Offers => offers;
        public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[ShopManager] Duplicate instance on '{name}' — destroying. This kills any wired listeners on this instance.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if PIXELSHOOT_IAP
            iap = new UnityIAPService();
#else
            iap = new NullIAPService();
#endif
            var products = new List<ProductRegistration>();
            foreach (var o in offers)
                if (o != null && !string.IsNullOrEmpty(o.ProductId))
                    products.Add(new ProductRegistration(o.ProductId, o.ProductType));
            iap.Initialize(products);

            // ── Wire UI ──
            Debug.Log($"[ShopManager] Awake on '{name}'. " +
                      $"shopPanel={NameOf(shopPanel)}, " +
                      $"openShopButton={NameOf(openShopButton)}, " +
                      $"closeShopButton={NameOf(closeShopButton)}, " +
                      $"startHidden={startHidden}.");

            if (openShopButton != null)
            {
                // Keep persistent (inspector-added) listeners alive — RemoveAllListeners
                // only nukes non-persistent ones added by code. The lambda below proves
                // whether the click reaches us at all.
                openShopButton.onClick.RemoveAllListeners();
                int btnInstanceId = openShopButton.GetInstanceID();
                openShopButton.onClick.AddListener(() =>
                {
                    Debug.Log($"[ShopManager] CLICK RECEIVED on button instId={btnInstanceId} " +
                              $"name='{(openShopButton != null ? openShopButton.name : "<null>")}'. " +
                              $"Calling OpenShop on instance {GetInstanceID()}.");
                    OpenShop();
                });
                Debug.Log($"[ShopManager] Wired OpenShop onto '{openShopButton.name}' " +
                          $"(instId={btnInstanceId}, persistentListenerCount={openShopButton.onClick.GetPersistentEventCount()}).");
            }
            else
            {
                Debug.LogWarning("[ShopManager] openShopButton is NULL — clicking will do nothing.");
            }
            if (closeShopButton != null)
            {
                closeShopButton.onClick.RemoveAllListeners();
                int closeInstanceId = closeShopButton.GetInstanceID();
                closeShopButton.onClick.AddListener(() =>
                {
                    Debug.Log($"[ShopManager] CLOSE CLICK on button instId={closeInstanceId} " +
                              $"name='{(closeShopButton != null ? closeShopButton.name : "<null>")}'.");
                    CloseShop();
                });
                Debug.Log($"[ShopManager] Wired CloseShop onto '{closeShopButton.name}' " +
                          $"(instId={closeInstanceId}, persistentListenerCount={closeShopButton.onClick.GetPersistentEventCount()}).");
            }
            else
            {
                Debug.LogWarning("[ShopManager] closeShopButton is NULL — close click will do nothing.");
            }
            if (shopPanel != null && startHidden) shopPanel.SetActive(false);

            // Defer one frame so any auto-created EventSystem / Canvas finishes initialising.
            DiagnoseClickPlumbing();
        }

        /// <summary>
        /// Walks the openShopButton's parent chain and reports anything that would
        /// silently block clicks: missing EventSystem, missing GraphicRaycaster on
        /// the Canvas, disabled interactable, raycast target off, etc.
        /// </summary>
        private void DiagnoseClickPlumbing()
        {
            if (openShopButton == null) return;

            // 1) EventSystem.
#if UNITY_2023_1_OR_NEWER
            var es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
#else
            var es = Object.FindObjectOfType<EventSystem>();
#endif
            if (es == null)
                Debug.LogError("[ShopManager] No EventSystem in the scene — UI clicks will never fire. Add one via GameObject ▶ UI ▶ Event System.");
            else if (!es.isActiveAndEnabled)
                Debug.LogError($"[ShopManager] EventSystem '{es.name}' is disabled — UI clicks will never fire.");
            else
                Debug.Log($"[ShopManager] EventSystem '{es.name}' OK.");

            // 2) Button.interactable
            if (!openShopButton.interactable)
                Debug.LogWarning($"[ShopManager] Button '{openShopButton.name}' has interactable=false.");

            // 3) targetGraphic + raycastTarget
            var tg = openShopButton.targetGraphic;
            if (tg == null)
                Debug.LogWarning($"[ShopManager] Button '{openShopButton.name}' has no targetGraphic — clicks need a graphic with raycastTarget=true.");
            else if (!tg.raycastTarget)
                Debug.LogWarning($"[ShopManager] Button '{openShopButton.name}'.targetGraphic '{tg.name}' has raycastTarget=false.");

            // 4) Canvas + GraphicRaycaster on the parent chain.
            Canvas canvas = openShopButton.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError($"[ShopManager] Button '{openShopButton.name}' is not under any Canvas — Unity UI requires it.");
            }
            else
            {
                var raycaster = canvas.rootCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                    Debug.LogError($"[ShopManager] Canvas '{canvas.rootCanvas.name}' has NO GraphicRaycaster — clicks ignored. Add one to the root canvas.");
                else if (!raycaster.enabled)
                    Debug.LogError($"[ShopManager] GraphicRaycaster on '{canvas.rootCanvas.name}' is disabled.");
                else
                    Debug.Log($"[ShopManager] Canvas '{canvas.rootCanvas.name}' + GraphicRaycaster OK.");
            }

            // 5) CanvasGroup blocking raycasts on any parent.
            var blocker = openShopButton.GetComponentInParent<CanvasGroup>();
            while (blocker != null)
            {
                if (!blocker.blocksRaycasts || !blocker.interactable)
                {
                    Debug.LogError($"[ShopManager] CanvasGroup on '{blocker.name}' is blocking clicks " +
                                   $"(blocksRaycasts={blocker.blocksRaycasts}, interactable={blocker.interactable}).");
                    break;
                }
                var parent = blocker.transform.parent;
                blocker = parent != null ? parent.GetComponentInParent<CanvasGroup>() : null;
            }
        }

        /// <summary>Resolve the UiPanel: explicit field, else a component on the shop panel.</summary>
        private PixelShoot.UI.UiPanel ResolvePanel()
        {
            if (uiPanel != null) return uiPanel;
            if (shopPanel != null) uiPanel = shopPanel.GetComponent<PixelShoot.UI.UiPanel>();
            return uiPanel;
        }

        public void OpenShop()
        {
            var p = ResolvePanel();
            Debug.Log($"[ShopManager] OpenShop() called. uiPanel={(p != null ? "set" : "<null>")}, shopPanel={NameOf(shopPanel)}.");
            if (p != null) { p.RequestOpen(replaceCurrent: true); return; }
            if (shopPanel == null)
            {
                Debug.LogWarning("[ShopManager] OpenShop: shopPanel reference is missing — nothing to show.");
                return;
            }
            shopPanel.SetActive(true);
        }

        public void CloseShop()
        {
            var p = ResolvePanel();
            if (p != null) { p.RequestClose(); return; }
            if (shopPanel != null) shopPanel.SetActive(false);
        }

        private static string NameOf(Object o) => o == null ? "<null>" : o.name;

        public void ToggleShop()
        {
            if (shopPanel == null) return;
            shopPanel.SetActive(!shopPanel.activeSelf);
        }

        public string GetLocalizedPrice(ShopOffer offer)
            => offer != null && iap != null ? iap.GetLocalizedPrice(offer.ProductId) : "—";

        public void BuyOffer(ShopOffer offer, System.Action<bool> onComplete = null)
        {
            if (offer == null) { onComplete?.Invoke(false); return; }
            if (!offer.IsAvailable)
            {
                Debug.LogWarning($"[Shop] Offer '{offer.OfferId}' not available (already purchased / locked).");
                onComplete?.Invoke(false);
                return;
            }
            if (iap == null || !iap.IsReady)
            {
                Debug.LogWarning("[Shop] IAP service not ready.");
                onComplete?.Invoke(false);
                return;
            }
            iap.Purchase(offer.ProductId, success =>
            {
                if (success) offer.OnPurchased();
                onComplete?.Invoke(success);
            });
        }
    }
}
