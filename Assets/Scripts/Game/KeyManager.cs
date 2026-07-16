using System;
using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    /// <summary>
    /// Tracks which level keys have been collected and unlocks locked buses.
    ///
    /// <para>A key (id &gt; 0) is collected the moment ANY of its grid cells becomes
    /// shootable (Frontier) — see <see cref="GridController"/>. Collection is banked,
    /// not applied instantly: a locked bus only actually unlocks when it reaches the
    /// TOP of its column and finds its key already collected (mirrors HexaSort's
    /// key/lock, with PixelShoot's "wait until it surfaces" twist).</para>
    /// </summary>
    public class KeyManager : MonoBehaviour
    {
        public static KeyManager Instance { get; private set; }

        private readonly HashSet<int> collected = new HashSet<int>();
        private readonly HashSet<int> consumed  = new HashSet<int>();
        // False until the level has finished spawning its columns. During the build, a key cell
        // sitting on the initial frontier collects its key BEFORE the columns exist, so the
        // "no lock waiting → consume now" shortcut can't be trusted yet — defer it to OnLevelReady.
        private bool levelReady;

        /// <summary>Fired when a key is CONSUMED — its lock reached the top and opened (or, if no
        /// lock is waiting on it, the moment it's collected). This is the "key flies away + its
        /// covered boxes reveal" beat. Distinct from banking (collection), which only arms the
        /// lock; the key visually waits in place until its lock surfaces.</summary>
        public event Action<int> OnKeyConsumed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Wipe collected/consumed keys at the start of a level build.</summary>
        public void ResetForLevel() { collected.Clear(); consumed.Clear(); levelReady = false; }

        /// <summary>Columns are all spawned. Consume any key that was banked during the build but
        /// has no lock waiting on it (nothing to surface for). Locks already sitting on top were
        /// opened by SpawnColumns' RefreshTop pass, so their keys are already consumed.</summary>
        public void OnLevelReady()
        {
            levelReady = true;
            foreach (var keyId in collected)
                if (!consumed.Contains(keyId) && !AnyLockWaitsFor(keyId))
                    WarnNoLock(keyId);
        }

        /// <summary>Key id ≤ 0 means "no lock", treated as always available.</summary>
        public bool IsCollected(int keyId) => keyId <= 0 || collected.Contains(keyId);

        /// <summary>
        /// BANK a key (arm its lock). Idempotent. The key is NOT visually consumed here — it
        /// stays put until its lock surfaces to the top of a column and opens. We refresh every
        /// column top so a lock ALREADY sitting on top opens (and consumes the key) right now.
        /// If no lock is waiting on this key at all, there's nothing to surface for, so consume
        /// it immediately (otherwise the key + its hidden boxes would hang forever).
        /// </summary>
        public void Collect(int keyId)
        {
            if (keyId <= 0) return;
            if (!collected.Add(keyId))
            {
                Debug.Log($"[KEYLOCK] Key {keyId} → Collect çağrıldı ama zaten TOPLANMIŞTI, atlanıyor.");
                return;
            }

            Debug.Log($"[KEYLOCK] Key {keyId} → TOPLANDI (bankalandı). Şimdi tüm column tepelerini kontrol ediyorum; en üstte bu key'in lock'u varsa açılacak.");

            var cols = ShooterColumn.All;
            for (int i = 0; i < cols.Count; i++)
                cols[i]?.RefreshTop(); // a lock on top opens now → ConsumeKey

            if (consumed.Contains(keyId))
                return; // a lock was on top and already opened + consumed it (logged in TryOpen)

            // The key is CONSUMED only when a lock actually opens — never on collection. If no
            // lock waits on this id, the data is mis-authored (keyId mismatch); warn instead of
            // silently flying the key while a mismatched lock sits there.
            if (levelReady && !AnyLockWaitsFor(keyId))
                WarnNoLock(keyId);
            else
                Debug.Log($"[KEYLOCK] Key {keyId} → bankada BEKLİYOR: eşleşen lock henüz bir column'un en üstünde değil. Lock en üste gelince uçacak.");
        }

        private static void WarnNoLock(int keyId) =>
            Debug.LogWarning($"[KEYLOCK] Key {keyId} TOPLANDI ama hiçbir column'da bu key'i bekleyen lock YOK — " +
                             "lock'un keyId'si boyanan key hücreleriyle eşleşmiyor olabilir. Eşleşen bir lock açılana kadar key bekler.");

        /// <summary>
        /// CONSUME a key: the "fly away + reveal covered boxes" beat. Called by a <see cref="Lock"/>
        /// the instant it opens (reaches the top with its key banked). Idempotent — multiple locks
        /// sharing a key id only fire the visual once.
        /// </summary>
        public void ConsumeKey(int keyId)
        {
            if (keyId <= 0) return;
            if (!consumed.Add(keyId))
            {
                Debug.Log($"[KEYLOCK] Key {keyId} → ConsumeKey çağrıldı ama zaten UÇMUŞTU (başka bir lock tüketmiş), atlanıyor.");
                return;
            }
            Debug.Log($"[KEYLOCK] Key {keyId} → UÇUYOR: key visual süzülüp yok oluyor + bu keyId'li box'lar görünür oluyor.");
            OnKeyConsumed?.Invoke(keyId);
        }

        private static bool AnyLockWaitsFor(int keyId)
        {
            var cols = ShooterColumn.All;
            for (int i = 0; i < cols.Count; i++)
                if (cols[i] != null && cols[i].HasLockWithKey(keyId)) return true;
            return false;
        }
    }
}
