using UnityEngine;
using DG.Tweening;
using PixelShoot.Shooters;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Tuning for the FillColor booster's run-in stickmen: which prefab, how fast they run,
    /// which animation state to play, how far off-screen they spawn, and pacing. Create via
    /// Create ▸ PixelShoot ▸ FillColor Config.
    /// </summary>
    [CreateAssetMenu(fileName = "FillColorConfig", menuName = "PixelShoot/FillColor Config")]
    public class FillColorConfig : ScriptableObject
    {
        [Header("Stickman")]
        [Tooltip("Stickman prefab pooled for the run-in (usually the same as the bus passenger).")]
        public Stickman stickmanPrefab;
        [Tooltip("Uniform scale applied to each spawned stickman.")]
        [Min(0.01f)] public float stickmanScale = 1.27f;
        [Tooltip("Animator TRIGGER set to start the run animation (SetTrigger), e.g. \"Falling\".")]
        public string runAnimState = "Falling";
        [Tooltip("Rotate the stickman to face its run direction.")]
        public bool faceMovement = true;

        [Header("Movement")]
        [Tooltip("Ground run speed in world units/second. Duration = distance / speed.")]
        [Min(0.01f)] public float runSpeed = 10f;
        [Tooltip("Floor on the run duration so a very near box still shows a brief run.")]
        [Min(0f)] public float minRunDuration = 0.1f;
        [Tooltip("Ease applied to the run tween.")]
        public Ease moveEase = Ease.Linear;

        [Header("Spawn")]
        [Tooltip("Seconds between successive stickman spawns (staggered arrival).")]
        [Min(0f)] public float spawnStagger = 0.03f;
        [Tooltip("How far outside the screen (in screen pixels) stickmen spawn before running in.")]
        [Min(0f)] public float edgeMarginPixels = 120f;
    }
}
