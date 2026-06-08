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
        [Tooltip("Pre-assigned tone for this cell. Decided once at authoring time so the cell " +
                 "keeps the same dark/light/normal shade in both its unhit gray state and its hit vivid state.")]
        [SerializeField] private Tone tone;
        [Tooltip("True if this cell is a bomb — when a same-color shooter clears it, the bomb opens a 5×5 area around it after a configurable delay.")]
        [SerializeField] private bool isBomb;

        public int GridX => gridX;
        public int GridZ => gridZ;
        public bool IsEmpty => isEmpty || color == null;
        public ColorData Color => color;
        public Tone Tone => tone;
        public bool IsBomb => isBomb;
    }
}
