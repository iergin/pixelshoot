using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Computes per-cell <see cref="Tone"/> assignments grouped by color.
    /// Guarantees exact counts (no rounding overshoot) by rounding two categories
    /// and using the third as the residual:
    /// <code>
    /// normal = round(N * normalRatio)
    /// dark   = round(N * darkRatio)
    /// light  = N - normal - dark
    /// </code>
    /// Then shuffles the cells of that color and assigns the first `normal`
    /// to Normal, next `dark` to Dark, the rest to Light.
    /// </summary>
    public static class ToneDistributor
    {
        public const float DefaultNormalRatio = 0.84f;
        public const float DefaultDarkRatio   = 0.08f;
        // Light = remainder, so it's implicitly 1 - normal - dark.

        public struct CellRef
        {
            public int Index; // flat index into the cells[] array (z*size + x)
        }

        /// <summary>
        /// Walks <paramref name="cellsByColor"/> (keyed by palette index ≥ 0) and writes
        /// the resulting Tone for each cell index into <paramref name="tonesOut"/>.
        /// </summary>
        public static void Distribute(
            Dictionary<int, List<int>> cellsByColor,
            Tone[] tonesOut,
            float normalRatio = DefaultNormalRatio,
            float darkRatio   = DefaultDarkRatio,
            int? seed = null)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            foreach (var kvp in cellsByColor)
            {
                var list = kvp.Value;
                int n = list.Count;
                if (n == 0) continue;

                int normal = Mathf.RoundToInt(n * normalRatio);
                int dark   = Mathf.RoundToInt(n * darkRatio);
                normal = Mathf.Clamp(normal, 0, n);
                dark   = Mathf.Clamp(dark, 0, n - normal);
                int light = n - normal - dark;

                // Fisher-Yates shuffle of indices, then slice.
                var shuffled = new List<int>(list);
                for (int i = shuffled.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(0, i + 1);
                    (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                }

                int cursor = 0;
                for (int i = 0; i < normal; i++) tonesOut[shuffled[cursor++]] = Tone.Normal;
                for (int i = 0; i < dark;   i++) tonesOut[shuffled[cursor++]] = Tone.Dark;
                for (int i = 0; i < light;  i++) tonesOut[shuffled[cursor++]] = Tone.Light;
            }
        }
    }
}
