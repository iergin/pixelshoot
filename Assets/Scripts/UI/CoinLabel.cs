using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Renders the PlayerWallet balance to a Text / TMP_Text. Subscribes to the
    /// wallet event so updates are instant — no per-frame polling.
    /// </summary>
    public class CoinLabel : MonoBehaviour
    {
        [SerializeField] private Text uiText;
        [SerializeField] private TMP_Text tmpText;
        [Tooltip("Format string. {0} = current coin balance.")]
        [SerializeField] private string format = "{0}";

        private void OnEnable()
        {
            PlayerWallet.OnBalanceChanged += Refresh;
            Refresh(PlayerWallet.Balance);
        }

        private void OnDisable()
        {
            PlayerWallet.OnBalanceChanged -= Refresh;
        }

        public void Refresh(int balance)
        {
            string s = string.Format(format, balance);
            if (uiText != null) uiText.text = s;
            if (tmpText != null) tmpText.text = s;
        }
    }
}
