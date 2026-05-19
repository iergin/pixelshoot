using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    [Serializable]
    public class GridData
    {
        [Tooltip("Always square. e.g. 7 means 7x7.")]
        [SerializeField] private int size = 7;

        [Tooltip("Scale applied to the grid root in the scene.")]
        [SerializeField] private Vector3 rootScale = Vector3.one;

        [Tooltip("Only non-empty cells need to be listed; missing coords are treated as empty.")]
        [SerializeField] private List<BoxCellData> cells = new List<BoxCellData>();

        public int Size => size;
        public Vector3 RootScale => rootScale;
        public IReadOnlyList<BoxCellData> Cells => cells;
    }
}
