using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "PixelShoot/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Match id (the source JSON's \"name\"). The level-order table references this string to " +
                 "pick which level to load for a given slot — so it must be unique across all levels.")]
        [SerializeField] private string levelName;

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

        /// <summary>Match id from the source JSON's "name" (used by the level-order table to locate this level).</summary>
        public string LevelName => levelName;
        public GridData Grid => grid;
        public IReadOnlyList<ColumnData> Columns => columns;
        public int ConveyorSlotCapacity => conveyorSlotCapacity;
        public int ReserveSlotCapacity => reserveSlotCapacity;
        public string SourceJson => sourceJson;
    }
}
