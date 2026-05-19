using System;
using UnityEngine;

namespace PixelShoot.Data
{
    [Serializable]
    public class ShooterData
    {
        [SerializeField] private ColorData color;
        [SerializeField] private int shotCount = 1;

        public ColorData Color => color;
        public int ShotCount => shotCount;
    }
}
