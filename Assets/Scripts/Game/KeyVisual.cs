using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Game
{
    /// <summary>
    /// The floating key shown at the centroid of its key-group cells. Plays a
    /// collect animation (rise + spin + shrink) when its key id is collected,
    /// then destroys itself. Purely cosmetic — the gating logic lives in
    /// <see cref="KeyManager"/>.
    /// </summary>
    public class KeyVisual : MonoBehaviour
    {
        [Tooltip("Optional spinner that idles while the key is uncollected.")]
        [SerializeField] private float idleSpinDegPerSec = 60f;
        [SerializeField] private float collectRise = 1.5f;
        [SerializeField] private float collectDuration = 0.5f;

        private int keyId;
        private bool collecting;

        public void Init(int id)
        {
            keyId = id;
            if (KeyManager.Instance != null)
                KeyManager.Instance.OnKeyConsumed += HandleKeyConsumed;
        }

        private void Update()
        {
            if (collecting || idleSpinDegPerSec == 0f) return;
            transform.Rotate(Vector3.up, idleSpinDegPerSec * Time.deltaTime, Space.World);
        }

        private void HandleKeyConsumed(int id)
        {
            if (id != keyId || collecting) return;
            collecting = true;
            Unsubscribe();

            transform.DOMoveY(transform.position.y + collectRise, collectDuration).SetEase(Ease.OutQuad);
            transform.DOScale(Vector3.zero, collectDuration).SetEase(Ease.InBack)
                .OnComplete(() => { if (this != null) Destroy(gameObject); });
        }

        private void Unsubscribe()
        {
            if (KeyManager.Instance != null)
                KeyManager.Instance.OnKeyConsumed -= HandleKeyConsumed;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
