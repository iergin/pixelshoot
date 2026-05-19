using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    [Serializable]
    public class ColumnData
    {
        [Tooltip("Order is bottom-to-top. The LAST element is the topmost (clickable) shooter.")]
        [SerializeField] private List<ShooterData> shooters = new List<ShooterData>();

        public IReadOnlyList<ShooterData> Shooters => shooters;
        public int Count => shooters.Count;
    }
}
