using UnityEngine;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// The shop popup. Its content is a set of <see cref="ShopOfferButton"/> rows that each drive
    /// themselves off <see cref="PixelShoot.Shop.ShopManager"/>.Instance (price polling, availability,
    /// purchase), so this class only needs to be a <see cref="BasePopup"/> host — the buttons refresh
    /// on their own when the popup is instantiated. Opened via
    /// <c>PopupService.Instance.Create&lt;ShopPopup&gt;()</c> (ShopManager.OpenShop does this).
    /// </summary>
    public class ShopPopup : BasePopup
    {
        [Header("Readouts (optional)")]
        [SerializeField] private TMP_Text balanceLabel;

        protected override void OnInit()
        {
            if (balanceLabel != null) balanceLabel.text = PlayerWallet.Balance.ToString();
        }

        protected override void OnPopupOpened()
        {
            PlayerWallet.OnBalanceChanged += OnBalance;
        }

        protected override void OnPopupClosing()
        {
            PlayerWallet.OnBalanceChanged -= OnBalance;
        }

        private void OnBalance(int balance)
        {
            if (balanceLabel != null) balanceLabel.text = balance.ToString();
        }
    }
}
