using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using LineworkLite.FreeOutline;
using PixelShoot.Conveyor;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    /// <summary>
    /// "You can shoot here" idle hint. After <see cref="idleSeconds"/> of no input (and no bus on the
    /// conveyor, no popup/booster), it lights the Free Outline on every shootable box and SMOOTHLY
    /// fades the hint outline's alpha from 0 up to <see cref="hintAlpha"/>. Any tap fades it back to 0
    /// and clears. Alpha is driven on the hint outline entry (index <see cref="hintOutlineIndex"/>) of
    /// the Free Outline Settings; the authored colour is restored on disable.
    /// </summary>
    public class IdleHintController : MonoBehaviour
    {
        [SerializeField] private GridController grid;
        [Tooltip("If a bus is riding / boarding the conveyor, the hint stays hidden.")]
        [SerializeField] private ConveyorController conveyor;
        [Tooltip("Seconds of no input before the hint appears.")]
        [SerializeField, Min(0f)] private float idleSeconds = 2f;

        [Header("Outline fade")]
        [Tooltip("The Free Outline Settings asset that holds the hint outline entry.")]
        [SerializeField] private FreeOutlineSettings outlineSettings;
        [Tooltip("Index of the HINT outline in the Free Outline Settings list (0 = booster, 1 = hint).")]
        [SerializeField] private int hintOutlineIndex = 1;
        [Tooltip("Alpha the hint outline fades IN to (0 = invisible, 1 = solid).")]
        [SerializeField, Range(0f, 1f)] private float hintAlpha = 0.3f;
        [Tooltip("Seconds for the alpha fade in / out.")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        private float idle;
        private bool shown;
        private Tween alphaTween;
        private Color baseColor, baseOccludedColor;
        private bool colorsCached;

        private void Awake() => CacheAndZeroAlpha();

        private void OnDisable()
        {
            alphaTween?.Kill();
            shown = false;
            SetAlpha(0f);
            if (grid != null) grid.SetShootableHint(false);
            RestoreColors(); // leave the asset at its authored colour
        }

        private void Update()
        {
            // Any press, a modal/booster suspend, or a bus on the conveyor → reset + fade out.
            if (AnyPress() || ClickInputRouter.Suspended || ConveyorHasBus())
            {
                idle = 0f;
                if (shown) HideHint();
                return;
            }

            idle += Time.unscaledDeltaTime;
            if (!shown && idle >= idleSeconds) ShowHint();
        }

        private void ShowHint()
        {
            shown = true;
            if (grid != null) grid.SetShootableHint(true); // layer on + hide Hit outlines
            FadeTo(hintAlpha);
        }

        private void HideHint()
        {
            shown = false;
            FadeTo(0f, () => { if (grid != null) grid.SetShootableHint(false); }); // fade out, then layer off
        }

        // ── Hint outline alpha ───────────────────────────────────────────────
        private Outline HintOutline()
        {
            if (outlineSettings == null) return null;
            var list = outlineSettings.Outlines;
            if (list == null || hintOutlineIndex < 0 || hintOutlineIndex >= list.Count) return null;
            return list[hintOutlineIndex];
        }

        private void CacheAndZeroAlpha()
        {
            var o = HintOutline();
            if (o == null) return;
            baseColor = o.color;
            baseOccludedColor = o.occludedColor;
            colorsCached = true;
            SetAlpha(0f); // start hidden
        }

        private void RestoreColors()
        {
            if (!colorsCached) return;
            var o = HintOutline();
            if (o == null) return;
            o.color = baseColor;
            o.occludedColor = baseOccludedColor;
        }

        private void FadeTo(float target, System.Action onComplete = null)
        {
            var o = HintOutline();
            if (o == null) { onComplete?.Invoke(); return; }
            alphaTween?.Kill();
            float from = o.color.a;
            alphaTween = DOTween.To(() => from, SetAlpha, target, fadeDuration)
                .SetEase(fadeEase)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }

        private void SetAlpha(float a)
        {
            var o = HintOutline();
            if (o == null) return;
            var c = o.color; c.a = a; o.color = c;
            var oc = o.occludedColor; oc.a = a; o.occludedColor = oc;
        }

        private bool ConveyorHasBus() => conveyor != null && conveyor.OccupiedCount > 0;

        private static bool AnyPress()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) return true;
            return false;
        }
    }
}
