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
    /// The script handles availability gating, localized price, and the purchase callback.
    /// </summary>
    public class ShopOfferButton : MonoBehaviour
    {
        [SerializeField] private ShopOffer offer;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text priceLabel;
        [Tooltip("Shown over the button when the offer has already been purchased.")]
        [SerializeField] private GameObject ownedOverlay;

        private void OnEnable()
        {
            PlayerWallet.OnBalanceChanged += _ => Refresh();
            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyClicked);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (offer == null) return;
            if (titleLabel != null) titleLabel.text = offer.DisplayName;
            if (priceLabel != null && ShopManager.Instance != null)
                priceLabel.text = ShopManager.Instance.GetLocalizedPrice(offer);

            bool available = offer.IsAvailable;
            if (buyButton != null) buyButton.interactable = available;
            if (ownedOverlay != null) ownedOverlay.SetActive(!available);
        }

        private void OnBuyClicked()
        {
            if (offer == null || ShopManager.Instance == null) return;
            ShopManager.Instance.BuyOffer(offer, success =>
            {
                Refresh();
            });
        }
    }
}
