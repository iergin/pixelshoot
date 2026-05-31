using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Persistent player state. Right now that's just the current level index, stored
    /// in PlayerPrefs so it survives quit / replay. LevelIndex is 0-based; DisplayLevel
    /// is the 1-based number you show in the HUD.
    /// </summary>
    public static class PlayerProgress
    {
        private const string LevelIndexKey = "PixelShoot.LevelIndex";

        public static int LevelIndex
        {
            get => PlayerPrefs.GetInt(LevelIndexKey, 0);
            set
            {
                PlayerPrefs.SetInt(LevelIndexKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Player-facing 1-based number. "Level 1" for a fresh save.</summary>
        public static int DisplayLevel => LevelIndex + 1;

        public static void Advance() => LevelIndex = LevelIndex + 1;
        public static void Reset() => LevelIndex = 0;
    }
}
