using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Grid;
using PixelShoot.Conveyor;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    public class LevelLoader : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;

        [Header("Scene refs")]
        [SerializeField] private GridController grid;
        [SerializeField] private ConveyorController conveyor;
        [SerializeField] private ReserveController reserve;
        [SerializeField] private PlayOnReserveController playOnReserve;
        [SerializeField] private GameController gameController;

        [Header("Shooter spawning")]
        [SerializeField] private Shooter shooterPrefab;
        [SerializeField] private ShooterColumn columnPrefab;
        [SerializeField] private Transform columnsRoot;
        [SerializeField] private float columnSpacing = 1.4f;

        private void Start()
        {
            if (levelData == null)
            {
                Debug.LogError("LevelLoader: no LevelData assigned.");
                return;
            }
            Build();
        }

        public void Build()
        {
            grid.Build(levelData.Grid);
            conveyor.Initialize(levelData.ConveyorSlotCapacity);
            reserve.Initialize(levelData.ReserveSlotCapacity);
            gameController.Bind(grid, conveyor, reserve, playOnReserve);

            SpawnColumns();
            ValidateBulletBudget();
        }

        private void SpawnColumns()
        {
            int columnCount = levelData.Columns.Count;
            float xOffset = (columnCount - 1) * 0.5f * columnSpacing;

            for (int ci = 0; ci < columnCount; ci++)
            {
                var colData = levelData.Columns[ci];
                var column = Instantiate(columnPrefab, columnsRoot != null ? columnsRoot : transform);
                column.transform.localPosition = new Vector3(ci * columnSpacing - xOffset, 0f, 0f);
                column.Initialize(gameController.RequestLaunch, gameController.RequestBoardFromReserve);

                for (int si = 0; si < colData.Count; si++)
                {
                    var sData = colData.Shooters[si];
                    var shooter = Instantiate(shooterPrefab);
                    shooter.Initialize(sData.Color, sData.ShotCount);
                    shooter.SetGridAndConveyor(grid, conveyor);
                    column.AddShooter(shooter);
                }
            }
        }

        private void ValidateBulletBudget()
        {
            int boxes = 0;
            foreach (var c in levelData.Grid.Cells) if (!c.IsEmpty) boxes++;
            int bullets = 0;
            foreach (var col in levelData.Columns)
                foreach (var s in col.Shooters) bullets += s.ShotCount;

            if (bullets != boxes)
                Debug.LogWarning($"LevelLoader: bullet budget mismatch — boxes={boxes}, bullets={bullets}");
        }
    }
}
