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

        /// <summary>Reward stops scaling past this streak (streak 3 = the top tier).</summary>
        public const int MaxRewardStreak = 3;

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

        /// <summary>Streak clamped to the reward cap — the tier the start-of-level gift uses.</summary>
        public static int RewardTier => Mathf.Clamp(Current, 0, MaxRewardStreak);

        /// <summary>Streak bombs to drop this level: 3 per tier (0/3/6/9).</summary>
        public static int RewardBombs => RewardTier * 3;

        /// <summary>Free painted pixels this level: 0/5/15/25 by tier.</summary>
        public static int RewardPaints => RewardTier == 0 ? 0 : RewardTier * 10 - 5;

        /// <summary>A level was cleared → extend the streak.</summary>
        public static void RegisterWin() => Current = Current + 1;

        /// <summary>The streak is broken (final fail or quit).</summary>
        public static void Reset() => Current = 0;
    }
}
