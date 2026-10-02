using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelShoot.Audio;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop this on any UI <see cref="Button"/> (or raycastable UI element) to, on click:
    /// <list type="bullet">
    /// <item>play a click sound through the <see cref="AudioManager"/>, and</item>
    /// <item>fire a <c>ui_button_click</c> analytics event (param <c>button_id</c>).</item>
    /// </list>
    /// (Formerly <c>ButtonClickSound</c> — sound + analytics merged into one component so every button
    /// that already had it now also reports clicks, with no extra component to add.)
    ///
    /// <para><b>Why a pointer handler, not <c>onClick</c>?</b> Many popups (BasePopup / PlayPopup …)
    /// call <c>button.onClick.RemoveAllListeners()</c> in their own wiring, which would wipe a listener
    /// we added — so hooking <c>onClick</c> goes silent on those buttons. Listening to the pointer-click
    /// event instead is immune: it fires on the actual tap regardless of what the button does with its
    /// <c>onClick</c>.</para>
    ///
    /// <para>Sound: plays the AudioManager named clip <see cref="clipId"/> (<c>"button"</c>) so every
    /// button shares one registered sound; assign <see cref="overrideClip"/> for a per-button sound.
    /// Analytics: set <see cref="buttonId"/> for a meaningful id (falls back to the GameObject name);
    /// untick <see cref="trackClick"/> to skip the event on noisy buttons.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonClickHandler : MonoBehaviour, IPointerClickHandler
    {
        [Header("Sound")]
        [Tooltip("Named clip id to play — must match an entry in AudioManager's 'Named SFX clips' list. " +
                 "Ignored when Override Clip is assigned.")]
        [SerializeField] private string clipId = "button";
        [Tooltip("Optional: play THIS clip instead of the named id (per-button override).")]
        [SerializeField] private AudioClip overrideClip;
        [Tooltip("Volume for the Override Clip (the named clip uses its own registered volume).")]
        [SerializeField, Range(0f, 1f)] private float overrideVolume = 1f;

        [Header("Analytics")]
        [Tooltip("Fire a 'ui_button_click' analytics event on click. Untick for noisy buttons you don't " +
                 "want to track.")]
        [SerializeField] private bool trackClick = true;
        [Tooltip("Logged as the 'button_id' parameter (e.g. 'play', 'shop', 'settings', 'no_ads'). " +
                 "Keep it stable + snake_case. Empty → falls back to the GameObject name.")]
        [SerializeField] private string buttonId;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            // Don't sound / log a disabled control (Button / Toggle / any non-interactable Selectable).
            var selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;

            // ── Sound ──
            var am = AudioManager.Instance;
            if (am != null)
            {
                if (overrideClip != null) am.PlaySfx(overrideClip, overrideVolume);
                else if (!string.IsNullOrEmpty(clipId)) am.PlaySfx(clipId); // gated on SFX-enabled + pooled by AudioManager
            }

            // ── Analytics ──
            if (trackClick)
                PixelShoot.Analytics.AnalyticsEvents.TrackUiButtonClick(
                    string.IsNullOrEmpty(buttonId) ? gameObject.name : buttonId);
        }
    }
}
