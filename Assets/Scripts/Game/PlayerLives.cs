using System;
using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Classic lives / hearts economy backed by PlayerPrefs.
    ///
    /// <list type="bullet">
    /// <item>Cap of <see cref="MaxLives"/> (5). Starts full.</item>
    /// <item>One life regenerates every <see cref="RegenIntervalMinutes"/> (30) minutes,
    /// computed lazily from a stored anchor timestamp — no ticking needed, so it keeps
    /// counting while the app is closed.</item>
    /// <item>A life is spent the moment a level STARTS (see
    /// <see cref="TryConsumeForLevelStart"/>): once started it's gone, whether the player
    /// wins, loses, quits, or backs out to the menu.</item>
    /// </list>
    /// </summary>
    public static class PlayerLives
    {
        public const int MaxLives = 5;
        public const double RegenIntervalMinutes = 30.0;

        private const string LivesKey     = "PixelShoot.Lives";
        private const string AnchorKey    = "PixelShoot.LivesRegenAnchorUtc";   // ISO-8601 UTC
        private const string UnlimitedKey = "PixelShoot.UnlimitedLivesUntilUtc"; // ISO-8601 UTC

        /// <summary>Fired after lives change (spend / regen / add / unlimited granted). Payload = current lives.</summary>
        public static event Action<int> OnChanged;

        /// <summary>Current lives, after applying any regen that has accrued since the last check.</summary>
        public static int Lives => Recompute();

        public static bool IsFull => Lives >= MaxLives;

        // ── Unlimited (timed) lives ──────────────────────────────────────────
        /// <summary>True while a timed "unlimited lives" period is active — levels don't cost a life.</summary>
        public static bool IsUnlimited => UnlimitedUntil() > DateTime.UtcNow;

        /// <summary>Seconds left on the unlimited-lives period, or 0 when inactive.</summary>
        public static float SecondsUntilUnlimitedEnds()
        {
            double rem = (UnlimitedUntil() - DateTime.UtcNow).TotalSeconds;
            return rem > 0 ? (float)rem : 0f;
        }

        /// <summary>Grant (or extend) unlimited lives for <paramref name="minutes"/> minutes. If a
        /// period is already running the time is ADDED on top. Call from an IAP / rewarded reward.</summary>
        public static void GrantUnlimited(double minutes)
        {
            if (minutes <= 0) return;
            DateTime baseTime = IsUnlimited ? UnlimitedUntil() : DateTime.UtcNow;
            PlayerPrefs.SetString(UnlimitedKey, baseTime.AddMinutes(minutes).ToString("o"));
            PlayerPrefs.Save();
            OnChanged?.Invoke(Recompute()); // refresh HUD (∞)
        }

        /// <summary>End the unlimited period immediately (debug).</summary>
        public static void ClearUnlimited()
        {
            PlayerPrefs.DeleteKey(UnlimitedKey);
            PlayerPrefs.Save();
            OnChanged?.Invoke(Recompute());
        }

        private static DateTime UnlimitedUntil()
        {
            string s = PlayerPrefs.GetString(UnlimitedKey, "");
            if (!string.IsNullOrEmpty(s) &&
                DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return DateTime.MinValue;
        }

        /// <summary>
        /// Spend one life for starting a level. Returns false (and changes nothing) when the
        /// player is out of lives — callers should block the level and show a wait/refill UI.
        /// </summary>
        public static bool TryConsumeForLevelStart()
        {
            // Unlimited period → free to start, no life spent (and nothing to refund on win).
            if (IsUnlimited) return true;

            int lives = Recompute();
            if (lives <= 0) return false;

            bool wasFull = lives >= MaxLives;
            // If we were full, the regen timer wasn't running — start it now. Otherwise the
            // anchor is already mid-cycle and must be kept so in-progress regen continues.
            DateTime anchor = wasFull ? DateTime.UtcNow : GetAnchor();
            Save(lives - 1, anchor);
            startLifeSpent = true; // remember so a WIN can refund it
            return true;
        }

        // True after a level-start life was spent, cleared once refunded on a win. Static so
        // it survives the menu→game transition; resets naturally on domain reload.
        private static bool startLifeSpent;

        /// <summary>
        /// Winning is free: give back the life spent at level start (if any). Call on level win.
        /// No-op if no start-life is pending, so it can't grant a free extra life.
        /// </summary>
        public static void RefundLevelStart()
        {
            if (!startLifeSpent) return;
            startLifeSpent = false;
            AddLives(1);
        }

        /// <summary>Grant lives (ad reward / purchase / refill). Clamped to the cap.</summary>
        public static void AddLives(int count = 1)
        {
            if (count <= 0) return;
            int lives = Recompute();
            int newLives = Mathf.Min(MaxLives, lives + count);
            if (newLives == lives) return;
            DateTime anchor = newLives >= MaxLives ? DateTime.UtcNow : GetAnchor();
            Save(newLives, anchor);
        }

        /// <summary>Refill to full (e.g. an IAP).</summary>
        public static void Refill() => AddLives(MaxLives);

        /// <summary>Force the life count (debug / cheats). Restarts the regen cycle from now.</summary>
        public static void SetLives(int count)
        {
            Save(Mathf.Clamp(count, 0, MaxLives), DateTime.UtcNow);
        }

        /// <summary>Seconds until the next life regenerates, or 0 when already full.</summary>
        public static float SecondsUntilNextLife()
        {
            if (Recompute() >= MaxLives) return 0f;
            double sinceMin = (DateTime.UtcNow - GetAnchor()).TotalMinutes;
            double remMin = RegenIntervalMinutes - (sinceMin % RegenIntervalMinutes);
            if (remMin < 0) remMin = 0;
            return (float)(remMin * 60.0);
        }

        // ── internals ────────────────────────────────────────────────────────
        private static int Recompute()
        {
            int stored = PlayerPrefs.GetInt(LivesKey, MaxLives); // first run → full
            if (stored >= MaxLives)
            {
                if (stored != MaxLives) { stored = MaxLives; PlayerPrefs.SetInt(LivesKey, stored); PlayerPrefs.Save(); }
                return MaxLives;
            }

            DateTime anchor = GetAnchor();
            double mins = (DateTime.UtcNow - anchor).TotalMinutes;
            int gained = mins > 0 ? (int)(mins / RegenIntervalMinutes) : 0;
            if (gained <= 0) return stored;

            int newLives = Mathf.Min(MaxLives, stored + gained);
            DateTime newAnchor = newLives >= MaxLives
                ? DateTime.UtcNow
                : anchor.AddMinutes(gained * RegenIntervalMinutes);
            Save(newLives, newAnchor);
            return newLives;
        }

        private static DateTime GetAnchor()
        {
            string s = PlayerPrefs.GetString(AnchorKey, "");
            if (!string.IsNullOrEmpty(s) &&
                DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            // No anchor yet → start a cycle now.
            var now = DateTime.UtcNow;
            PlayerPrefs.SetString(AnchorKey, now.ToString("o"));
            PlayerPrefs.Save();
            return now;
        }

        private static void Save(int lives, DateTime anchor)
        {
            lives = Mathf.Clamp(lives, 0, MaxLives);
            PlayerPrefs.SetInt(LivesKey, lives);
            PlayerPrefs.SetString(AnchorKey, anchor.ToString("o"));
            PlayerPrefs.Save();
            OnChanged?.Invoke(lives);
        }
    }
}
