using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Ads;
using PixelShoot.Shop;
using PixelShoot.Conveyor;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Shared "buy this booster" popup (works for every booster). Shows the booster's
    /// title / description / icon and two purchase buttons (watch-ad and buy-with-coins,
    /// each shown per the BoosterData flags). Not enough coins → sends the player to the
    /// shop and reopens itself when the shop closes. Pauses the conveyor while open.
    ///
    /// Put this on an always-active object (e.g. the canvas) and point <see cref="panel"/>
    /// at the popup root so button wiring survives while the popup is hidden.
    /// </summary>
    public class BoosterPurchaseController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;

        [Header("Content")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image iconImage;

        [Header("Buttons")]
        [SerializeField] private Button adButton;
        [SerializeField] private Button coinButton;
        [SerializeField] private TMP_Text coinCostLabel;
        [SerializeField] private string coinCostFormat = "{0}";
        [SerializeField] private Button closeButton;

        [Header("Links")]
        [SerializeField] private BoosterManager manager;
        [Tooltip("Conveyor paused while the popup is open.")]
        [SerializeField] private ConveyorController conveyor;

        // ShopManager lives in the persistent InitializeScene, so reach it via its static Instance
        // (a serialized cross-scene reference wouldn't survive).
        private static ShopManager Shop => ShopManager.Instance;

        private BoosterData current;
        private bool wired;

        // Shop-redirect reopen tracking.
        private BoosterData pendingReopen;
        private bool waitingForShop;
        private bool sawShopOpen;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired) return;
            wired = true;
            if (adButton    != null) { adButton.onClick.RemoveAllListeners();    adButton.onClick.AddListener(OnWatchAd); }
            if (coinButton  != null) { coinButton.onClick.RemoveAllListeners();  coinButton.onClick.AddListener(OnBuyWithCoins); }
            if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Close); }
        }

        public void Open(BoosterData data)
        {
            if (data == null) return;
            Wire();
            current = data;

            if (titleLabel != null)       titleLabel.text = data.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = data.Description;
            if (iconImage != null) { iconImage.sprite = data.Icon; iconImage.enabled = data.Icon != null; }

            if (adButton != null)  adButton.gameObject.SetActive(data.AdPurchaseEnabled);
            if (coinButton != null)
            {
                coinButton.gameObject.SetActive(data.CoinPurchaseEnabled);
                if (coinCostLabel != null) coinCostLabel.text = string.Format(coinCostFormat, data.CoinCost);
            }

            if (conveyor != null) conveyor.IsPaused = true; // freeze play while choosing
            if (panel != null) panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (conveyor != null) conveyor.IsPaused = false;
        }

        // ── Actions ──────────────────────────────────────────────────────────
        private void OnBuyWithCoins()
        {
            if (current == null) return;
            if (PlayerWallet.TrySpend(current.CoinCost))
            {
                var bought = current;
                PlayerBoosters.Add(bought.Id, bought.GrantAmount);
                Close();
                if (manager != null) manager.OnPurchased(bought); // → opens the use panel
            }
            else
            {
                // Not enough coins → shop, then come back to this popup. Hide the panel but
                // KEEP the conveyor paused so the game stays frozen behind the shop.
                Debug.Log($"[BoosterPurchase] Not enough coins ({PlayerWallet.Balance}/{current.CoinCost}) → shop.");
                pendingReopen = current;
                waitingForShop = true;
                sawShopOpen = false;
                if (panel != null) panel.SetActive(false); // NOT Close() — don't resume the conveyor
                if (Shop != null) Shop.OpenShop();
                else Debug.LogWarning("[BoosterPurchase] No ShopManager (is InitializeScene loaded?).");
            }
        }

        private void OnWatchAd()
        {
            if (current == null) return;
            var svc = AdsManager.Service;
            if (svc == null || !svc.IsRewardedReady) { Debug.Log("[BoosterPurchase] Rewarded ad not ready."); return; }
            var granted = current;
            bool rewarded = false;
            svc.ShowRewarded(
                onRewarded: () => { PlayerBoosters.Add(granted.Id, granted.GrantAmount); rewarded = true; },
                onClosed:   () => { Close(); if (rewarded && manager != null) manager.OnPurchased(granted); });
        }

        private void Update()
        {
            if (!waitingForShop) return;
            if (Shop == null) { waitingForShop = false; return; }

            if (Shop.IsOpen) { sawShopOpen = true; return; }
            if (sawShopOpen) // shop was opened and is now closed → reopen the booster popup
            {
                waitingForShop = false;
                sawShopOpen = false;
                if (pendingReopen != null) Open(pendingReopen);
            }
        }
    }
}
