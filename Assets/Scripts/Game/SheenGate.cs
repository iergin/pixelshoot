using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Put this on a box's sheen overlay child. When the box becomes Hit, the parent
    /// enables this object — but if a shine sweep happens to be crossing the screen at
    /// that instant, the freshly-enabled overlay would "pop" the band into view, which
    /// reads as a shine triggered BY the hit.
    ///
    /// <para>To avoid that, the renderer is held hidden until the next gap between
    /// sweeps (global <c>_SweepIntensity</c> ≈ 0), then shown. The box therefore only
    /// ever joins a sweep cleanly from its start — never mid-pass — so a hit never
    /// causes an instant shine. The continuous loop shimmer is unaffected.</para>
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class SheenGate : MonoBehaviour
    {
        private static readonly int SweepIntensityId = Shader.PropertyToID("_SweepIntensity");
        [Tooltip("Treat the sweep as 'off' when its intensity is at or below this.")]
        [SerializeField] private float offThreshold = 0.001f;

        private Renderer rend;

        private void Awake() => rend = GetComponent<Renderer>();

        private void OnEnable()
        {
            // Start hidden; reveal only once we're between sweeps.
            if (rend != null) rend.enabled = false;
        }

        private void Update()
        {
            if (rend == null || rend.enabled) return;
            if (Shader.GetGlobalFloat(SweepIntensityId) <= offThreshold)
                rend.enabled = true;
        }
    }
}
