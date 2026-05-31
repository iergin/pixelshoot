using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Ordered playlist of LevelData assets. The player walks through them in order
    /// based on their saved progress (see PixelShoot.Game.PlayerProgress); once they
    /// run past the last entry, LevelLoader serves a random pick from this list.
    /// </summary>
    [CreateAssetMenu(fileName = "AllLevels", menuName = "PixelShoot/All Levels")]
    public class AllLevelsData : ScriptableObject
    {
        [Tooltip("Levels in play order. Index 0 = first level the player sees.")]
        [SerializeField] private List<LevelData> levels = new List<LevelData>();

        public IReadOnlyList<LevelData> Levels => levels;
        public int Count => levels != null ? levels.Count : 0;

        public LevelData Get(int index)
        {
            if (levels == null || index < 0 || index >= levels.Count) return null;
            return levels[index];
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
