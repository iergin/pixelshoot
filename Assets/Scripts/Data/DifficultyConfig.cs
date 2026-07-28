using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>Per-level difficulty. Drives the button/popup colour and the win-reward multiplier.</summary>
    public enum LevelDifficulty { Normal, Hard, SuperHard }

    /// <summary>
    /// Shared difficulty tuning: the colour and win-reward multiplier for each difficulty. One asset,
    /// referenced by DifficultyProvider (via AppBootstrap). Sprites are per-UI-element (see
    /// <c>DifficultyThemed</c>); only the colour + reward live here so they stay consistent everywhere.
    /// Create via <b>Assets ▸ Create ▸ PixelShoot ▸ Difficulty Config</b>.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "PixelShoot/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        [System.Serializable]
        public class Style
        {
            public Color color = Color.white;
            [Tooltip("Win reward is multiplied by this (Normal 1, Hard 3, Super Hard 5).")]
            [Min(1)] public int rewardMultiplier = 1;
        }

        [SerializeField] private Style normal    = new Style { color = new Color(0.30f, 0.78f, 0.32f), rewardMultiplier = 1 }; // green
        [SerializeField] private Style hard      = new Style { color = new Color(0.90f, 0.25f, 0.25f), rewardMultiplier = 3 }; // red
        [SerializeField] private Style superHard = new Style { color = new Color(0.62f, 0.24f, 0.90f), rewardMultiplier = 5 }; // purple

        [Header("Level table")]
        [Tooltip("JSON mapping level id → difficulty. Format: { \"levels\": [ { \"id\": 4, \"difficulty\": " +
                 "\"Hard\" }, { \"id\": 8, \"difficulty\": \"SuperHard\" } ] }. Levels not listed = Normal. " +
                 "id is the 1-based level number (DisplayLevel).")]
        [SerializeField] private TextAsset levelTableJson;

        public TextAsset LevelTableJson => levelTableJson;

        public Style For(LevelDifficulty d)
        {
            switch (d)
            {
                case LevelDifficulty.Hard:      return hard;
                case LevelDifficulty.SuperHard: return superHard;
                default:                        return normal;
            }
        }
    }
}
