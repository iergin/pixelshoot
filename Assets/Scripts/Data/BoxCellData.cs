using System;
using UnityEngine;

namespace PixelShoot.Data
{
    [Serializable]
    public class BoxCellData
    {
        [SerializeField] private int gridX;
        [SerializeField] private int gridZ;
        [SerializeField] private bool isEmpty;
        [SerializeField] private ColorData color;

        public int GridX => gridX;
        public int GridZ => gridZ;
        public bool IsEmpty => isEmpty || color == null;
        public ColorData Color => color;
    }
}
