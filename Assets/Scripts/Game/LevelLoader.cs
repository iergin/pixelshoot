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
        [Tooltip("Optional explicit override. If set, this level is used and AllLevels is ignored.\n" +
                 "Left null in normal play; the level editor wizard sets this for previews.")]
        [SerializeField] private LevelData levelData;
        [Tooltip("Ordered playlist consulted when no explicit LevelData override is assigned. " +
                 "Picks levels by PlayerProgress.LevelIndex; falls back to a random pick once " +
                 "the index runs past the last entry.")]
        [SerializeField] private AllLevelsData allLevels;
        [Tooltip("Coin tunables (initial balance, win reward, revive cost). " +
                 "GameController also reads ReviveCost from this to gate Play-On.")]
        [SerializeField] private CoinsConfig coinsConfig;

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

        private bool subscribedToWin;

        private void Start()
        {
            // First-run wallet seeding — does nothing if the player already has a saved balance.
            if (coinsConfig != null) PlayerWallet.EnsureInitialized(coinsConfig.InitialBalance);

            // Resolve the level we'll actually play: explicit override beats the playlist.
            if (levelData == null) levelData = PickFromPlaylist();

            if (levelData == null)
            {
                Debug.LogError("LevelLoader: no LevelData and no AllLevelsData entries to fall back on.");
                return;
            }
            Build();

            // Subscribe AFTER Build so the GameController is bound.
            if (gameController != null && !subscribedToWin)
            {
                gameController.OnLevelWon += HandleLevelWon;
                subscribedToWin = true;
            }

            // Let the GameController know about the coins config so it can gate Play-On.
            if (gameController != null) gameController.SetCoinsConfig(coinsConfig);
        }

        /// <summary>
        /// Picks a level out of the playlist using PlayerProgress.LevelIndex.
        /// If the player has exhausted the playlist, returns a random entry instead.
        /// </summary>
        private LevelData PickFromPlaylist()
        {
            if (allLevels == null || allLevels.Count == 0) return null;
            int idx = PlayerProgress.LevelIndex;
            if (idx < allLevels.Count)
            {
                var chosen = allLevels.Get(idx);
                if (chosen != null) return chosen;
                Debug.LogWarning($"LevelLoader: AllLevels[{idx}] is null; falling back to a random pick.");
            }
            return allLevels.GetRandom();
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

        private void HandleLevelWon()
        {
            // Coin reward fires for any real play session (whether using the playlist
            // or a single override level), so designers can test reward economy without
            // hooking up the full playlist asset.
            if (coinsConfig != null && coinsConfig.LevelWinReward > 0)
            {
                PlayerWallet.Add(coinsConfig.LevelWinReward);
                Debug.Log($"LevelLoader: paid +{coinsConfig.LevelWinReward} coins on level win. " +
                          $"Balance now {PlayerWallet.Balance}.");
            }

            // Bump player progress only when we're actually following the playlist;
            // explicit-override sessions (e.g. the level editor preview) shouldn't advance.
            if (allLevels == null || allLevels.Count == 0) return;
            PlayerProgress.Advance();
            Debug.Log($"LevelLoader: PlayerProgress advanced to LevelIndex={PlayerProgress.LevelIndex} " +
                      $"(displayed as Level {PlayerProgress.DisplayLevel}).");
        }

        private void OnDestroy()
        {
            if (gameController != null && subscribedToWin)
            {
                gameController.OnLevelWon -= HandleLevelWon;
                subscribedToWin = false;
            }
        }
    }
}
