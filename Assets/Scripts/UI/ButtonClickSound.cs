using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Audio;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop this on any UI <see cref="Button"/> to play a click sound through the <see cref="AudioManager"/>
    /// whenever it's pressed. It hooks the button's <c>onClick</c> automatically — no wiring needed.
    ///
    /// <para>By default it plays the AudioManager named clip <see cref="clipId"/> (<c>"button"</c>), so
    /// EVERY button shares one sound you register once in AudioManager's "Named SFX clips" list. Assign
    /// <see cref="overrideClip"/> to give a specific button its own sound instead. Muting is handled by
    /// the AudioManager (respects the Sound/Sfx setting), so nothing plays when SFX are off.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class ButtonClickSound : MonoBehaviour
    {
        [Tooltip("Named clip id to play — must match an entry in AudioManager's 'Named SFX clips' list. " +
                 "Ignored when Override Clip is assigned.")]
        [SerializeField] private string clipId = "button";
        [Tooltip("Optional: play THIS clip instead of the named id (per-button override).")]
        [SerializeField] private AudioClip overrideClip;
        [Tooltip("Volume for the Override Clip (the named clip uses its own registered volume).")]
        [SerializeField, Range(0f, 1f)] private float overrideVolume = 1f;

        private Button button;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(Play); // avoid double-subscribe
                button.onClick.AddListener(Play);
            }
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Play);
        }

        private void Play()
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            if (overrideClip != null) am.PlaySfx(overrideClip, overrideVolume);
            else if (!string.IsNullOrEmpty(clipId)) am.PlaySfx(clipId); // gated on SFX-enabled + pooled by AudioManager
        }
    }
}
