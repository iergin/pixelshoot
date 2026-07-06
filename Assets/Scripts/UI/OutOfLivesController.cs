using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;
using PixelShoot.Ads;
using PixelShoot.Shop;

namespace PixelShoot.UI
{
    /// <summary>
    /// The "Out of lives" popup shown when the player presses Start with no lives left.
    /// Offers two ways to keep playing:
    /// <list type="bullet">
    /// <item><b>Refill</b> to full for <see cref="refillCost"/> coins. Not enough coins →
    /// closes and opens the shop.</item>
    /// <item><b>Watch a rewarded ad</b> for <see cref="adLifeReward"/> life.</item>
    /// </list>
    /// Show/hide is routed through the shared <see cref="UiPanel"/> / UiPanelManager, so it
    /// never overlaps another panel. Gaining any life dismisses the popup.
    /// </summary>
    public class OutOfLivesController : MonoBehaviour
    {
        [Header("Panel routing")]
        [Tooltip("UiPanel on the panel root — open/close go through the shared UiPanelManager. If left empty, a UiPanel on THIS GameObject is used.")]
        [SerializeField] private UiPanel uiPanel;

        [Header("Buttons")]
        [SerializeField] private Button refillButton;
        [SerializeField] private Button adButton;

        [Header("Refill with coins")]
        [SerializeField] private int refillCost = 900;
        [Tooltip("Optional. Shows the refill price, {0} = cost.")]
        [SerializeField] private TMP_Text refillLabel;
        [SerializeField] private string refillFormat = "Refill  {0}";

        [Header("Rewarded ad → lives")]
        [SerializeField, Min(1)] private int adLifeReward = 1;
        [SerializeField] private TMP_Text adLabel;
        [SerializeField] private string adReadyFormat = "Watch Ad  +{0}";
        [SerializeField] private string adNotReadyText = "Ad not ready";

        [Header("Readouts (optional)")]
        [SerializeField] private TMP_Text balanceLabel;
        [SerializeField] private TMP_Text livesLabel;

        [Header("Shop redirect")]
        [SerializeField] private ShopManager shop;

        private bool wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired) return;
            wired = true;
            if (refillButton != null) { refillButton.onClick.RemoveAllListeners(); refillButton.onClick.AddListener(OnRefill); }
            if (adButton     != null) { adButton.onClick.RemoveAllListeners();     adButton.onClick.AddListener(OnWatchAd); }
            if (refillLabel  != null) refillLabel.text = string.Format(refillFormat, refillCost);
        }

        private UiPanel ResolvePanel()
        {
            if (uiPanel == null) uiPanel = GetComponent<UiPanel>();
            return uiPanel;
        }

        private void OnEnable()
        {
            PlayerLives.OnChanged += OnLivesChanged;
            PlayerWallet.OnBalanceChanged += OnBalanceChanged;
            var p = ResolvePanel();
            if (p != null) p.OnOpened += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerLives.OnChanged -= OnLivesChanged;
            PlayerWallet.OnBalanceChanged -= OnBalanceChanged;
            if (uiPanel != null) uiPanel.OnOpened -= Refresh;
        }

        /// <summary>Show the popup (call from the Start handler when out of lives).</summary>
        public void Open()
        {
            Wire();
            Refresh();
            var p = ResolvePanel();
            if (p != null) p.RequestOpen(replaceCurrent: true);
            else { Debug.LogWarning("[OutOfLives] No UiPanel assigned — falling back to SetActive."); gameObject.SetActive(true); }
        }

        public void Close()
        {
            var p = ResolvePanel();
            if (p != null) p.RequestClose();
            else gameObject.SetActive(false);
        }

        private void OnLivesChanged(int lives)
        {
            if (lives > 0) Close(); // got a life → let them start again
            else Refresh();
        }

        private void OnBalanceChanged(int _) => Refresh();

        private void Refresh()
        {
            if (balanceLabel != null) balanceLabel.text = PlayerWallet.Balance.ToString();
            if (livesLabel != null)   livesLabel.text = PlayerLives.Lives.ToString();

            bool adReady = AdsManager.Service != null && AdsManager.Service.IsRewardedReady;
            if (adButton != null) adButton.interactable = adReady;
            if (adLabel  != null) adLabel.text = adReady ? string.Format(adReadyFormat, adLifeReward) : adNotReadyText;
        }

        // ── Actions ─────────────────────────────────────────────────────────
        private void OnRefill()
        {
            if (PlayerLives.IsFull) { Close(); return; }

            if (PlayerWallet.TrySpend(refillCost))
            {
                PlayerLives.Refill();
                Close();
            }
            else
            {
                Debug.Log($"[OutOfLives] Not enough coins for refill ({PlayerWallet.Balance}/{refillCost}) → opening shop.");
                Close();
                if (shop != null) shop.OpenShop();
                else if (ShopManager.Instance != null) ShopManager.Instance.OpenShop();
            }
        }

        private void OnWatchAd()
        {
            var svc = AdsManager.Service;
            if (svc == null || !svc.IsRewardedReady) { Refresh(); return; }
            svc.ShowRewarded(
                onRewarded: () => PlayerLives.AddLives(adLifeReward),
                onClosed:   Refresh);
        }
    }
}
