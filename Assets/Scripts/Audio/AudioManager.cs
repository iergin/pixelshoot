using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Game;

namespace PixelShoot.Audio
{
    /// <summary>
    /// Central audio hub. You hand it a POOL of AudioSources in the inspector; every SFX
    /// request grabs the first idle source (or, if all are busy, the next one round-robin)
    /// and plays a one-shot on it. Music has its own dedicated looping source.
    ///
    /// <para>Mute is split into two independent categories — <b>Music</b> and <b>Effects</b> —
    /// each backed by <see cref="PlayerWallet"/> so the choice survives restarts. The Settings
    /// panel flips those flags; this manager reacts via PlayerWallet's change events.</para>
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX pool")]
        [Tooltip("AudioSources used for one-shot effects. A free (not-playing) source is chosen each time; if all are busy the next one is reused round-robin.")]
        [SerializeField] private List<AudioSource> sfxSources = new List<AudioSource>();

        [Header("Music")]
        [Tooltip("Dedicated looping AudioSource for background music.")]
        [SerializeField] private AudioSource musicSource;
        [Tooltip("Optional clip auto-played (looping) on start.")]
        [SerializeField] private AudioClip defaultMusic;

        [Header("Named SFX clips")]
        [Tooltip("Look up a clip by id with PlaySfx(\"id\"). Keeps callers from holding direct AudioClip references.")]
        [SerializeField] private List<NamedClip> clips = new List<NamedClip>();

        // ══════════════════════════════════════════════════════════════════════════════════════
        #region PixelShoot gameplay SFX — game-specific; swap/remove this whole region for another game
        // ══════════════════════════════════════════════════════════════════════════════════════
        [Header("Common clips")]
        [Tooltip("Played by PlayShooterClick() when a column shooter is tapped.")]
        [SerializeField] private AudioClip shooterClickClip;
        [SerializeField, Range(0f, 1f)] private float shooterClickVolume = 1f;
        [Tooltip("Played by PlayBoxHit() when a box is cleared (transitions to the Hit state).")]
        [SerializeField] private AudioClip boxHitClip;
        [SerializeField, Range(0f, 1f)] private float boxHitVolume = 1f;
        [Tooltip("Minimum seconds between two box-hit SFX. A hit arriving within this window of the previous one is dropped, so bomb ripples / rapid fire don't machine-gun the sound. 0 = no throttle.")]
        [SerializeField, Min(0f)] private float boxHitMinInterval = 0.1f;
        [Tooltip("Separate clip for boxes opened by a BOMB blast (vs a stickman). Has its own throttle.")]
        [SerializeField] private AudioClip boxBombClip;
        [SerializeField, Range(0f, 1f)] private float boxBombVolume = 1f;
        [Tooltip("Minimum seconds between two bomb-open SFX (same idea as Box Hit Min Interval). 0 = no throttle.")]
        [SerializeField, Min(0f)] private float boxBombMinInterval = 0.1f;
        [Tooltip("Negative 'can't do that' cue — e.g. tapping a linked group that can't board yet because a member hasn't surfaced.")]
        [SerializeField] private AudioClip blockedClip;
        [SerializeField, Range(0f, 1f)] private float blockedVolume = 1f;
        [Tooltip("Played by PlayLockOpen() when a lock is unlocked (its key was collected).")]
        [SerializeField] private AudioClip lockOpenClip;
        [SerializeField, Range(0f, 1f)] private float lockOpenVolume = 1f;
        [Tooltip("Played by PlayWin() when a level is cleared.")]
        [SerializeField] private AudioClip winClip;
        [SerializeField, Range(0f, 1f)] private float winVolume = 1f;
        [Tooltip("Played by PlayLose() when a level is failed.")]
        [SerializeField] private AudioClip failClip;
        [SerializeField, Range(0f, 1f)] private float failVolume = 1f;

        [Header("Footsteps")]
        [Tooltip("Single footstep clip played (throttled) as stickmen run. Many runners collapse into one steady patter.")]
        [SerializeField] private AudioClip footstepClip;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;
        [Tooltip("Minimum seconds between footstep sounds ACROSS ALL stickmen — the throttle that stops many runners from turning into machine-gun noise.")]
        [SerializeField, Min(0f)] private float footstepMinInterval = 0.08f;
        [Tooltip("Random pitch range per step, so repeats don't sound identical.")]
        [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.9f, 1.12f);
        [Tooltip("Random ± volume jitter fraction per step (0.2 = ±20%).")]
        [SerializeField, Range(0f, 1f)] private float footstepVolumeJitter = 0.2f;

        private float lastBoxHitTime = -999f;
        private float lastBoxBombTime = -999f;
        private float lastFootstepTime = -999f;
        private AudioSource footstepSource; // dedicated source so per-step pitch changes don't touch other SFX
        #endregion

        [System.Serializable]
        public class NamedClip
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        private Dictionary<string, NamedClip> clipMap;
        private int rrIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            clipMap = new Dictionary<string, NamedClip>();
            foreach (var c in clips)
                if (c != null && !string.IsNullOrEmpty(c.id)) clipMap[c.id] = c;

