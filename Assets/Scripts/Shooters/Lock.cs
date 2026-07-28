using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// A column barrier that occupies a slot in the stack exactly like a bus, but blocks the
    /// column until its key is collected. It is never boardable, never counts as a live bus,
    /// is ignored by the Claw, and is kept fixed by Shuffle.
    ///
    /// <para>A Lock IS-A <see cref="Shooter"/> only so it fits the column's <c>List&lt;Shooter&gt;</c>
    /// and reuses the stacking/positioning plumbing. It uses NONE of the bus fields (colour,
    /// shots, seats, wobble) — its setup goes through <see cref="InitializeAsLock"/>, not the
    /// bus <see cref="Shooter.Initialize"/>, so it never registers as alive.</para>
    /// </summary>
    public class Lock : Shooter
    {
        [Header("Lock")]
        [Tooltip("The lock model shown while this barrier is closed. Popped away on open.")]
        [SerializeField] private GameObject lockVisual;
        [Tooltip("Animator that plays the open animation. Its trigger is fired once the key lands. " +
                 "Add an Animation Event at the END of the Open clip calling OnOpenAnimationFinished().")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger name fired on the Animator once the key has jumped in.")]
        [SerializeField] private string openTrigger = "Open";
        [Tooltip("The collected key DOJumps to THIS transform's position + rotation before the lock " +
                 "opens. Leave empty to use the lock's own transform.")]
        [SerializeField] private Transform keyLandTarget;

        /// <summary>Key id this lock waits for; opens once that key is collected.</summary>
        public int KeyId { get; private set; }

        private bool opening; // set once the open sequence starts, so taps/RefreshTop don't re-trigger it

        /// <summary>Set this up as a lock barrier waiting on <paramref name="keyId"/>. No colour,
        /// no seats, no RegisterAlive — just take a resting column slot and show the lock.</summary>
        public void InitializeAsLock(int keyId)
        {
            KeyId = keyId;
            ApplyColumnScale();
            if (lockVisual != null) lockVisual.SetActive(true);
        }

        /// <summary>
        /// Called when this lock is the top of its column. If its key has been collected, open:
        /// drop out of the column (which restacks) and destroy with a pop. Returns true if opened.
        /// </summary>
        public bool TryOpen()
        {
            if (opening) return true; // open sequence already running — swallow further taps

            // A lock needs a REAL, collected key. KeyId <= 0 is a misconfigured lock (KeyManager
            // treats id ≤ 0 as "always available" for buses, which would open the lock instantly)
            // — never auto-open it; the level editor flags such locks so they get a real key id.
            if (KeyId <= 0)
            {
                Debug.Log($"[KEYLOCK] Lock '{name}' TryOpen → AÇILMADI: keyId={KeyId} (≤0, hatalı ayar — bu lock asla açılmaz). Editörde lock'a pozitif key id ver.", this);
                return false;
            }
            var km = PixelShoot.Game.KeyManager.Instance;
            if (km == null)
            {
                Debug.Log($"[KEYLOCK] Lock '{name}' (key={KeyId}) TryOpen → AÇILMADI: sahnede KeyManager yok.", this);
                return false;
            }
            if (!km.IsCollected(KeyId))
            {
                Debug.Log($"[KEYLOCK] Lock '{name}' (key={KeyId}) TryOpen → BEKLİYOR: key {KeyId} henüz TOPLANMADI (bankalanmadı).", this);
                return false;
            }

            opening = true;
            Debug.Log($"[KEYLOCK] Lock '{name}' (key={KeyId}) TryOpen → AÇILIYOR: key jump ile lock'a geliyor...", this);

            // Tell the key to hop onto this lock (pos + rot). When it lands → open animation.
            var target = keyLandTarget != null ? keyLandTarget : transform;
            var kv = km.GetKeyVisual(KeyId);
            if (kv != null && Application.isPlaying) kv.JumpToLock(target, BeginOpenAnimation);
            else BeginOpenAnimation(); // no key visual (or edit mode) → open straight away
            return true;
        }

        /// <summary>Key has landed: reveal its covered boxes and fire the Animator's open trigger.
        /// If there's no Animator wired up, we skip straight to removing the lock.</summary>
        private void BeginOpenAnimation()
        {
            PixelShoot.Game.KeyManager.Instance?.ConsumeKey(KeyId); // reveal the key's covered boxes
            PixelShoot.Audio.AudioManager.Instance?.PlayLockOpen(); // unlock SFX

            if (Application.isPlaying && animator != null && !string.IsNullOrEmpty(openTrigger))
            {
                Debug.Log($"[KEYLOCK] Lock '{name}' → Animator '{openTrigger}' trigger'landı. Open klibinin SONUNA OnOpenAnimationFinished() çağıran bir Animation Event ekle.", this);
                animator.SetTrigger(openTrigger);
            }
            else
            {
                Debug.Log($"[KEYLOCK] Lock '{name}' → Animator/trigger yok, lock direkt kaldırılıyor.", this);
                OnOpenAnimationFinished();
            }
        }

        /// <summary>
        /// Hook this up as an Animation Event at the END of the lock's Open clip. Drops the lock
        /// out of its column (which restacks the buses below) and destroys it.
        /// </summary>
        public void OnOpenAnimationFinished()
        {
            Debug.Log($"[KEYLOCK] Lock '{name}' → Open animasyonu bitti (Animation Event): lock column'dan kaldırılıp yok ediliyor.", this);
            ShooterColumn.ColumnOf(this)?.RemoveShooter(this); // remove + restack the column
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>Little shake when a still-locked barrier is tapped (key not collected yet).</summary>
        public void PlayLockedFeedback()
        {
            if (!Application.isPlaying) return;
            Transform t = lockVisual != null ? lockVisual.transform : transform;
            t.DOShakePosition(0.25f, 0.12f, 12, 90f, false, true);
        }
    }
}
