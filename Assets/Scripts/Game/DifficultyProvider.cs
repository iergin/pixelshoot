using System;
using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Game
{
    /// <summary>
    /// Owns the ORDERED level table parsed from a JSON export (the "level order" / difficulty table).
    /// Each entry is <c>{ "id": "&lt;name&gt;", "difficulty": "Hard" }</c> and the array order IS the play
    /// order. For the level at a given 0-based progress index it yields both:
    /// <list type="bullet">
    /// <item>the level's <b>match id</b> (<see cref="NameForLevelIndex"/>) — used by LevelLoader to find
    /// the <see cref="LevelData"/> whose <see cref="LevelData.LevelName"/> equals it, and</item>
    /// <item>its <b>difficulty</b> (colour + reward multiplier from the <see cref="DifficultyConfig"/>).</item>
    /// </list>
    /// Injected once by AppBootstrap so the menu (before entering) and gameplay both query it. Levels not
    /// listed (or past the end, once looped) resolve to Normal.
    ///
    /// <para>JSON format:
    /// <code>{ "levels": [ { "id": "necktie", "difficulty": "Normal" }, { "id": "owl_tree", "difficulty": "Super Hard" } ] }</code>
    /// where <c>id</c> is the level's name (match id) and the array order is the play order.</para>
    /// </summary>
    public static class DifficultyProvider
    {
        private static DifficultyConfig config;
        private static readonly List<Entry> order = new List<Entry>();               // play order (id + difficulty)
        private static readonly Dictionary<string, LevelDifficulty> byName =
            new Dictionary<string, LevelDifficulty>();                                // id → difficulty
        private static int loopStart = 1; // 1-based; play wraps back to here once the index runs past the end

        [Serializable] private class Entry { public string id; public string difficulty; }
        [Serializable] private class Table { public Entry[] levels; }

        /// <summary>Wire the config (with its level-table JSON) and the loop-start (1-based) used when the
        /// progress index runs past the last table entry. Call once from AppBootstrap.</summary>
        public static void Configure(DifficultyConfig cfg, int loopStartLevel = 1)
        {
            config = cfg;
            loopStart = Mathf.Max(1, loopStartLevel);
            Parse(cfg != null ? cfg.LevelTableJson : null);
        }

        private static void Parse(TextAsset json)
        {
            order.Clear();
            byName.Clear();
            if (json == null || string.IsNullOrWhiteSpace(json.text)) return;
            Table parsed;
            try { parsed = JsonUtility.FromJson<Table>(json.text); }
            catch (Exception e) { Debug.LogError($"[Difficulty] Failed to parse level table JSON: {e.Message}"); return; }
            if (parsed?.levels == null) return;
            foreach (var e in parsed.levels)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                order.Add(e);
                if (Enum.TryParse<LevelDifficulty>(e.difficulty, true, out var d)) byName[e.id] = d;
                else
                {
                    Debug.LogWarning($"[Difficulty] Unknown difficulty '{e.difficulty}' for level '{e.id}' — treated as Normal.");
                    byName[e.id] = LevelDifficulty.Normal;
                }
            }
        }

        /// <summary>Number of levels in the order table.</summary>
        public static int OrderCount => order.Count;

        // Map a 0-based progress index to an entry index, looping from loopStart past the end. -1 if empty.
        private static int WrapIndex(int levelIndex)
        {
            if (order.Count == 0) return -1;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex < order.Count) return levelIndex;
            int ls = Mathf.Clamp(loopStart - 1, 0, order.Count - 1); // 0-based
            int len = order.Count - ls;                              // >= 1
            return ls + ((levelIndex - ls) % len);
        }

        /// <summary>The level NAME (match id) to play at a 0-based progress index, or null if the table is
        /// empty. Wraps from the loop-start once the index runs past the end.</summary>
        public static string NameForLevelIndex(int levelIndex)
        {
            int i = WrapIndex(levelIndex);
            return i >= 0 ? order[i].id : null;
        }

        /// <summary>Difficulty for the level at a 0-based progress index (looping). Not listed → Normal.</summary>
        public static LevelDifficulty DifficultyForLevelIndex(int levelIndex)
        {
            int i = WrapIndex(levelIndex);
            if (i < 0) return LevelDifficulty.Normal;
            return byName.TryGetValue(order[i].id, out var d) ? d : LevelDifficulty.Normal;
        }

        /// <summary>Difficulty for a level by its match id (name). Not listed → Normal.</summary>
        public static LevelDifficulty DifficultyForName(string name)
            => (name != null && byName.TryGetValue(name, out var d)) ? d : LevelDifficulty.Normal;

        /// <summary>Difficulty of the level the player is about to / currently playing (by LevelIndex).</summary>
        public static LevelDifficulty Current => DifficultyForLevelIndex(PlayerProgress.LevelIndex);

        public static Color ColorFor(LevelDifficulty d) => config != null ? config.For(d).color : Color.white;
        public static int RewardMultiplierFor(LevelDifficulty d) => config != null ? config.For(d).rewardMultiplier : 1;

        public static Color CurrentColor => ColorFor(Current);
        public static int CurrentRewardMultiplier => RewardMultiplierFor(Current);
    }
}
