using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;
using PixelShoot.Ads;
using PixelShoot.Shop;

namespace PixelShoot.UI
{
    /// <summary>
    /// "Out of lives" popup shown when the player presses Start with no lives left. Two ways to keep
    /// playing:
    /// <list type="bullet">
    /// <item><b>Refill</b> to full for <see cref="refillCost"/> coins. Not enough coins → closes and
    /// opens the shop.</item>
    /// <item><b>Watch a rewarded ad</b> for <see cref="adLifeReward"/> life.</item>
    /// </list>
    /// Gaining any life auto-closes the popup. Open it with
    /// <c>PopupService.Instance.Create&lt;OutOfLivesPopup&gt;()</c>.
    /// </summary>
    public class OutOfLivesPopup : BasePopup
    {
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
        [Tooltip("Shows the current life count (or ∞ during an unlimited period).")]
        [SerializeField] private TMP_Text livesLabel;
        [SerializeField] private string unlimitedText = "∞";

        protected override void OnInit()
        {
            if (refillButton != null) { refillButton.onClick.RemoveAllListeners(); refillButton.onClick.AddListener(OnRefill); }
            if (adButton     != null) { adButton.onClick.RemoveAllListeners();     adButton.onClick.AddListener(OnWatchAd); }
            if (refillLabel  != null) refillLabel.text = string.Format(refillFormat, refillCost);

            PlayerLives.OnChanged += OnLivesChanged;
            PlayerWallet.OnBalanceChanged += OnBalanceChanged;
            Refresh();
        }

        protected override void OnPopupClosing()
        {
            PlayerLives.OnChanged -= OnLivesChanged;
            PlayerWallet.OnBalanceChanged -= OnBalanceChanged;
        }

        private void OnLivesChanged(int lives)
        {
            if (lives > 0 || PlayerLives.IsUnlimited) Close(); // can play again → dismiss
            else Refresh();
        }

        private void OnBalanceChanged(int _) => Refresh();

        private void Refresh()
        {
            if (balanceLabel != null) balanceLabel.text = PlayerWallet.Balance.ToString();
            if (livesLabel != null)   livesLabel.text = PlayerLives.IsUnlimited ? unlimitedText : PlayerLives.Lives.ToString();

            bool adReady = AdsManager.Service != null && AdsManager.Service.IsRewardedReady;
            if (adButton != null) adButton.interactable = adReady;
            if (adLabel  != null) adLabel.text = adReady ? string.Format(adReadyFormat, adLifeReward) : adNotReadyText;
        }

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
                // Top-level Create → queues behind our close, so the shop opens once we're gone.
                if (ShopManager.Instance != null) ShopManager.Instance.OpenShop("out_of_lives");
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
