using System;
using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Game
{
    /// <summary>
    /// The floating key shown over the centre of its key-group cells. While uncollected it
    /// gently pulses (scale up/down). When its lock opens, the lock calls <see cref="JumpToLock"/>
    /// to make the key DOJump to the lock's target (position + rotation); on landing it fires the
    /// supplied callback (the lock then plays its Open animation) and the key destroys itself.
    /// Purely cosmetic — the gating logic lives in <see cref="KeyManager"/>.
    /// </summary>
    public class KeyVisual : MonoBehaviour
    {
        [Header("Idle")]
        [Tooltip("Peak scale of the gentle idle pulse (relative to the spawn scale). 1 = no pulse.")]
        [SerializeField] private float idlePulseScale = 1.15f;
        [Tooltip("Seconds for one half of the pulse (grow, then shrink back).")]
        [SerializeField] private float idlePulseHalfDuration = 0.7f;

        [Header("Jump to lock")]
        [SerializeField] private float jumpPower = 2f;
        [SerializeField] private int jumpCount = 1;
        [SerializeField] private float jumpDuration = 0.5f;

        private int keyId;
        private Vector3 baseScale;
        private bool leaving;
        private Tween idleTween;

        public void Init(int id)
        {
            keyId = id;
            baseScale = transform.localScale;
            if (KeyManager.Instance != null) KeyManager.Instance.RegisterKeyVisual(id, this);
            StartIdlePulse();
        }

        private void StartIdlePulse()
        {
            if (idlePulseScale <= 1f || idlePulseHalfDuration <= 0f) return;
            idleTween = transform.DOScale(baseScale * idlePulseScale, idlePulseHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>Make the key hop to the lock's landing target (position + rotation). Invokes
        /// <paramref name="onLanded"/> when it arrives, then removes the key visual.</summary>
        public void JumpToLock(Transform target, Action onLanded)
        {
            if (leaving || target == null) { onLanded?.Invoke(); return; }
            leaving = true;
            idleTween?.Kill();
            transform.DOScale(baseScale, 0.1f); // settle back from the pulse

            transform.DOJump(target.position, jumpPower, jumpCount, jumpDuration).SetEase(Ease.OutQuad);
            transform.DORotateQuaternion(target.rotation, jumpDuration);
            DOVirtual.DelayedCall(jumpDuration, () =>
            {
                onLanded?.Invoke();
                if (this != null) Destroy(gameObject);
            });
        }

        private void OnDestroy()
        {
            idleTween?.Kill();
            if (KeyManager.Instance != null) KeyManager.Instance.UnregisterKeyVisual(keyId, this);
        }
    }
}
