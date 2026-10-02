using UnityEngine;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// Button-driven pager for the main menu's Home / Shop / Leaderboard pages. NO swipe — pressing a
    /// <see cref="NavigationBar"/> tab slides to that page.
    ///
    /// <para><b>Resolution / aspect independent by design.</b> Pages are NOT laid out side-by-side at
    /// hard pixel offsets (that breaks on iPad / different aspect ratios — gaps show through). Instead
    /// every page is <b>stretch-anchored to fill the whole viewport</b>, so it is always exactly
    /// full-screen on any device via anchors, not pixels. Only ONE page is active at a time; switching
    /// slides the incoming page in and the outgoing page out by the viewport's <i>current</i> width, so
    /// even the transient animation is correct for the running resolution/orientation. At rest a page
    /// sits at x = 0 = perfectly full-screen. Put a background Image on each page anchored stretch-fill
    /// and it will always cover the screen.</para>
    /// </summary>
    public class MenuPager : MonoBehaviour
    {
        [Tooltip("Parent of the pages. Each child is one page and is stretched to fill this. Defaults to this RectTransform.")]
        [SerializeField] private RectTransform content;
        [Tooltip("The visible area; its width is the slide distance. Defaults to this GameObject's RectTransform.")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private NavigationBar navBar;
        [Tooltip("0 = use content child count.")]
        [SerializeField] private int pageCountOverride = 0;
        [Tooltip("Page shown on start (e.g. 1 = Home in the middle of Shop | Home | Leaderboard).")]
        [SerializeField] private int startPage = 1;
        [SerializeField] private float transitionDuration = 0.35f;
        [SerializeField] private Ease ease = Ease.OutCubic;
        [Tooltip("On start, stretch-anchor every page to fill the viewport so backgrounds are always " +
                 "full-screen on any aspect ratio. Leave on unless you anchor the pages yourself.")]
        [SerializeField] private bool autoLayoutPages = true;

        private int current = -1;
        private Tween tween;

        private int Pages => pageCountOverride > 0
            ? pageCountOverride
            : (content != null ? content.childCount : 0);

        private float SlideWidth
        {
            get
            {
                float w = viewport != null ? viewport.rect.width : 0f;
                return w > 0f ? w : Screen.width; // fallback before the first layout pass
            }
        }

        private RectTransform Page(int i) =>
            (content != null && i >= 0 && i < content.childCount) ? content.GetChild(i) as RectTransform : null;

        private void Awake()
        {
            if (viewport == null) viewport = transform as RectTransform;
            if (content == null) content = transform as RectTransform;
        }

        private void OnEnable()  { if (navBar != null) navBar.OnTabSelected += GoTo; }
        private void OnDisable() { if (navBar != null) navBar.OnTabSelected -= GoTo; }

        private void Start()
        {
            if (autoLayoutPages) LayoutPages();
            GoToInstant(Mathf.Clamp(startPage, 0, Mathf.Max(0, Pages - 1)));
        }

        /// <summary>Slide to a page (nav tab tap → NavigationBar.OnTabSelected).</summary>
        public void GoTo(int index) => MoveTo(index, instant: false);

        /// <summary>Jump to a page with no animation.</summary>
        public void GoToInstant(int index) => MoveTo(index, instant: true);

        // Stretch every page to fill the viewport (full-screen on any aspect ratio).
        private void LayoutPages()
        {
            if (content == null) return;
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i) is not RectTransform rt) continue;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;      // fills parent
                rt.anchoredPosition = Vector2.zero;
            }
        }

        private void MoveTo(int index, bool instant)
        {
            int n = Pages;
            if (content == null || n <= 0) return;
            index = Mathf.Clamp(index, 0, n - 1);
            tween?.Kill();

            // Instant (or first) → activate only the target at x = 0, hide the rest.
            if (instant || current < 0)
            {
                for (int i = 0; i < n; i++)
                {
                    var rt = Page(i);
                    if (rt == null) continue;
                    SetX(rt, 0f);
                    rt.gameObject.SetActive(i == index);
                }
                current = index;
                if (navBar != null) navBar.SetHighlight(index);
                return;
            }

            if (index == current) return;

            float w = SlideWidth;
            int dir = index > current ? 1 : -1; // forward → incoming from the right
            var incoming = Page(index);
            var outgoing = Page(current);
            int outIdx = current;

            if (incoming != null) { incoming.gameObject.SetActive(true); SetX(incoming, dir * w); }

            var seq = DOTween.Sequence();
            if (incoming != null) seq.Join(incoming.DOAnchorPosX(0f, transitionDuration).SetEase(ease));
            if (outgoing != null) seq.Join(outgoing.DOAnchorPosX(-dir * w, transitionDuration).SetEase(ease));
            seq.OnComplete(() =>
            {
                var o = Page(outIdx);
                if (o != null && outIdx != index) o.gameObject.SetActive(false);
            });
            tween = seq;

            current = index;
            if (navBar != null) navBar.SetHighlight(index);
        }

        // Shift a stretch-anchored (full-width) page horizontally without changing its size.
        private static void SetX(RectTransform rt, float x)
        {
            var p = rt.anchoredPosition;
            p.x = x;
            rt.anchoredPosition = p;
        }

        private void OnDestroy() => tween?.Kill();
    }
}
