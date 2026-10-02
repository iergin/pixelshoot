using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelShoot.UI
{
    /// <summary>One reward line inside the <see cref="RewardClaimPopup"/>: an icon and an amount label.
    /// The icon's RectTransform is the fly ORIGIN when the player presses Continue.</summary>
    public class RewardRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountLabel;

        public RectTransform IconRect => icon != null ? (RectTransform)icon.transform : (RectTransform)transform;
        public Sprite IconSprite => icon != null ? icon.sprite : null;

        public void Set(Sprite sprite, string amountText)
        {
            if (icon != null) icon.sprite = sprite;
            if (amountLabel != null)
            {
                bool has = !string.IsNullOrEmpty(amountText);
                amountLabel.text = has ? amountText : "";
                amountLabel.gameObject.SetActive(has); // No-Ads has no amount → hide the label
            }
        }
    }
}
