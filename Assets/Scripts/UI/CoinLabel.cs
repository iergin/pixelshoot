using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Renders the PlayerWallet balance to a Text / TMP_Text. Subscribes to the wallet event so
    /// updates are instant — no per-frame polling.
    ///
    /// <para><b>Claim hold.</b> During a reward claim the label can be FROZEN (<see cref="BeginClaimHold"/>)
    /// so it ignores the balance jump while the coins fly, then counted up to the new balance
    /// (<see cref="ReleaseClaimTo"/>) when they land. <see cref="EndClaimImmediate"/> is the safety
    /// release if the claim popup is dismissed without flying.</para>
    /// </summary>
    public class CoinLabel : MonoBehaviour
    {
        [SerializeField] private Text uiText;
        [SerializeField] private TMP_Text tmpText;
        [Tooltip("Format string. {0} = current coin balance.")]
        [SerializeField] private string format = "{0}";

        private bool held;
        private int shownValue;
        private Tween countTween;

        private void OnEnable()
        {
            PlayerWallet.OnBalanceChanged += Refresh;
            held = false;
            Refresh(PlayerWallet.Balance);
        }

        private void OnDisable()
        {
            PlayerWallet.OnBalanceChanged -= Refresh;
            countTween?.Kill();
        }

        public void Refresh(int balance)
        {
            if (held) return; // frozen during a claim — the fly animation releases it later
            shownValue = balance;
            SetText(balance);
        }

        // ── Reward-claim hold / release ──────────────────────────────────────
        /// <summary>Freeze the label at its current displayed value (ignore live balance changes).</summary>
        public void BeginClaimHold()
        {
            countTween?.Kill();
            held = true;
        }

        /// <summary>Count up from the frozen value to <paramref name="value"/>, then resume live updates.</summary>
        public void ReleaseClaimTo(int value, float duration)
        {
            countTween?.Kill();
            held = false; // events will follow live again after the tween sets the final value
            if (duration <= 0f || shownValue == value)
            {
                Refresh(PlayerWallet.Balance);
                return;
            }
            int from = shownValue;
            countTween = DOTween.To(() => from, x => { from = x; shownValue = x; SetText(x); }, value, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() => Refresh(PlayerWallet.Balance));
        }

        /// <summary>Release immediately to the real balance (no count-up). Safety net.</summary>
        public void EndClaimImmediate()
        {
            countTween?.Kill();
            held = false;
            Refresh(PlayerWallet.Balance);
        }

        private void SetText(int v)
        {
            string s = string.Format(format, v);
            if (uiText != null) uiText.text = s;
            if (tmpText != null) tmpText.text = s;
        }
    }
}
