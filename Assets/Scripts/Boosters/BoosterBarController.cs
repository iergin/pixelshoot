using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Slides the BoosterBar down off-screen while an interactive booster (Claw / FillColor)
    /// is running, and brings it back when the process ends. Put this on the BoosterBar's
    /// RectTransform. Driven by <see cref="BoosterProcess"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoosterBarController : MonoBehaviour
    {
        [Tooltip("How far (anchored units) the bar drops to hide below the screen.")]
        [SerializeField] private float slideDistance = 400f;
        [SerializeField] private float duration = 0.35f;
        [SerializeField] private Ease hideEase = Ease.InBack;
        [SerializeField] private Ease showEase = Ease.OutBack;

        private RectTransform rt;
        private Vector2 shownPos;
        private Tween tween;

        private void Awake()
        {
            rt = (RectTransform)transform;
            shownPos = rt.anchoredPosition;
        }

        private void OnEnable()
        {
            BoosterProcess.Changed += OnProcessChanged;
            // Sync in case a process was already running when we enabled.
            rt.anchoredPosition = BoosterProcess.Active ? HiddenPos : shownPos;
        }

        private void OnDisable() => BoosterProcess.Changed -= OnProcessChanged;

        private Vector2 HiddenPos => shownPos - new Vector2(0f, slideDistance);

        private void OnProcessChanged(bool active)
        {
            tween?.Kill();
            tween = rt.DOAnchorPos(active ? HiddenPos : shownPos, duration)
                .SetEase(active ? hideEase : showEase)
                .SetUpdate(true);
        }
    }
}
