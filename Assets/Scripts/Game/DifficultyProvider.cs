using System;
using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Game
{
    /// <summary>
    /// Resolves a level's difficulty from a JSON table (level id → difficulty) and looks up its colour /
    /// reward multiplier from a <see cref="DifficultyConfig"/>. Injected once by AppBootstrap so the menu
    /// (before entering) and gameplay can both query it. Levels not listed in the JSON are Normal.
    ///
    /// <para>JSON format:
    /// <code>{ "levels": [ { "id": 4, "difficulty": "Hard" }, { "id": 8, "difficulty": "SuperHard" } ] }</code>
    /// where <c>id</c> is the 1-based level number (DisplayLevel).</para>
    /// </summary>
    public static class DifficultyProvider
    {
        private static DifficultyConfig config;
        private static Dictionary<int, LevelDifficulty> table;

        [Serializable] private class Entry { public int id; public string difficulty; }
        [Serializable] private class Table { public Entry[] levels; }

        /// <summary>Wire the config (with its level-table JSON). Call once from AppBootstrap.</summary>
        public static void Configure(DifficultyConfig cfg)
        {
            config = cfg;
            table = Parse(cfg != null ? cfg.LevelTableJson : null);
        }

        private static Dictionary<int, LevelDifficulty> Parse(TextAsset json)
        {
            var map = new Dictionary<int, LevelDifficulty>();
            if (json == null || string.IsNullOrWhiteSpace(json.text)) return map;
            Table parsed;
            try { parsed = JsonUtility.FromJson<Table>(json.text); }
            catch (Exception e) { Debug.LogError($"[Difficulty] Failed to parse level table JSON: {e.Message}"); return map; }
            if (parsed?.levels == null) return map;
            foreach (var e in parsed.levels)
            {
                if (e == null) continue;
                if (Enum.TryParse<LevelDifficulty>(e.difficulty, true, out var d)) map[e.id] = d;
                else Debug.LogWarning($"[Difficulty] Unknown difficulty '{e.difficulty}' for level id {e.id} — treated as Normal.");
            }
            return map;
        }

        /// <summary>Difficulty of the level the player is about to / currently playing (by DisplayLevel).</summary>
        public static LevelDifficulty Current => DifficultyForLevel(PlayerProgress.DisplayLevel);

        /// <summary>Difficulty for a 1-based level id. Not listed → Normal.</summary>
        public static LevelDifficulty DifficultyForLevel(int levelId)
        {
            if (table != null && table.TryGetValue(levelId, out var d)) return d;
            return LevelDifficulty.Normal;
        }

        public static Color ColorFor(LevelDifficulty d) => config != null ? config.For(d).color : Color.white;
        public static int RewardMultiplierFor(LevelDifficulty d) => config != null ? config.For(d).rewardMultiplier : 1;

        public static Color CurrentColor => ColorFor(Current);
        public static int CurrentRewardMultiplier => RewardMultiplierFor(Current);
    }
}
