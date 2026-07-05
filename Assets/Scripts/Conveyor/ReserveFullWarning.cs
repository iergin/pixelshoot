using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Conveyor
{
    /// <summary>
    /// Warns the player when the reserve fills up: the moment the last reserve slot is
    /// taken, the assigned SpriteRenderers smoothly pulse toward red twice, then settle
    /// back to their normal colour. Edge-triggered — it fires ONCE per full transition,
    /// not continuously while full.
    /// </summary>
    public class ReserveFullWarning : MonoBehaviour
    {
        [SerializeField] private ReserveController reserve;
        [Tooltip("Slot sprites tinted during the warning (typically one per reserve slot).")]
        [SerializeField] private SpriteRenderer[] sprites;

        [Header("Pulse")]
        [SerializeField] private Color warningColor = Color.red;
        [Tooltip("Seconds for one base→red half-pulse. Full warning = redPulses × 2 × this.")]
        [SerializeField] private float pulseHalfDuration = 0.25f;
        [Tooltip("How many times the sprites flash red before settling.")]
        [SerializeField, Min(1)] private int redPulses = 2;
        [SerializeField] private Ease ease = Ease.InOutSine;

        private Color[] baseColors;
        private Tween[] tweens;
        private bool wasFull;

        private void Awake()
        {
            if (sprites == null) return;
            baseColors = new Color[sprites.Length];
            tweens = new Tween[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) baseColors[i] = sprites[i].color;
        }

        private void Update()
        {
            if (reserve == null) return;
            bool full = reserve.Capacity > 0 && reserve.IsFull;
            if (full && !wasFull) Pulse();
            wasFull = full;
        }

        private void Pulse()
        {
            if (sprites == null) return;
            for (int i = 0; i < sprites.Length; i++)
            {
                var s = sprites[i];
                if (s == null) continue;
                tweens[i]?.Kill();
                s.color = baseColors[i];
                // Yoyo for redPulses*2 loops: base→red, red→base, … ends back at base.
                tweens[i] = s.DOColor(warningColor, pulseHalfDuration)
                    .SetEase(ease)
                    .SetLoops(redPulses * 2, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        private void OnDisable()
        {
            if (tweens == null) return;
            for (int i = 0; i < tweens.Length; i++)
            {
                tweens[i]?.Kill();
                if (sprites != null && i < sprites.Length && sprites[i] != null && baseColors != null)
                    sprites[i].color = baseColors[i];
            }
        }
    }
}
