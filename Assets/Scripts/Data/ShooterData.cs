using System;
using UnityEngine;

namespace PixelShoot.Data
{
    [Serializable]
    public class ShooterData
    {
        [SerializeField] private ColorData color;
        [SerializeField] private int shotCount = 1;
        [Tooltip("Surprise bus: spawns hidden behind a '?' cover and reveals (color + count) when it surfaces to the top of its column.")]
        [SerializeField] private bool isSurprise;
        [Tooltip("Link group id. 0 = unlinked. Two or more buses sharing the same positive id form a link group that boards together and dissolves together.")]
        [SerializeField] private int linkGroupId;
        [Tooltip("If true, this stack item is a LOCK barrier (not a bus). It ships up the column like a bus and blocks the column when it reaches the top, until its key is collected.")]
        [SerializeField] private bool isLock;
        [Tooltip("Key id the lock waits for (only when Is Lock is true). Opens when this key is collected AND the lock is at the top of its column.")]
        [SerializeField] private int keyId;

        public ColorData Color => color;
        public int ShotCount => shotCount;
        public bool IsSurprise => isSurprise;
        public int LinkGroupId => linkGroupId;
        public bool IsLock => isLock;
        public int KeyId => keyId;
    }
}
