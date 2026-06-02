using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Per-cell tonal variant. Assigned at level-authoring time (NOT per-frame random)
    /// so the same cell carries the same tone whether it's currently shown as
    /// gray (Locked / Frontier) or vivid (Hit).
    ///
    /// <para>The actual color offset is computed by <see cref="ToneShifter"/>:
    /// Dark = base − 20/255, Light = base + 20/255 per channel, Normal = base.</para>
    /// </summary>
    public enum Tone : byte
    {
        Normal = 0,
        Dark   = 1,
        Light  = 2,
    }

    public static class ToneShifter
    {
        /// <summary>Magnitude of the per-channel shift, in 0–1 color space (≈ 20/255).</summary>
        public const float Shift01 = 20f / 255f;

        public static Color Apply(Color baseColor, Tone tone)
        {
            switch (tone)
            {
                case Tone.Dark:
                    return new Color(
                        Mathf.Clamp01(baseColor.r - Shift01),
                        Mathf.Clamp01(baseColor.g - Shift01),
                        Mathf.Clamp01(baseColor.b - Shift01),
                        baseColor.a);
                case Tone.Light:
                    return new Color(
                        Mathf.Clamp01(baseColor.r + Shift01),
                        Mathf.Clamp01(baseColor.g + Shift01),
                        Mathf.Clamp01(baseColor.b + Shift01),
                        baseColor.a);
                default:
                    return baseColor;
            }
        }
    }
}
