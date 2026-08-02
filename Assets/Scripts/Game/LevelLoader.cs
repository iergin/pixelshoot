using UnityEngine;
using PixelShoot.Data;
using PixelShoot.Grid;
using PixelShoot.Conveyor;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    public class LevelLoader : MonoBehaviour
    {
        /// <summary>Coins actually paid for the most recent win (base × difficulty multiplier). The
        /// success panel reads this so its readout matches what was granted, without re-deriving the
        /// difficulty (which would race with PlayerProgress advancing on the win).</summary>
        public static int LastWinReward { get; private set; }

        [Header("Data")]
         private LevelData levelData;
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
        [Tooltip("Shows first-time tutorials for special items (Surprise/Link/LockKey/Bomb) present in the level.")]
        [SerializeField] private PixelShoot.UI.SpecialItemTutorialController specialItemTutorial;

        [Header("Shooter spawning")]
        [SerializeField] private Shooter shooterPrefab;
        [Tooltip("Prefab for a LOCK barrier stack item (has a Lock component). Spawned for ColumnData items with Is Lock.")]
        [SerializeField] private Lock lockPrefab;
        [SerializeField] private ShooterColumn columnPrefab;
        [SerializeField] private Transform columnsRoot;
        [SerializeField] private float columnSpacing = 1.4f;

        private bool subscribedToWin;

        private void Start()
        {
            // One-time app init lives in AppBootstrap (InitializeScene) for the two-scene setup —
            // doing it here too would re-bump the session every Game-scene load. Single-scene
            // fallback (no SceneFlow): run it here.
            if (SceneFlow.Instance == null)
            {
                if (coinsConfig != null) PlayerWallet.EnsureInitialized(coinsConfig.InitialBalance);
                PlayerWallet.StampFirstLaunchIfMissing();
                PlayerWallet.BeginSession();
            }

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
        /// Picks a level out of the playlist using PlayerProgress.LevelIndex. Once the player runs
        /// past the last entry, play LOOPS from the playlist's configured Loop Start Level and repeats
        /// (instead of a random pick).
        /// </summary>
        private LevelData PickFromPlaylist()
        {
            if (allLevels == null || allLevels.Count == 0) return null;
            var chosen = allLevels.GetLooping(PlayerProgress.LevelIndex);
            if (chosen == null)
            {
                Debug.LogWarning($"LevelLoader: no level for index {PlayerProgress.LevelIndex}; falling back to a random pick.");
                return allLevels.GetRandom();
            }
            return chosen;
        }

        public void Build()
        {
            // Keys must be reset BEFORE grid.Build — a key cell on the initial frontier
            // collects its key during Build. (No-op in the editor preview.)
            EnsureKeyManager()?.ResetForLevel();

            grid.Build(levelData.Grid);
            conveyor.Initialize(levelData.ConveyorSlotCapacity);
            reserve.Initialize(levelData.ReserveSlotCapacity);
            gameController.Bind(grid, conveyor, reserve, playOnReserve);

            SpawnColumns();
            // Columns exist now: let banked keys that have no lock waiting consume themselves
            // (keys whose lock starts on top were already opened by SpawnColumns' RefreshTop).
            KeyManager.Instance?.OnLevelReady();
            ValidateBulletBudget();

            // All buses have spawned — arm the endgame watcher (fires now if this level
            // already has ≤ threshold buses).
            if (gameController != null) gameController.NotifyLevelReady();

            // First-time tutorials for any special items this level uses.
            if (specialItemTutorial != null) specialItemTutorial.CheckLevel(levelData);
        }

        private KeyManager EnsureKeyManager()
        {
            if (KeyManager.Instance != null) return KeyManager.Instance;
            // Editor preview rebuilds (edit mode) don't need a runtime manager — and creating
            // one would leak GameObjects since Awake (which sets Instance) doesn't run in edit mode.
            if (!Application.isPlaying) return null;
            var go = new GameObject("[KeyManager]");
            return go.AddComponent<KeyManager>();
        }

        private void SpawnColumns()
        {
            int columnCount = levelData.Columns.Count;
            float xOffset = (columnCount - 1) * 0.5f * columnSpacing;

            // Buckets every spawned shooter by its link group id (>0) so we can wire
            // the shared link lists once all of them exist.
            var linkBuckets = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Shooter>>();
            var spawnedColumns = new System.Collections.Generic.List<ShooterColumn>();

            for (int ci = 0; ci < columnCount; ci++)
            {
                var colData = levelData.Columns[ci];
                var column = Instantiate(columnPrefab, columnsRoot != null ? columnsRoot : transform);
                column.transform.localPosition = new Vector3(ci * columnSpacing - xOffset, 0f, 0f);
                column.Initialize(gameController.RequestLaunch, gameController.RequestBoardFromReserve);
                spawnedColumns.Add(column);

                for (int si = 0; si < colData.Count; si++)
                {
                    var sData = colData.Shooters[si];

                    // A lock barrier stack item: spawn the dedicated Lock prefab, no colour/link.
                    if (sData.IsLock)
                    {
                        if (lockPrefab == null)
                        {
                            Debug.LogWarning("[LevelLoader] Column has an isLock item but no Lock Prefab is assigned — skipping it.", this);
                            continue;
                        }
                        var lock_ = Instantiate(lockPrefab);
                        lock_.InitializeAsLock(sData.KeyId);
                        lock_.SetGridAndConveyor(grid, conveyor);
                        column.AddShooter(lock_);
                        continue;
                    }

                    var shooter = Instantiate(shooterPrefab);
                    shooter.Initialize(sData.Color, sData.ShotCount, sData.IsSurprise, sData.LinkGroupId);
                    shooter.SetGridAndConveyor(grid, conveyor);
                    column.AddShooter(shooter);

                    if (sData.LinkGroupId > 0)
                    {
                        if (!linkBuckets.TryGetValue(sData.LinkGroupId, out var list))
                        {
                            list = new System.Collections.Generic.List<Shooter>();
                            linkBuckets[sData.LinkGroupId] = list;
                        }
                        list.Add(shooter);
                    }
                }
            }

            BuildLinkGroups(linkBuckets);

            // Refresh the initial top of every column — reveal surprises sitting on top and
            // unlock any locked top whose key was already collected during grid build.
            foreach (var col in spawnedColumns) col.RefreshTop();
        }

        /// <summary>
        /// Wire each bucket of ≥2 shooters into a shared link group (first member = owner).
        /// A bucket with a single member is a level-data mistake — left unlinked with a warning.
        /// </summary>
        private void BuildLinkGroups(System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Shooter>> buckets)
        {
            foreach (var kv in buckets)
            {
                var members = kv.Value;
                if (members.Count < 2)
                {
                    Debug.LogWarning($"LevelLoader: link group {kv.Key} has only {members.Count} member — needs ≥2. Left unlinked.");
                    continue;
                }
                for (int i = 0; i < members.Count; i++)
                    members[i].SetLinkGroup(members, isOwner: i == 0);

                // Connect the group with Obi ropes (one per adjacent pair).
                PixelShoot.Shooters.LinkRopeController.Instance?.BuildForGroup(members);
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
            // Winning is free — refund the life that was spent when the level started.
            PlayerLives.RefundLevelStart();

            // Mark "first level of this session done" so the no-ads promo controller
            // knows when to consider showing the second-session trigger.
            PlayerWallet.MarkFirstLevelDoneThisSession();

            // Coin reward fires for any real play session (whether using the playlist
            // or a single override level), so designers can test reward economy without
            // hooking up the full playlist asset.
            if (coinsConfig != null && coinsConfig.LevelWinReward > 0)
            {
                // Difficulty (from the JSON table, by current DisplayLevel) multiplies the win reward
                // — Normal x1, Hard x3, Super Hard x5. PlayerProgress hasn't advanced yet here, so
                // DifficultyProvider.Current reflects the level just won.
                int mult = Mathf.Max(1, DifficultyProvider.CurrentRewardMultiplier);
                int reward = coinsConfig.LevelWinReward * mult;
                LastWinReward = reward; // exact amount paid this win (for the success panel to display)
                PlayerWallet.Add(reward);
                Debug.Log($"LevelLoader: paid +{reward} coins on level win (x{mult} for {DifficultyProvider.Current}). " +
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
