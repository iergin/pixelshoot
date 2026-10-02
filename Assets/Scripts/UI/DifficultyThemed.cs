using System;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Generic difficulty skinning for ANY menu / popup. On enable it reads the current level's
    /// difficulty (<see cref="DifficultyProvider.Current"/>) and:
    /// <list type="bullet">
    /// <item>swaps each <see cref="spriteTargets"/> Image to its per-difficulty sprite (e.g. the menu
    /// Play button, a popup's two images) — each target carries its own 3 sprites;</item>
    /// <item>tints each <see cref="colorTargets"/> Graphic with the difficulty's config colour (green /
    /// red / purple), e.g. the popup background.</item>
    /// </list>
    /// Drop one on any menu/popup root and fill in whichever targets it has. Refreshes every open, so
    /// it always shows the upcoming level's difficulty.
    /// </summary>
    public class DifficultyThemed : MonoBehaviour
    {
        [Serializable]
        public class SpriteTarget
        {
            public Image image;
            public Sprite normal;
            public Sprite hard;
            public Sprite superHard;
        }

        [Tooltip("Images whose sprite changes per difficulty (play button, popup images, …).")]
        [SerializeField] private SpriteTarget[] spriteTargets;
        [Tooltip("Graphics tinted with the difficulty's config colour (popup background, banner, …).")]
        [SerializeField] private Graphic[] colorTargets;

        private void OnEnable() => Apply(DifficultyProvider.Current);

        /// <summary>Apply a specific difficulty (also called automatically on enable for the current one).</summary>
        public void Apply(LevelDifficulty d)
        {
            if (spriteTargets != null)
                foreach (var t in spriteTargets)
                {
                    if (t == null || t.image == null) continue;
                    Sprite s = SpriteFor(t, d);
                    if (s != null) t.image.sprite = s;
                }

            if (colorTargets != null && colorTargets.Length > 0)
            {
                Color c = DifficultyProvider.ColorFor(d);
                foreach (var g in colorTargets) if (g != null) g.color = c;
            }
        }

        private static Sprite SpriteFor(SpriteTarget t, LevelDifficulty d)
        {
            switch (d)
            {
                case LevelDifficulty.Hard:      return t.hard;
                case LevelDifficulty.SuperHard: return t.superHard;
                default:                        return t.normal;
            }
        }
    }
}
