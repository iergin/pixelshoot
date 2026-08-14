using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Per-level attempt counter, persisted in PlayerPrefs. An "attempt" is one fresh entry into a level
    /// (first play, or a retry after failing/quitting) — a mid-level revive/Play-On does NOT start a new
    /// attempt. The count is 1-based: the first time a level is entered it reports 1. Winning a level
    /// clears its counter, so a later replay (e.g. once the playlist loops) starts again at 1.
    ///
    /// <para>Used for the <c>attempt_no</c> analytics parameter (Jira KAN-25).</para>
    /// </summary>
    public static class PlayerAttempts
    {
        private const string Prefix = "PixelShoot.Attempt.";

        private static string Key(string levelId) => Prefix + (levelId ?? "");

        /// <summary>Current attempt number for a level without changing it (0 if never entered).</summary>
        public static int Peek(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return 0;
            return PlayerPrefs.GetInt(Key(levelId), 0);
        }

        /// <summary>Register a fresh entry into a level and return the new (1-based) attempt number.</summary>
        public static int RegisterAttempt(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return 1;
            int next = PlayerPrefs.GetInt(Key(levelId), 0) + 1;
            PlayerPrefs.SetInt(Key(levelId), next);
            PlayerPrefs.Save();
            return next;
        }

        /// <summary>Clear a level's attempt counter (call on win) so a later replay restarts at 1.</summary>
        public static void ClearAttempts(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return;
            PlayerPrefs.DeleteKey(Key(levelId));
            PlayerPrefs.Save();
        }
    }
}
