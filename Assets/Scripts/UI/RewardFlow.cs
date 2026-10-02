using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Single entry point for handing the player a <see cref="RewardBundle"/> (shop purchase today,
    /// possibly streak / level-end / promo later).
    ///
    /// <para><b>Grant is decoupled from presentation, in TWO steps:</b></para>
    /// <list type="number">
    /// <item><see cref="Grant"/> — freezes the coin/life HUD and <b>writes the reward to disk
    /// immediately</b>, then remembers the bundle as a pending claim. Called the moment the purchase
    /// succeeds, so even if the player never sees the claim popup (or kills the app), the reward is
    /// already saved.</item>
    /// <item><see cref="ShowClaim"/> — opens the <see cref="RewardClaimPopup"/> for that pending bundle.
    /// The shop calls this AFTER its <c>PurchaseSuccessPopup</c> closes, so the order is
    /// success → claim.</item>
    /// </list>
    /// <see cref="Claim"/> does both back-to-back for non-shop sources that want the claim right away.
    /// </summary>
    public static class RewardFlow
    {
        private static RewardBundle pending;

        /// <summary>True while a granted-but-not-yet-shown reward is waiting for <see cref="ShowClaim"/>.</summary>
        public static bool HasPending => pending != null;

        /// <summary>Freeze the HUD, WRITE the reward now (persisted — survives an early exit), and stash
        /// it as the pending claim. Does NOT open any popup.</summary>
        public static void Grant(RewardBundle bundle)
        {
            if (bundle == null || bundle.IsEmpty) return;
            RewardFlyTargets.Instance?.BeginHold(); // menu-only; freezes coin/life so they don't jump
            bundle.Apply();                          // DATA saved here
            pending = bundle;
        }

        /// <summary>Open the claim popup for the pending bundle (call after the success popup closes).
        /// No-op if nothing is pending. Releases the frozen HUD if there's no popup service to show it.</summary>
        public static void ShowClaim()
        {
            var bundle = pending;
            pending = null;
            if (bundle == null) return;

            var targets = RewardFlyTargets.Instance;
            targets?.FocusHome(); // return to the Home tab BEFORE the claim, so the reward flies there

            var popup = PopupService.Instance != null
                ? PopupService.Instance.CreateOnTop<RewardClaimPopup>(p => p.Setup(bundle, targets))
                : null;
            if (popup == null) targets?.EndHoldImmediate(); // couldn't show it → don't leave the HUD frozen
        }

        /// <summary>Grant + show the claim popup immediately (non-shop direct path).</summary>
        public static void Claim(RewardBundle bundle)
        {
            Grant(bundle);
            ShowClaim();
        }
    }
}
