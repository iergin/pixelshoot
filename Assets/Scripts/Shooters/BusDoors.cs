using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Two bus doors (left / right) that swing open when a stickman spawns and swing shut
    /// again after a hold. If another stickman spawns while they're closing, they reopen
    /// from wherever they currently are (no snap) — call <see cref="Open"/> on every spawn.
    /// </summary>
    public class BusDoors : MonoBehaviour
    {
        [Header("Doors")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [Tooltip("Local axis each door hinges around.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;
        [Tooltip("Open angle (degrees). Left door rotates +angle, right door -angle.")]
        [SerializeField] private float openAngle = 90f;

        [Header("Timing")]
        [SerializeField] private float openDuration = 0.18f;
        [SerializeField] private float closeDuration = 0.3f;
        [Tooltip("Seconds the doors stay fully open before they start closing.")]
        [SerializeField] private float openHold = 0.5f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InOutSine;

        private Quaternion leftClosed, rightClosed, leftOpen, rightOpen;
        private Tween doorTween;
        private Tween closeCall;
        private bool captured;

        private void Awake() => Capture();

        private void Capture()
        {
            if (captured) return;
            if (leftDoor != null)
            {
                leftClosed = leftDoor.localRotation;
                leftOpen = leftClosed * Quaternion.AngleAxis(openAngle, hingeAxis);
            }
            if (rightDoor != null)
            {
                rightClosed = rightDoor.localRotation;
                rightOpen = rightClosed * Quaternion.AngleAxis(-openAngle, hingeAxis);
            }
            captured = true;
        }

        /// <summary>Open the doors (from their current angle) and schedule an auto-close.</summary>
        public void Open()
        {
            Capture();
            KillTweens();

            var seq = DOTween.Sequence().SetUpdate(false);
            if (leftDoor != null) seq.Join(leftDoor.DOLocalRotateQuaternion(leftOpen, openDuration).SetEase(openEase));
            if (rightDoor != null) seq.Join(rightDoor.DOLocalRotateQuaternion(rightOpen, openDuration).SetEase(openEase));
            doorTween = seq;

            // Auto-close after the doors are open + the hold. Rescheduled on every Open().
            closeCall = DOVirtual.DelayedCall(openDuration + openHold, Close);
        }

        /// <summary>Close the doors from their current angle.</summary>
        public void Close()
        {
            Capture();
            doorTween?.Kill();

            var seq = DOTween.Sequence().SetUpdate(false);
            if (leftDoor != null) seq.Join(leftDoor.DOLocalRotateQuaternion(leftClosed, closeDuration).SetEase(closeEase));
            if (rightDoor != null) seq.Join(rightDoor.DOLocalRotateQuaternion(rightClosed, closeDuration).SetEase(closeEase));
            doorTween = seq;
        }

        private void KillTweens()
        {
            doorTween?.Kill();
            closeCall?.Kill();
        }

        private void OnDestroy() => KillTweens();
    }
}
