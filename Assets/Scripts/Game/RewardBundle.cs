using System.Collections.Generic;
using PixelShoot.Ads;
using PixelShoot.Data;

namespace PixelShoot.Game
{
    /// <summary>
    /// A parcel of rewards to hand the player: coins, boosters, start-of-level powerups, lives
    /// (finite or timed-unlimited) and/or No-Ads. Built by a shop offer (or any future source) and
    /// passed to <see cref="PixelShoot.UI.RewardFlow.Claim"/>.
    ///
    /// <para><b>Grant is decoupled from presentation.</b> <see cref="Apply"/> writes everything to the
    /// persistent <c>Player*</c> stores the MOMENT the reward is produced — BEFORE the claim popup or
    /// any fly animation. So if the player never presses Continue (or kills the app), the reward is
    /// already saved. The popup and the coin/life fly-and-count-up are pure visualisation of a grant
    /// that already happened.</para>
    /// </summary>
    public class RewardBundle
    {
        public struct BoosterLine { public BoosterData booster; public int amount; }

        public int Coins;
        public readonly List<BoosterLine> Boosters = new List<BoosterLine>();
        public int BombPowerups;
        public int PaintPowerups;
        public double UnlimitedMinutes;
        public int Lives;            // finite +lives (PlayerLives clamps to the cap)
        public bool GrantsNoAds;

        public bool IsEmpty =>
            Coins <= 0 && Boosters.Count == 0 && BombPowerups <= 0 && PaintPowerups <= 0 &&
            UnlimitedMinutes <= 0 && Lives <= 0 && !GrantsNoAds;

        // ── Fluent builders ──────────────────────────────────────────────────
        public RewardBundle AddCoins(int n)            { if (n > 0) Coins += n; return this; }
        public RewardBundle AddBooster(BoosterData b, int n) { if (b != null && n > 0) Boosters.Add(new BoosterLine { booster = b, amount = n }); return this; }
        public RewardBundle AddBomb(int n)             { if (n > 0) BombPowerups += n; return this; }
        public RewardBundle AddPaint(int n)            { if (n > 0) PaintPowerups += n; return this; }
        public RewardBundle AddUnlimited(double mins)  { if (mins > 0) UnlimitedMinutes += mins; return this; }
        public RewardBundle AddLives(int n)            { if (n > 0) Lives += n; return this; }
        public RewardBundle AddNoAds()                 { GrantsNoAds = true; return this; }

        /// <summary>
        /// Write the whole bundle to the persistent stores NOW. Call this EXACTLY ONCE per produced
        /// reward — <see cref="PixelShoot.UI.RewardFlow.Claim"/> does. Idempotency across app restarts
        /// is not this method's job; the store event fires so any live (non-held) HUD refreshes.
        /// </summary>
        public void Apply()
        {
            if (Coins > 0) PlayerWallet.Add(Coins);

            foreach (var line in Boosters)
                if (line.booster != null && line.amount > 0)
                    PlayerBoosters.Add(line.booster.Id, line.amount);

            if (BombPowerups > 0)  PlayerPowerups.Add(PowerupType.Bomb, BombPowerups);
            if (PaintPowerups > 0) PlayerPowerups.Add(PowerupType.Paint, PaintPowerups);

            if (UnlimitedMinutes > 0) PlayerLives.GrantUnlimited(UnlimitedMinutes);
            if (Lives > 0) PlayerLives.AddLives(Lives);

            if (GrantsNoAds && !PlayerWallet.HasNoAds)
            {
                PlayerWallet.MarkNoAdsBought();
                AdsManager.SuppressAdsAfterNoAdsPurchase();
            }
        }
    }
}