            // Dedicated source for footsteps so per-step pitch changes don't leak onto other SFX.
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            PlayerWallet.OnMusicEnabledChanged += ApplyMusicState;
            PlayerWallet.OnSfxEnabledChanged   += ApplySfxState;
        }

        private void OnDisable()
        {
            PlayerWallet.OnMusicEnabledChanged -= ApplyMusicState;
            PlayerWallet.OnSfxEnabledChanged   -= ApplySfxState;
        }

        private void Start()
        {
            ApplyMusicState(PlayerWallet.MusicEnabled);
            ApplySfxState(PlayerWallet.SfxEnabled);
            if (defaultMusic != null && musicSource != null) PlayMusic(defaultMusic);
        }

        // ── SFX ──────────────────────────────────────────────────────────────
        /// <summary>Play a one-shot effect on a free pooled source. No-op if Effects are off.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || !PlayerWallet.SfxEnabled) return;
            var src = GetFreeSfxSource();
            if (src != null) src.PlayOneShot(clip, volume);
        }

        /// <summary>Play a named clip from the inspector list.</summary>
        public void PlaySfx(string id)
        {
            if (clipMap != null && clipMap.TryGetValue(id, out var nc) && nc.clip != null)
                PlaySfx(nc.clip, nc.volume);
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        #region PixelShoot gameplay SFX — game-specific cues (shooter/box/bomb/blocked/footstep)
        // ══════════════════════════════════════════════════════════════════════════════════════
        public void PlayShooterClick() => PlaySfx(shooterClickClip, shooterClickVolume);

        /// <summary>Negative cue when an action is rejected (e.g. a linked group can't board yet).</summary>
        public void PlayBlocked() => PlaySfx(blockedClip, blockedVolume);

        /// <summary>Played when a lock unlocks (its key was collected).</summary>
        public void PlayLockOpen() => PlaySfx(lockOpenClip, lockOpenVolume);

        /// <summary>Played when a level is won (grid cleared).</summary>
        public void PlayWin() => PlaySfx(winClip, winVolume);

        /// <summary>Played when a level is failed.</summary>
        public void PlayLose() => PlaySfx(failClip, failVolume);

        public void PlayBoxHit()
        {
            // Throttle: drop hits that land within boxHitMinInterval of the previous one.
            if (boxHitMinInterval > 0f)
            {
                float now = Time.unscaledTime;
                if (now - lastBoxHitTime < boxHitMinInterval) return;
                lastBoxHitTime = now;
            }
            PlaySfx(boxHitClip, boxHitVolume);
        }

        /// <summary>Box opened by a BOMB blast — its own clip + own throttle.</summary>
        public void PlayBoxHitBomb()
        {
            if (boxBombMinInterval > 0f)
            {
                float now = Time.unscaledTime;
                if (now - lastBoxBombTime < boxBombMinInterval) return;
                lastBoxBombTime = now;
            }
            PlaySfx(boxBombClip, boxBombVolume);
        }

        /// <summary>
        /// A stickman footstep. GLOBALLY throttled — no matter how many stickmen are running, at
        /// most one step sound plays per <see cref="footstepMinInterval"/>, so a crowd collapses
        /// into a steady patter instead of machine-gun noise. Pitch/volume are jittered per step.
        /// </summary>
        public void PlayFootstep()
        {
            if (footstepClip == null || !PlayerWallet.SfxEnabled) return;

            float now = Time.unscaledTime;
            if (now - lastFootstepTime < footstepMinInterval) return; // throttle across ALL stickmen
            lastFootstepTime = now;

            if (footstepSource == null) { PlaySfx(footstepClip, footstepVolume); return; }
            footstepSource.pitch = UnityEngine.Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            float vol = footstepVolume * (1f + UnityEngine.Random.Range(-footstepVolumeJitter, footstepVolumeJitter));
            footstepSource.PlayOneShot(footstepClip, Mathf.Clamp01(vol));
        }
        #endregion

        private AudioSource GetFreeSfxSource()
        {
            if (sfxSources == null || sfxSources.Count == 0) return null;
            // Prefer an idle source so simultaneous effects layer cleanly.
            for (int i = 0; i < sfxSources.Count; i++)
                if (sfxSources[i] != null && !sfxSources[i].isPlaying) return sfxSources[i];
            // All busy → round-robin reuse (PlayOneShot layers, so nothing is cut).
            for (int n = 0; n < sfxSources.Count; n++)
            {
                rrIndex = (rrIndex + 1) % sfxSources.Count;
                if (sfxSources[rrIndex] != null) return sfxSources[rrIndex];
            }
            return null;
        }

        // ── Music ────────────────────────────────────────────────────────────
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.mute = !PlayerWallet.MusicEnabled;
            musicSource.Play();
        }

        public void StopMusic() { if (musicSource != null) musicSource.Stop(); }

        private void ApplyMusicState(bool enabled)
        {
            if (musicSource != null) musicSource.mute = !enabled;
        }

        private void ApplySfxState(bool enabled)
        {
            // SFX are gated at play time; also silence any currently-ringing one-shots.
            if (!enabled && sfxSources != null)
                foreach (var s in sfxSources) if (s != null) s.Stop();
        }
    }
}
