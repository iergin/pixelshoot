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
    /// <para><b>Three visual states</b>:
    /// <list type="bullet">
    /// <item><b>For sale</b> — buy button interactable, real localised price.</item>
    /// <item><b>Owned</b> — overlay shown, button disabled. The offer was purchased.</item>
    /// <item><b>Locked / not yet available</b> — the whole GameObject is hidden, so a
    /// "not eligible" offer (e.g. Starter Pack before NoAds is bought) doesn't show
    /// up as "OWNED" or clutter the shop list.</item>
    /// </list></para>
    ///
    /// <para>Until Unity IAP returns a real localized price the label shows "?" and
    /// the buy button stays disabled; the component polls once per
    /// <see cref="RetryIntervalSec"/> until the store is initialised.</para>
    /// </summary>
    public class ShopOfferButton : MonoBehaviour
    {
        private const float RetryIntervalSec = 1f;
        private const string UnknownPriceLabel = "?";

        [SerializeField] private ShopOffer offer;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text priceLabel;
        [Tooltip("Shown over the button when the offer has already been purchased.")]
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
            // Retry pulling the price from the store until we have a real one.
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

            bool isPurchased = PlayerWallet.HasPurchased(offer.OfferId);
            bool isAvailable = offer.IsAvailable;

            // Locked: not bought AND not currently available (e.g. Starter Pack before
            // the NoAds prerequisite is met). Hide the whole row so it doesn't show as OWNED.
            if (!isPurchased && !isAvailable)
            {
                gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (titleLabel != null) titleLabel.text = offer.DisplayName;

            // Pull price. Empty / "—" means store not ready yet.
            string price = ShopManager.Instance != null
                ? ShopManager.Instance.GetLocalizedPrice(offer)
                : null;
            bool priceKnown = !string.IsNullOrEmpty(price) && price != "—";
            lastPriceKnown = priceKnown;

            if (priceLabel != null)
                priceLabel.text = priceKnown ? price : UnknownPriceLabel;

            // OWNED overlay shows ONLY for actual purchases. Buy is disabled if
            // owned OR if the store hasn't returned a price yet.
            if (ownedOverlay != null) ownedOverlay.SetActive(isPurchased);
            if (buyButton    != null) buyButton.interactable = !isPurchased && priceKnown;
        }

        private void OnBuyClicked()
        {
            if (offer == null || ShopManager.Instance == null) return;
            ShopManager.Instance.BuyOffer(offer, _ => Refresh());
        }
    }
}
