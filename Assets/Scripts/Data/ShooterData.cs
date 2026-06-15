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
        [Tooltip("Lock key id. 0 = unlocked. >0 = locked until the key with this id is collected AND the bus has surfaced to the top of its column.")]
        [SerializeField] private int lockKeyId;

        public ColorData Color => color;
        public int ShotCount => shotCount;
        public bool IsSurprise => isSurprise;
        public int LinkGroupId => linkGroupId;
        public int LockKeyId => lockKeyId;
    }
}
