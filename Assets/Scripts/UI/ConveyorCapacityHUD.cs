using UnityEngine;
using PixelShoot.Conveyor;
using TMPro;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drops a "current / capacity" readout onto the HUD for the conveyor.
    /// Turns red when the conveyor is full, and does a small horizontal shake when the
    /// player clicks a shooter that can't board because it's full.
    /// </summary>
    public class ConveyorCapacityHUD : MonoBehaviour
    {
        [SerializeField] private ConveyorController conveyor;
        [SerializeField] private TextMeshPro label;
        [Tooltip("Format string. {0} = occupied, {1} = capacity.")]
        [SerializeField] private string format = "Conveyor: {0} / {1}";

        [Header("Full state")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color fullColor = Color.red;

        [Header("Full-click shake (X only)")]
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeAmplitude = 0.15f;
        [SerializeField] private int shakeVibrato = 12;
        [Tooltip("Seconds for the momentary red flash on a rejected board (fades back to the current colour).")]
        [SerializeField] private float flashDuration = 0.25f;

        private int lastOcc = -1;
        private int lastCap = -1;
        private Vector3 baseLocalPos;
        private Tween shakeTween;
        private Tween flashTween;

        private void Awake()
        {
            if (label != null) baseLocalPos = label.transform.localPosition;
        }

        private void OnEnable()
        {
            if (conveyor != null) conveyor.OnFullSlotAttempt += Shake;
        }

        private void OnDisable()
        {
            if (conveyor != null) conveyor.OnFullSlotAttempt -= Shake;
            shakeTween?.Kill();
            flashTween?.Kill();
        }

        private void LateUpdate()
        {
            if (conveyor == null || label == null) return;
            int occ = conveyor.OccupiedCount;
            int cap = conveyor.Capacity;
            if (occ == lastOcc && cap == lastCap) return;
            lastOcc = occ;
            lastCap = cap;
            label.text = string.Format(format, occ, cap);
            label.color = conveyor.IsFull ? fullColor : normalColor;
        }

        // Rejected board cue: a small horizontal jitter + a momentary red flash. The flash is the
        // key cue when the conveyor ISN'T fully full (e.g. a 2-bus link tapped at 4/5), where the
        // label wouldn't otherwise turn red.
        private void Shake()
        {
            if (label == null) return;

            shakeTween?.Kill();
            label.transform.localPosition = baseLocalPos;
            shakeTween = label.transform
                .DOShakePosition(shakeDuration, new Vector3(shakeAmplitude, 0f, 0f), shakeVibrato, 90f, false, true)
                .SetUpdate(true);

            // Flash to fullColor, then fade back to whatever colour the state calls for (red if now
            // full, white otherwise). LateUpdate only rewrites the colour when occ/cap change — a
            // rejected board changes neither — so it won't stomp this flash mid-way.
            flashTween?.Kill();
            Color settle = conveyor != null && conveyor.IsFull ? fullColor : normalColor;
            label.color = fullColor;
            flashTween = DOTween.To(() => label.color, c => label.color = c, settle, flashDuration).SetUpdate(true);
        }
    }
}
