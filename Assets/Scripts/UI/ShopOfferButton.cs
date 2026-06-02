using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.Shop;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drag a ShopOffer SO + a Button + (optional) TMP labels for title and price.
    /// Handles availability gating, localized price polling, and the purchase callback.
    ///
    /// <para><b>Price pulled from the store</b>: until Unity IAP returns a real
    /// localized price the label shows "?" and the buy button is disabled. The
    /// component polls once per <see cref="RetryIntervalSec"/> until the store
    /// is initialised — no extra user action required.</para>
    /// </summary>
    public class ShopOfferButton : MonoBehaviour
    {
        private const float RetryIntervalSec = 1f;
        private const string UnknownPriceLabel = "?";

        [SerializeField] private ShopOffer offer;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text priceLabel;
        [Tooltip("Shown over the button when the offer has already been purchased (or is otherwise unavailable).")]
        [SerializeField] private GameObject ownedOverlay;

        private float retryCooldown;
        private bool lastPriceKnown;

        private void OnEnable()
        {
            PlayerWallet.OnBalanceChanged += OnBalance;
            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyClicked);
            }
            retryCooldown = 0f;
            lastPriceKnown = false;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerWallet.OnBalanceChanged -= OnBalance;
        }

        private void Update()
        {
            // Retry pulling the price from the store until we have a real one. As soon
            // as the IAP catalog comes online the price label flips from "?" to
            // localised currency and the buy button becomes interactable again.
            if (lastPriceKnown) return;
            retryCooldown -= Time.unscaledDeltaTime;
            if (retryCooldown > 0f) return;
            retryCooldown = RetryIntervalSec;
            Refresh();
        }

        private void OnBalance(int _) => Refresh();

        public void Refresh()
        {
            if (offer == null) return;
            if (titleLabel != null) titleLabel.text = offer.DisplayName;

            // 1) Visibility: starter / one-time offers can hide themselves entirely.
            bool available = offer.IsAvailable;

            // 2) Pull price. Empty / "—" means store not ready yet.
            string price = ShopManager.Instance != null
                ? ShopManager.Instance.GetLocalizedPrice(offer)
                : null;
            bool priceKnown = !string.IsNullOrEmpty(price) && price != "—";
            lastPriceKnown = priceKnown;

            if (priceLabel != null)
                priceLabel.text = priceKnown ? price : UnknownPriceLabel;

            // 3) Button: enabled only when offer is available AND we have a real price.
            if (buyButton != null) buyButton.interactable = available && priceKnown;
            if (ownedOverlay != null) ownedOverlay.SetActive(!available);
        }

        private void OnBuyClicked()
        {
            if (offer == null || ShopManager.Instance == null) return;
            ShopManager.Instance.BuyOffer(offer, _ => Refresh());
        }
    }
}
