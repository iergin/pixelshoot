using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Scale tuning for bus stickmen: the size they spawn at, the size they run at (relative to
    /// the target box's world scale), and how fast they grow between the two. Create via
    /// Create ▸ PixelShoot ▸ Stickman Scale Config.
    /// </summary>
    [CreateAssetMenu(fileName = "StickmanScaleConfig", menuName = "PixelShoot/Stickman Scale Config")]
    public class StickmanScaleConfig : ScriptableObject
    {
        [Tooltip("Uniform world scale the stickman has the moment it spawns.")]
        public float spawnScale = 1f;
        [Tooltip("Running scale = the target box's world scale × this multiplier.")]
        public float runScaleBoxMultiplier = 1.74f;
        [Tooltip("Seconds to smoothly grow from the spawn scale up to the run scale.")]
        [Min(0.01f)] public float growDuration = 0.2f;
        [Tooltip("Ease for the grow tween.")]
        public Ease growEase = Ease.OutBack;
    }
}
