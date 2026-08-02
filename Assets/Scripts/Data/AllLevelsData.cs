using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Ordered playlist of LevelData assets. The player walks through them in order
    /// based on their saved progress (see PixelShoot.Game.PlayerProgress); once they
    /// run past the last entry, play LOOPS from <see cref="LoopStartLevel"/> and repeats.
    /// </summary>
    [CreateAssetMenu(fileName = "AllLevels", menuName = "PixelShoot/All Levels")]
    public class AllLevelsData : ScriptableObject
    {
        [Tooltip("Levels in play order. Index 0 = first level the player sees.")]
        [SerializeField] private List<LevelData> levels = new List<LevelData>();
        [Tooltip("When the player runs PAST the last level, play loops back to THIS level (1-based) and " +
                 "continues to the end, repeating forever. e.g. 11 → after the last level it plays 11, 12, " +
                 "…, last, then 11 again. 1 = loop the whole list.")]
        [SerializeField, Min(1)] private int loopStartLevel = 1;

        public IReadOnlyList<LevelData> Levels => levels;
        public int Count => levels != null ? levels.Count : 0;
        public int LoopStartLevel => Mathf.Max(1, loopStartLevel);

        public LevelData Get(int index)
        {
            if (levels == null || index < 0 || index >= levels.Count) return null;
            return levels[index];
        }

        /// <summary>Level for a 0-based progress index. Within the list → that level; PAST the end →
        /// loops back from <see cref="LoopStartLevel"/> and continues, repeating.</summary>
        public LevelData GetLooping(int levelIndex)
        {
            if (levels == null || levels.Count == 0) return null;
            if (levelIndex < 0) levelIndex = 0;
            if (levelIndex < levels.Count) return Get(levelIndex);

            int loopStart = Mathf.Clamp(LoopStartLevel - 1, 0, levels.Count - 1); // 0-based
            int loopLen = levels.Count - loopStart;                                // >= 1
            int looped = loopStart + ((levelIndex - loopStart) % loopLen);
            return Get(looped);
        }

        /// <summary>Random level from the list, ignoring nulls. Returns null only if the list is empty.</summary>
        public LevelData GetRandom()
        {
            if (levels == null || levels.Count == 0) return null;
            // Try a few times to avoid stuck-on-null entries; fall back to linear scan.
            for (int i = 0; i < 4; i++)
            {
                var pick = levels[Random.Range(0, levels.Count)];
                if (pick != null) return pick;
            }
            foreach (var l in levels) if (l != null) return l;
            return null;
        }
    }
}
