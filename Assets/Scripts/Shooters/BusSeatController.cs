using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Spawns bus passengers (Stickmen) on demand from a SINGLE spawn point. There are no
    /// persistent seats or a queue anymore: every shot pulls one pooled stickman, drops it
    /// at the spawn point, and hands it to the Shooter to fly off immediately.
    ///
    /// <para>Stickmen are pooled (see <see cref="StickmanPool"/>) — reused rather than
    /// Instantiated/Destroyed each shot.</para>
    /// </summary>
    public class BusSeatController : MonoBehaviour
    {
        [Tooltip("Where each stickman appears before it runs. If null, uses this transform.")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Stickman stickmanPrefab;
        [Tooltip("Uniform scale applied to each spawned stickman.")]
        [SerializeField] private float stickmanScale = 1.27f;

        private ColorData color;

        /// <summary>Bind the bus colour. Shot count is tracked by the Shooter, not here.</summary>
        public void Initialize(int totalShots, ColorData c)
        {
            color = c;
        }

        /// <summary>
        /// Pull one pooled stickman, place it at the spawn point coloured + facing forward,
        /// and return it so the Shooter can launch it at its target. Returns null if no
        /// prefab is assigned (the Shooter then falls back to an instant hit).
        /// </summary>
        public Stickman SpawnRunner()
        {
            if (stickmanPrefab == null) return null;

            var s = StickmanPool.Get(stickmanPrefab, transform);
            Transform anchor = spawnPoint != null ? spawnPoint : transform;
            s.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            s.transform.localScale = Vector3.one * stickmanScale;
            s.SetColor(color);
            s.PlayIdle();
            return s;
        }

        /// <summary>
        /// Bomb payback previously despawned seated stickmen; with on-demand spawning there
        /// are none waiting on the bus, so this is a no-op (the Shooter already deducts the
        /// shot count). Kept for interface compatibility.
        /// </summary>
        public void ConsumeFromBack(int amount) { }
    }
}
