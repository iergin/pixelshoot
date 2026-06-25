using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop on any UI button (or any object with a Graphic that receives pointer events):
    /// it shrinks a little while pressed and pops back to its original scale on release.
    /// Uses EventSystem pointer callbacks, so it works with both the old and new input
    /// backends and doesn't care whether there's a Button component.
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Transform to scale. Defaults to this object.")]
        [SerializeField] private Transform target;
        [Tooltip("Scale multiplier while held down (relative to the original scale).")]
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;
        [Tooltip("Seconds to shrink down on press.")]
        [SerializeField] private float downDuration = 0.08f;
        [Tooltip("Seconds to pop back up on release.")]
        [SerializeField] private float upDuration = 0.18f;
        [Tooltip("Ease for the pop-back-up (OutBack adds a little overshoot bounce).")]
        [SerializeField] private Ease upEase = Ease.OutBack;
        [Tooltip("If false, ignores presses (e.g. while the button is disabled).")]
        [SerializeField] private bool interactable = true;

        private Vector3 baseScale;
        private bool captured;
        private Tween tween;

        public bool Interactable { get => interactable; set => interactable = value; }

        private void Awake()
        {
            if (target == null) target = transform;
            baseScale = target.localScale;
            captured = true;
        }

        private void OnEnable()
        {
            // Restore in case it was disabled mid-press.
            if (captured && target != null) target.localScale = baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable) return;
            ScaleTo(baseScale * pressedScale, downDuration, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!interactable) return;
            ScaleTo(baseScale, upDuration, upEase);
        }

        private void ScaleTo(Vector3 s, float dur, Ease ease)
        {
            if (target == null) return;
            tween?.Kill();
            tween = target.DOScale(s, dur).SetEase(ease).SetUpdate(true); // unscaled time so it works while paused
        }

        private void OnDisable()
        {
            tween?.Kill();
            if (captured && target != null) target.localScale = baseScale;
        }
    }
}
