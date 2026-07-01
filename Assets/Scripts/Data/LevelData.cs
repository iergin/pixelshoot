using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "PixelShoot/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Grid")]
        [SerializeField] private GridData grid = new GridData();

        [Header("Shooters")]
        [Tooltip("Each entry is one column on the bottom of the scene. List order = left-to-right.")]
        [SerializeField] private List<ColumnData> columns = new List<ColumnData>();

        [Header("Conveyor")]
        [Tooltip("How many slots can ride the conveyor at once.")]
        [SerializeField] private int conveyorSlotCapacity = 5;

        [Header("Reserve")]
        [Tooltip("If conveyor is full, shooters jump to reserve. Game fails when reserve is full.")]
        [SerializeField] private int reserveSlotCapacity = 5;

        [Tooltip("Editor-only: the original designer JSON this level was imported from, kept so the Level Editor can hand it back for copying. Not used at runtime.")]
        [HideInInspector] [SerializeField] private string sourceJson;

        public GridData Grid => grid;
        public IReadOnlyList<ColumnData> Columns => columns;
        public int ConveyorSlotCapacity => conveyorSlotCapacity;
        public int ReserveSlotCapacity => reserveSlotCapacity;
        public string SourceJson => sourceJson;
    }
}
