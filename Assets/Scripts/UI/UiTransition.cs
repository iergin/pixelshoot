using UnityEngine;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// Reusable show/hide animation for a single UI group (a "button parent" container).
    /// Slides in from a chosen screen edge and/or fades, then reverses on hide. Each
    /// menu group gets one of these; the <see cref="MainMenuController"/> just plays them
    /// together (with optional per-group delay for a staggered reveal). Designed to be
    /// dropped onto any future panel without code changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiTransition : MonoBehaviour
    {
        public enum Edge { None, Left, Right, Top, Bottom }

        [Tooltip("Element to animate. Defaults to this object's RectTransform.")]
        [SerializeField] private RectTransform target;

        [Header("Slide")]
        [Tooltip("Screen edge the group slides IN from (and back OUT to). None = no movement, fade only.")]
        [SerializeField] private Edge slideFrom = Edge.Left;
        [Tooltip("Off-screen travel distance in canvas units. 0 = auto (uses the root canvas size so it's fully off-screen).")]
        [SerializeField] private float slideDistance = 0f;

        [Header("Fade")]
        [Tooltip("Also fade the group via a CanvasGroup (added automatically if missing).")]
        [SerializeField] private bool useFade = true;

        [Header("Timing")]
        [SerializeField] private float inDuration = 0.45f;
        [SerializeField] private float outDuration = 0.3f;
        [Tooltip("Delay before this group starts animating in — use to stagger several groups.")]
        [SerializeField] private float inDelay = 0f;
        [SerializeField] private float outDelay = 0f;
        [SerializeField] private Ease inEase = Ease.OutBack;
        [SerializeField] private Ease outEase = Ease.InBack;

        private CanvasGroup cg;
        private Vector2 shownPos;
        private Vector2 hiddenPos;
        private bool captured;
        private Tween moveTween;
        private Tween fadeTween;

        /// <summary>Total time the in-animation occupies (delay + duration).</summary>
        public float InTime => inDelay + inDuration;
        /// <summary>Total time the out-animation occupies (delay + duration).</summary>
        public float OutTime => outDelay + outDuration;

        private void Awake() => Capture();

        private void Capture()
        {
            if (captured) return;
            if (target == null) target = transform as RectTransform;
            if (target == null) return;

            shownPos = target.anchoredPosition;
            hiddenPos = shownPos + OffsetFor(slideFrom);

            if (useFade)
            {
                cg = GetComponent<CanvasGroup>();
                if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            }
            captured = true;
        }

        private Vector2 OffsetFor(Edge edge)
        {
            float d = slideDistance > 0f ? slideDistance : AutoDistance(edge);
            switch (edge)
            {
                case Edge.Left:   return new Vector2(-d, 0f);
                case Edge.Right:  return new Vector2(+d, 0f);
                case Edge.Top:    return new Vector2(0f, +d);
                case Edge.Bottom: return new Vector2(0f, -d);
                default:          return Vector2.zero;
            }
        }

        private float AutoDistance(Edge edge)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.transform is RectTransform crt)
                return (edge == Edge.Left || edge == Edge.Right) ? crt.rect.width : crt.rect.height;
            return (edge == Edge.Left || edge == Edge.Right) ? Screen.width : Screen.height;
        }

        // ── Public API ──────────────────────────────────────────────────────
        /// <summary>Snap to the shown pose immediately (no animation) — for a popup used as a static
        /// embedded page.</summary>
        public void SetShown()
        {
            Capture();
            if (target == null) return;
            moveTween?.Kill(); fadeTween?.Kill();
            target.anchoredPosition = shownPos;
            if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
        }

        /// <summary>Snap to the hidden pose immediately (call before the first PlayIn).</summary>
        public void SetHidden()
        {
            Capture();
            if (target == null) return;
            moveTween?.Kill(); fadeTween?.Kill();
            target.anchoredPosition = hiddenPos;
            if (cg != null) { cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; }
        }

        /// <summary>Animate from hidden → shown. Returns the move tween (or fade if no slide).</summary>
        public Tween PlayIn()
        {
            Capture();
            if (target == null) return null;
            moveTween?.Kill(); fadeTween?.Kill();

            Tween result = null;
            if (slideFrom != Edge.None)
            {
                target.anchoredPosition = hiddenPos;
                moveTween = target.DOAnchorPos(shownPos, inDuration).SetEase(inEase).SetDelay(inDelay).SetUpdate(true);
                result = moveTween;
            }
            if (cg != null)
            {
                cg.interactable = true; cg.blocksRaycasts = true;
                fadeTween = cg.DOFade(1f, inDuration).SetEase(Ease.OutQuad).SetDelay(inDelay).SetUpdate(true);
                result ??= fadeTween;
            }
            return result;
        }

        /// <summary>Animate from shown → hidden. Returns the longest tween so callers can sequence.</summary>
        public Tween PlayOut()
        {
            Capture();
            if (target == null) return null;
            moveTween?.Kill(); fadeTween?.Kill();

            Tween result = null;
            if (slideFrom != Edge.None)
            {
                moveTween = target.DOAnchorPos(hiddenPos, outDuration).SetEase(outEase).SetDelay(outDelay).SetUpdate(true);
                result = moveTween;
            }
            if (cg != null)
            {
                cg.interactable = false; cg.blocksRaycasts = false;
                fadeTween = cg.DOFade(0f, outDuration).SetEase(Ease.InQuad).SetDelay(outDelay).SetUpdate(true);
                result ??= fadeTween;
            }
            return result;
        }

        private void OnDestroy()
        {
            moveTween?.Kill();
            fadeTween?.Kill();
        }
    }
}
