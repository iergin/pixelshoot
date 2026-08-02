using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Celebrates a <see cref="RewardBundle"/> the player just earned (shop purchase today). Shows one
    /// <see cref="RewardRow"/> per reward, then on <b>Continue</b>:
    /// <list type="bullet">
    /// <item><b>Menu</b> (a <see cref="RewardFlyTargets"/> exists): the icons fly to the HUD — coins to
    /// the coin counter (which then counts up), boosters/powerups/No-Ads to the Play button, life to
    /// the life widget — then the popup closes.</item>
    /// <item><b>In-game</b> (no fly targets): it just closes. Nothing flies.</item>
    /// </list>
    /// The grant itself already happened in <see cref="RewardFlow.Claim"/> before this popup opened, so
    /// closing it any other way (or killing the app) never loses the reward.
    /// </summary>
    public class RewardClaimPopup : BasePopup
    {
        [Header("Reward icons")]
        [Tooltip("Shared coin / life / no-ads / bomb / paint sprites (a RewardIconSet asset).")]
        [SerializeField] private RewardIconSet icons;

        [Header("Rows")]
        [Tooltip("Row prefab (icon + amount). One is instantiated per reward under Rows Container.")]
        [SerializeField] private RewardRow rowPrefab;
        [SerializeField] private Transform rowsContainer;

        [Header("Buttons")]
        [Tooltip("Continue: starts the fly (menu) or just closes (in-game).")]
        [SerializeField] private Button continueButton;

        private RewardBundle bundle;
        private RewardFlyTargets targets;
        private readonly List<(RewardRow row, RewardFlyKind kind)> rows = new List<(RewardRow, RewardFlyKind)>();
        private bool claimed;
        private bool flyStarted;

        /// <summary>Called by <see cref="RewardFlow.Claim"/> BEFORE OnInit — sets what to show and
        /// whether to fly (targets == null → in-game, no fly).</summary>
        public void Setup(RewardBundle b, RewardFlyTargets t)
        {
            bundle = b;
            targets = t;
        }

        protected override void OnInit()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinue);
            }
            BuildRows();
        }

        private void BuildRows()
        {
            rows.Clear();
            if (bundle == null || rowPrefab == null || rowsContainer == null) return;

            if (bundle.Coins > 0) AddRow(icons?.Coin, $"+{bundle.Coins}", RewardFlyKind.Coin);

            foreach (var line in bundle.Boosters)
                if (line.booster != null && line.amount > 0)
                    AddRow(line.booster.Icon, $"x{line.amount}", RewardFlyKind.PlayButton);

            if (bundle.BombPowerups > 0)  AddRow(icons?.Bomb,  $"x{bundle.BombPowerups}",  RewardFlyKind.PlayButton);
            if (bundle.PaintPowerups > 0) AddRow(icons?.Paint, $"x{bundle.PaintPowerups}", RewardFlyKind.PlayButton);

            if (bundle.UnlimitedMinutes > 0) AddRow(icons?.Life, DurationText(bundle.UnlimitedMinutes), RewardFlyKind.Life);
            if (bundle.Lives > 0)            AddRow(icons?.Life, $"x{bundle.Lives}", RewardFlyKind.Life);

            if (bundle.GrantsNoAds) AddRow(icons?.NoAds, "", RewardFlyKind.PlayButton); // no amount text
        }

        private void AddRow(Sprite sprite, string amountText, RewardFlyKind kind)
        {
            var row = Instantiate(rowPrefab, rowsContainer);
            row.gameObject.SetActive(true);
            row.Set(sprite, amountText);
            rows.Add((row, kind));
        }

        private void OnContinue()
        {
            if (claimed) return;
            claimed = true;
            if (continueButton != null) continueButton.interactable = false;

            // Menu: capture each row icon's position and fly them to the HUD, then close.
            if (targets != null && rows.Count > 0)
            {
                var reqs = new List<RewardFlyTargets.FlyRequest>(rows.Count);
                foreach (var (row, kind) in rows)
                    reqs.Add(new RewardFlyTargets.FlyRequest
                    {
                        kind = kind,
                        sprite = row.IconSprite,
                        startWorld = row.IconRect.position, // captured NOW, before Close() destroys the row
                    });
                flyStarted = true;
                targets.Fly(reqs, null); // fly runs independently on the fly layer; popup can close now
            }

            Close();
        }

        protected override void OnPopupClosing()
        {
            // Closed without ever starting the fly (e.g. in-game, or a stray close) → make sure the
            // frozen HUD snaps to the real, already-granted values so nothing stays stuck.
            if (targets != null && !flyStarted) targets.EndHoldImmediate();
        }

        // "12h", "12h 30m", or "45m".
        private static string DurationText(double minutes)
        {
            int m = Mathf.Max(0, (int)minutes);
            if (m < 60) return $"{m}m";
            int h = m / 60, rem = m % 60;
            return rem == 0 ? $"{h}h" : $"{h}h {rem}m";
        }
    }
}
