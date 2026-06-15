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

        /// <summary>Fired when a key id is collected for the first time.</summary>
        public event Action<int> OnKeyCollected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Wipe collected keys at the start of a level build.</summary>
        public void ResetForLevel() => collected.Clear();

        /// <summary>Key id ≤ 0 means "no lock", treated as always available.</summary>
        public bool IsCollected(int keyId) => keyId <= 0 || collected.Contains(keyId);

        /// <summary>
        /// Bank a key. Idempotent. On the first collect, fires the event and re-checks
        /// every column's top so a bus already sitting on top unlocks immediately.
        /// </summary>
        public void Collect(int keyId)
        {
            if (keyId <= 0) return;
            if (!collected.Add(keyId)) return; // already collected
            OnKeyCollected?.Invoke(keyId);

            var cols = ShooterColumn.All;
            for (int i = 0; i < cols.Count; i++)
                cols[i]?.RefreshTop();
        }
    }
}
