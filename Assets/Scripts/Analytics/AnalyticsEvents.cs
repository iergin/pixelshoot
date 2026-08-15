using System.Collections.Generic;

namespace PixelShoot.Analytics
{
    /// <summary>
    /// Typed payloads for the game's tracking events (see Jira KAN-25). Each struct mirrors one event's
    /// parameter contract exactly (names + types), builds its parameter dictionary, and fires through
    /// <see cref="AnalyticsManager"/>. Keeping the schema here means the parameter names live in ONE place.
    /// </summary>
    public static class AnalyticsEvents
    {
        public const string LevelStart    = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail     = "level_fail";

        /// <summary>Fired when a level starts (after it is built).</summary>
        public struct LevelStartData
        {
            public string level_id;       // Unity level name (LevelData match id)
            public int level_index;       // player-facing level number, 1-based
            public int attempt_no;        // this player's attempt on this level, 1-based
            public int pixel_count;       // fillable cells
            public int color_count;       // distinct colours
            public int shooter_count;     // total shooters
            public int linked_groups;     // 🔗 groups
            public int lock_count;
            public int bomb_count;
            public int streak_count;
            public int power_up_bomb;     // bomb powerup active (0/1)
            public int power_up_shoot;    // paint/shoot powerup active (0/1)
            public int coin_amount;       // coin balance at start

            public void Track()
            {
                AnalyticsManager.Track(LevelStart, new Dictionary<string, object>
                {
                    { "level_id", level_id },
                    { "level_index", level_index },
                    { "attempt_no", attempt_no },
                    { "pixel_count", pixel_count },
                    { "color_count", color_count },
                    { "shooter_count", shooter_count },
                    { "linked_groups", linked_groups },
                    { "lock_count", lock_count },
                    { "bomb_count", bomb_count },
                    { "streak_count", streak_count },
                    { "power_up_bomb", power_up_bomb },
                    { "power_up_shoot", power_up_shoot },
                    { "coin_amount", coin_amount },
                });
            }
        }

        /// <summary>Fired when a level is completed (won).</summary>
        public struct LevelCompleteData
        {
            public string level_id;
            public int level_index;
            public int attempt_no;
            public int time_sec;             // seconds on the level screen
            public int moves_made;           // total shots (buses boarded)
            public int moves_par;            // solver's move count (0 if unknown)
            public int max_slots_used;       // peak conveyor slot occupancy
            public int near_overflow_count;  // times occupancy hit capacity-1
            public int boosters_used;
            public int revived_count;        // revives this attempt
            public int coins_earned;

            public void Track()
            {
                AnalyticsManager.Track(LevelComplete, new Dictionary<string, object>
                {
                    { "level_id", level_id },
                    { "level_index", level_index },
                    { "attempt_no", attempt_no },
                    { "time_sec", time_sec },
                    { "moves_made", moves_made },
                    { "moves_par", moves_par },
                    { "max_slots_used", max_slots_used },
                    { "near_overflow_count", near_overflow_count },
                    { "boosters_used", boosters_used },
                    { "revived_count", revived_count },
                    { "coins_earned", coins_earned },
                });
            }
        }

        /// <summary>Fired when a level is failed or quit.</summary>
        public struct LevelFailData
        {
            public string level_id;
            public int level_index;
            public int attempt_no;
            public int progress_pct;         // percent of pixels filled
            public string fail_reason;       // "fail" | "quit"
            public int time_sec;
            public int moves_made;
            public int shooters_left;        // shooters still in columns
            public int near_overflow_count;
            public int boosters_used;
            public int continues_used;       // revives (PlayOn) used this attempt

            public void Track()
            {
                AnalyticsManager.Track(LevelFail, new Dictionary<string, object>
                {
                    { "level_id", level_id },
                    { "level_index", level_index },
                    { "attempt_no", attempt_no },
                    { "progress_pct", progress_pct },
                    { "fail_reason", fail_reason },
                    { "time_sec", time_sec },
                    { "moves_made", moves_made },
                    { "shooters_left", shooters_left },
                    { "near_overflow_count", near_overflow_count },
                    { "boosters_used", boosters_used },
                    { "continues_used", continues_used },
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Shop / IAP + meta-interaction events (all discrete — never fired from Update loops).
        // ─────────────────────────────────────────────────────────────────────
        public const string ShopOpen       = "shop_open";
        public const string PaymentRequest = "payment_request";
        public const string PaymentSuccess = "payment_success";
        public const string PaymentFail    = "payment_fail";
        public const string PowerupSelect  = "powerup_select";
        public const string BoosterUse     = "booster_use";
        public const string UiButtonClick  = "ui_button_click";

        /// <summary>Shop opened. <paramref name="source"/> = where from ("menu", "out_of_lives",
        /// "revive_insufficient_coins", "powerup_slot", "booster_purchase", …).</summary>
        public static void TrackShopOpen(string source) =>
            AnalyticsManager.Track(ShopOpen, new Dictionary<string, object>
            {
                { "source", string.IsNullOrEmpty(source) ? "unknown" : source },
            });

        /// <summary>Player initiated a purchase (tapped Buy). <paramref name="price"/> is the localized
        /// store price string.</summary>
        public static void TrackPaymentRequest(string productId, string offerId, string price) =>
            AnalyticsManager.Track(PaymentRequest, new Dictionary<string, object>
            {
                { "product_id", productId },
                { "offer_id", offerId },
                { "price", price },
            });

        public static void TrackPaymentSuccess(string productId, string offerId) =>
            AnalyticsManager.Track(PaymentSuccess, new Dictionary<string, object>
            {
                { "product_id", productId },
                { "offer_id", offerId },
            });

        /// <summary><paramref name="reason"/> = "not_available" | "iap_not_ready" | "failed".</summary>
        public static void TrackPaymentFail(string productId, string offerId, string reason) =>
            AnalyticsManager.Track(PaymentFail, new Dictionary<string, object>
            {
                { "product_id", productId },
                { "offer_id", offerId },
                { "reason", reason },
            });

        /// <summary>A powerup slot was toggled. <paramref name="powerupType"/> = "Bomb" | "Paint".</summary>
        public static void TrackPowerupSelect(string powerupType, bool selected) =>
            AnalyticsManager.Track(PowerupSelect, new Dictionary<string, object>
            {
                { "powerup_type", powerupType },
                { "selected", selected ? 1 : 0 },
            });

        /// <summary>A booster was consumed (used) in a level.</summary>
        public static void TrackBoosterUse(string boosterId) =>
            AnalyticsManager.Track(BoosterUse, new Dictionary<string, object>
            {
                { "booster_id", boosterId },
            });

        /// <summary>A tracked UI button was clicked. <paramref name="buttonId"/> identifies the button.</summary>
        public static void TrackUiButtonClick(string buttonId) =>
            AnalyticsManager.Track(UiButtonClick, new Dictionary<string, object>
            {
                { "button_id", buttonId },
            });
    }
}
