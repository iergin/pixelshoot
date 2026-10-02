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
    /// <para>A subtle sweep replays every <see cref="loopInterval"/> seconds during normal
    /// play; once the level is won it switches to the separate "success" values
    /// (faster / brighter) so the celebration reads differently.</para>
    /// </summary>
    public class GridSheenController : MonoBehaviour
    {
        private static readonly int SweepPosId       = Shader.PropertyToID("_SweepPos");
        private static readonly int SweepIntensityId = Shader.PropertyToID("_SweepIntensity");

        [Header("Sweep travel")]
        [Tooltip("How far past the edges the band starts/ends, so it fully enters and exits the screen.")]
        [SerializeField] private float edgeMargin = 0.25f;

        [Header("Loop (normal gameplay)")]
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

        [Header("Loop after level success (different feel)")]
        [Tooltip("Switch to the success values below when the level is won.")]
        [SerializeField] private bool overrideOnSuccess = true;
        [Tooltip("GameController whose OnLevelWon flips the sheen into 'success' mode.")]
        [SerializeField] private GameController gameController;
        [SerializeField] private float successInterval = 0.6f;
        [SerializeField] private float successDuration = 0.7f;
        [SerializeField, Min(0f)] private float successIntensity = 1.2f;

        private bool successMode;
        private Tween sweepTween;
        private Coroutine loopRoutine;

        // Active values follow the success flag once the level is won.
        private float CurInterval  => (successMode && overrideOnSuccess) ? successInterval  : loopInterval;
        private float CurDuration  => (successMode && overrideOnSuccess) ? successDuration  : loopDuration;
        private float CurIntensity => (successMode && overrideOnSuccess) ? successIntensity : loopIntensity;

        private void Awake()
        {
            // Start invisible.
            Shader.SetGlobalFloat(SweepIntensityId, 0f);
            Shader.SetGlobalFloat(SweepPosId, -1f);
        }

        private void OnEnable()
        {
            if (gameController != null) gameController.OnLevelWon += OnLevelWon;
            if (loopEnabled) loopRoutine = StartCoroutine(LoopRoutine(immediate: false));
        }

        private void OnDisable()
        {
            if (gameController != null) gameController.OnLevelWon -= OnLevelWon;
            if (loopRoutine != null) { StopCoroutine(loopRoutine); loopRoutine = null; }
            sweepTween?.Kill();
        }

        private void OnLevelWon()
        {
            successMode = true;
            if (!loopEnabled) return;
            // Restart the loop so the success values kick in right away (no idle wait).
            if (loopRoutine != null) StopCoroutine(loopRoutine);
            loopRoutine = StartCoroutine(LoopRoutine(immediate: true));
        }

        private IEnumerator LoopRoutine(bool immediate)
        {
            if (!immediate) yield return new WaitForSeconds(loopStartDelay);
            while (true)
            {
                Sweep(CurDuration, CurIntensity);
                yield return new WaitForSeconds(CurDuration);
                yield return new WaitForSeconds(CurInterval);
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
