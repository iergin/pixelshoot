using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Game
{
    /// <summary>
    /// Drives the screen-space shine sweep on the boxes (BoxSheen shader) by animating
    /// two GLOBAL shader floats, so every box stays in sync and the whole grid reads as
    /// one continuous light wave travelling bottom-left → top-right.
    ///
    /// <para>LOOP ONLY: a subtle sweep replays every <see cref="loopInterval"/> seconds.
    /// There is no event-triggered (hit/win) sweep — it just keeps shimmering.</para>
    /// </summary>
    public class GridSheenController : MonoBehaviour
    {
        private static readonly int SweepPosId       = Shader.PropertyToID("_SweepPos");
        private static readonly int SweepIntensityId = Shader.PropertyToID("_SweepIntensity");

        [Header("Sweep travel")]
        [Tooltip("How far past the edges the band starts/ends, so it fully enters and exits the screen.")]
        [SerializeField] private float edgeMargin = 0.25f;

        [Header("Loop")]
        [Tooltip("Enable the periodic shimmer.")]
        [SerializeField] private bool loopEnabled = true;
        [Tooltip("Seconds to wait before the very first loop sweep.")]
        [SerializeField] private float loopStartDelay = 1.5f;
        [Tooltip("Idle seconds between loop sweeps (added after each sweep finishes).")]
        [SerializeField] private float loopInterval = 3f;
        [Tooltip("Seconds one loop sweep takes to cross the screen.")]
        [SerializeField] private float loopDuration = 1.1f;
        [Tooltip("Brightness of the loop sweep (keep low for a soft shimmer).")]
        [SerializeField, Min(0f)] private float loopIntensity = 0.35f;

        private Tween sweepTween;
        private Coroutine loopRoutine;

        private void Awake()
        {
            // Start invisible.
            Shader.SetGlobalFloat(SweepIntensityId, 0f);
            Shader.SetGlobalFloat(SweepPosId, -1f);
        }

        private void OnEnable()
        {
            if (loopEnabled) loopRoutine = StartCoroutine(LoopRoutine());
        }

        private void OnDisable()
        {
            if (loopRoutine != null) { StopCoroutine(loopRoutine); loopRoutine = null; }
            sweepTween?.Kill();
        }

        private IEnumerator LoopRoutine()
        {
            yield return new WaitForSeconds(loopStartDelay);
            var wait = new WaitForSeconds(loopInterval);
            while (true)
            {
                Sweep(loopDuration, loopIntensity);
                yield return new WaitForSeconds(loopDuration);
                yield return wait;
            }
        }

        /// <summary>Animate one band pass across the diagonal at the given brightness.</summary>
        public void Sweep(float duration, float intensity)
        {
            sweepTween?.Kill();
            Shader.SetGlobalFloat(SweepIntensityId, intensity);

            float start = -edgeMargin;
            float end = 1f + edgeMargin;
            sweepTween = DOTween.To(() => start, v => Shader.SetGlobalFloat(SweepPosId, v), end, duration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true) // unscaled — keeps shimmering even if the game is paused
                .OnComplete(() => Shader.SetGlobalFloat(SweepIntensityId, 0f));
        }
    }
}
