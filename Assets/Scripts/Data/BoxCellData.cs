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

        public int GridX => gridX;
        public int GridZ => gridZ;
        public bool IsEmpty => isEmpty || color == null;
        public ColorData Color => color;
        public Tone Tone => tone;
    }
}
