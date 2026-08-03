using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelShoot.Audio;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop this on any UI <see cref="Button"/> (or raycastable UI element) to play a click sound
    /// through the <see cref="AudioManager"/> when it's pressed.
    ///
    /// <para><b>Why a pointer handler, not <c>onClick</c>?</b> Many popups (BasePopup / PlayPopup …)
    /// call <c>button.onClick.RemoveAllListeners()</c> in their own wiring, which would wipe a listener
    /// we added — so a sound hooked onto <c>onClick</c> goes silent on those buttons. Listening to the
    /// pointer-click event instead is immune to that: it fires on the actual tap regardless of what the
    /// button does with its <c>onClick</c>.</para>
    ///
    /// <para>By default it plays the AudioManager named clip <see cref="clipId"/> (<c>"button"</c>) so
    /// every button shares one sound registered once in AudioManager. Assign <see cref="overrideClip"/>
    /// for a per-button sound. Muting is handled by the AudioManager (respects the SFX setting).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonClickSound : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Named clip id to play — must match an entry in AudioManager's 'Named SFX clips' list. " +
                 "Ignored when Override Clip is assigned.")]
        [SerializeField] private string clipId = "button";
        [Tooltip("Optional: play THIS clip instead of the named id (per-button override).")]
        [SerializeField] private AudioClip overrideClip;
        [Tooltip("Volume for the Override Clip (the named clip uses its own registered volume).")]
        [SerializeField, Range(0f, 1f)] private float overrideVolume = 1f;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            // Don't sound a disabled button (Button / Toggle / any Selectable that's non-interactable).
            var selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;

            var am = AudioManager.Instance;
            if (am == null) return;
            if (overrideClip != null) am.PlaySfx(overrideClip, overrideVolume);
            else if (!string.IsNullOrEmpty(clipId)) am.PlaySfx(clipId); // gated on SFX-enabled + pooled by AudioManager
        }
    }
}
