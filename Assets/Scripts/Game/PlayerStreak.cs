using System;
using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Consecutive-clear streak, persisted in PlayerPrefs. Increments on a level WIN and resets
    /// when the player abandons a level without clearing it (leaving a failed level or quitting).
    /// A "Play On" continue does NOT reset it.
    ///
    /// <para>The streak drives per-level start-of-level gifts (bombs + free painted pixels). The
    /// reward amount is capped at <see cref="MaxRewardStreak"/> (streak beyond that keeps counting
    /// but the gift stops growing).</para>
    /// </summary>
    public static class PlayerStreak
    {
        private const string StreakKey = "PixelShoot.Streak";

        // ─────────────────────────────────────────────────────────────────────
        //  STREAK TUNING lives in a StreakConfig ScriptableObject (edit it in the Inspector).
        //  Resolved lazily: an explicitly-injected config (see Configure) wins; else a
        //  Resources/StreakConfig asset is auto-loaded; else built-in defaults are used.
        // ─────────────────────────────────────────────────────────────────────
        private static StreakConfig config;

        private static StreakConfig Cfg
        {
            get
            {
                if (config != null) return config;
                config = Resources.Load<StreakConfig>("StreakConfig");
                if (config == null)
                {
                    Debug.LogWarning("[PlayerStreak] No StreakConfig found (put one in a Resources folder " +
                                     "named 'StreakConfig', or inject via PlayerStreak.Configure / AppBootstrap). " +
                                     "Using built-in defaults for now.");
                    config = ScriptableObject.CreateInstance<StreakConfig>(); // fields already hold defaults
                }
                return config;
            }
        }

        /// <summary>Inject the streak config explicitly (e.g. from AppBootstrap) instead of relying on
        /// the Resources auto-load.</summary>
        public static void Configure(StreakConfig cfg) { if (cfg != null) config = cfg; }

        /// <summary>Number of streak steps (fill-bar length). Reward + bar cap here.</summary>
        public static int MaxRewardStreak => Cfg.MaxRewardStreak;

        /// <summary>Fired whenever the streak value changes (win / reset).</summary>
        public static event Action<int> OnChanged;

        public static int Current
        {
            get => PlayerPrefs.GetInt(StreakKey, 0);
            private set
            {
                int v = Mathf.Max(0, value);
                PlayerPrefs.SetInt(StreakKey, v);
                PlayerPrefs.Save();
                OnChanged?.Invoke(v);
            }
        }

        /// <summary>Streak clamped to the reward cap — the step the start-of-level gift uses.</summary>
        public static int RewardTier => Mathf.Clamp(Current, 0, MaxRewardStreak);

        /// <summary>Streak bombs to drop this level — from the config at the current step.</summary>
        public static int RewardBombs => Cfg.Bombs(RewardTier);

        /// <summary>Free painted pixels this level — from the config at the current step.</summary>
        public static int RewardPaints => Cfg.Paints(RewardTier);

        /// <summary>A level was cleared → extend the streak.</summary>
        public static void RegisterWin() => Current = Current + 1;

        /// <summary>The streak is broken (final fail or quit).</summary>
        public static void Reset() => Current = 0;
    }
}
