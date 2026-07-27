using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PixelShoot.Conveyor;
using PixelShoot.Data;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    public enum GameState { Playing, Won, Failed }

    public class GameController : MonoBehaviour
    {
        [SerializeField] private GridController grid;
        [SerializeField] private ConveyorController conveyor;
        [SerializeField] private ReserveController reserve;
        [SerializeField] private PlayOnReserveController playOnReserve;

        [Header("HUD")]
        [Tooltip("In-game settings (gear) button. Opens the IngameSettingsPopup via PopupService.")]
        [SerializeField] private Button settingsButton;

        [Header("Endgame (last buses)")]
        [Tooltip("Fallback threshold used only if no conveyor is bound. Normally the endgame triggers when alive buses <= the conveyor's CAPACITY (so a capacity booster raises the threshold too).")]
        [SerializeField] private int lastBusesThreshold = 5;
        [Tooltip("Conveyor speed multiplier once the endgame kicks in.")]
        [SerializeField, Min(1f)] private float endgameSpeedMultiplier = 2f;

        private bool endgameActive;
        private bool levelReady; // set once all buses have spawned, so the ramp-up during build doesn't trip it

        private GameState state = GameState.Playing;
        private CoinsConfig coinsConfig;

        /// <summary>The Game-scene controller, so cross-scene popups (fail / quit flow, spawned by
        /// PopupService in the InitializeScene) can reach PlayOn / ReloadScene / revive cost.</summary>
        public static GameController Instance { get; private set; }

        public GameState State => state;
        public event Action OnLevelWon;
        public event Action OnLevelFailed;
        /// <summary>Fired when the player tries to Play-On but cannot afford the revive cost.</summary>
        public event Action OnPlayOnDenied;

        public int ReviveCost => coinsConfig != null ? coinsConfig.ReviveCost : 0;
        public bool CanAffordRevive => PlayerWallet.CanAfford(ReviveCost);

        public void SetCoinsConfig(CoinsConfig cfg) => coinsConfig = cfg;

        private void Awake()
        {
            Instance = this;
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OpenIngameSettings);
            }
        }

        /// <summary>Open the in-game settings popup (gear button). Public so a HUD button can call it too.</summary>
        public void OpenIngameSettings()
        {
            if (PixelShoot.UI.PopupService.Instance != null)
                PixelShoot.UI.PopupService.Instance.Create<PixelShoot.UI.IngameSettingsPopup>();
            else
                Debug.LogWarning("[GameController] No PopupService — can't open in-game settings.");
        }

        public void Bind(GridController g, ConveyorController c, ReserveController r, PlayOnReserveController p = null)
        {
            grid = g;
            conveyor = c;
            reserve = r;
            playOnReserve = p;
            if (grid != null) grid.OnGridCleared += HandleGridCleared;

            // Fresh level → reset endgame and re-arm the alive-bus watcher.
            endgameActive = false;
            levelReady = false;
            Shooter.ClearAliveRegistry();
            Shooter.AliveCountChanged -= EvaluateEndgame;
            Shooter.AliveCountChanged += EvaluateEndgame;
            // Re-evaluate when the conveyor capacity changes (capacity booster raises the threshold).
            if (conveyor != null)
            {
                conveyor.OnCapacityChanged -= EvaluateEndgame;
                conveyor.OnCapacityChanged += EvaluateEndgame;
            }
        }

        private void OnDisable()
        {
            Shooter.AliveCountChanged -= EvaluateEndgame;
            if (conveyor != null) conveyor.OnCapacityChanged -= EvaluateEndgame;
        }

        /// <summary>Called by the loader once every bus has spawned, so the count ramp-up
        /// during build can't trip the endgame. Also evaluates immediately (covers levels
        /// that already have ≤ threshold buses).</summary>
        /// <summary>Fired once the level is built and all buses have spawned — the game-side
        /// "level started" signal (streak gifts, etc. hook this instead of the menu's OnGameStarted,
        /// so they keep working when the menu lives in a separate scene).</summary>
        public event Action OnLevelReady;

        public void NotifyLevelReady()
        {
            levelReady = true;
            EvaluateEndgame();
            OnLevelReady?.Invoke();
        }

        /// <summary>Enter endgame once only the last few buses remain: crank the conveyor and
        /// switch path-end handling from "reserve" to "loop back to start".</summary>
        private void EvaluateEndgame()
        {
            if (!levelReady || endgameActive) return;
            // Threshold = conveyor capacity, so a capacity booster raises it too ("last N" = N slots).
            int threshold = conveyor != null ? conveyor.Capacity : lastBusesThreshold;
            if (Shooter.AliveCount > threshold) return;
            endgameActive = true;
            if (conveyor != null)
            {
                conveyor.SpeedMultiplier = endgameSpeedMultiplier;
                conveyor.LoopMode = true; // riders now wrap seamlessly at the path end
            }
            Debug.Log($"[GameController] Endgame: {Shooter.AliveCount} buses left (<= capacity {threshold}) → conveyor x{endgameSpeedMultiplier}, belt loops.");
        }

        /// <summary>
        /// Column top-shooter click. Boards conveyor if there's room; silently no-ops
        /// otherwise (reserve is reserved for path-end overflows, not column clicks).
        /// </summary>
        public bool RequestLaunch(Shooter shooter)
        {
            if (state != GameState.Playing) return false;

            // Linked buses board as a whole group, removed from every column at once.
            if (shooter != null && shooter.IsLinked)
                return RequestLaunchGroup(shooter);

            // Surprise reveal happens before boarding (defensive — usually already revealed on surface).
            if (shooter != null && shooter.IsSurprise) shooter.RevealSurprise();

            if (conveyor.TryReserveSlot(out float boardingDuration, out float landingProgress))
            {
                BoardConveyor(shooter, boardingDuration, landingProgress);
                return true;
            }
            // Conveyor full → can't board: negative cue.
            PixelShoot.Audio.AudioManager.Instance?.PlayBlocked();
            return false;
        }

        /// <summary>
        /// Board an entire link group from the columns. Requires EVERY member to be in a
        /// column with nothing but other group members stacked above it (so the whole group
        /// can surface) and enough free conveyor slots for all of them. All-or-nothing —
        /// returns false (no visual change) if any precondition fails, matching HexaSort.
        /// </summary>
        private bool RequestLaunchGroup(Shooter tapped)
        {
            var group = tapped.LinkGroup;
            if (group == null || group.Count == 0) return false;

            // 1) Every member must be in a column, unlocked, and boardable (no foreign bus above it).
            //    If a member hasn't surfaced to the top of its column yet, the group can't move —
            //    play a negative cue so the tap gives feedback.
            foreach (var m in group)
            {
                if (m == null || m.State != ShooterState.InColumn) return RejectGroupTap();
                // A lock barrier above any member counts as a non-member above it → buried, so
                // the "nothing but group members above" check below already blocks the group.
                var col = ShooterColumn.ColumnOf(m);
                if (col == null) return RejectGroupTap();
                int idx = col.IndexOf(m);
                if (idx < 0) return RejectGroupTap();
                // "Above" = higher index (top is the last element). Any non-member above → buried.
                for (int i = idx + 1; i < col.Shooters.Count; i++)
                    if (!group.Contains(col.Shooters[i])) return RejectGroupTap();
            }

            // 2) Need a free conveyor slot for every member. If there isn't room for the whole
            //    group (even when the conveyor isn't completely full), flash the capacity HUD too.
            if (conveyor.FreeCount < group.Count) { conveyor.SignalFullSlotAttempt(); return RejectGroupTap(); }

            // 3) Reveal surprises, then detach + board each. Snapshot first — boarding
            //    mutates columns and (on dissolve) the shared group list. Board LEFT→RIGHT
            //    so the leftmost bus always reaches the conveyor first.
            var members = new List<Shooter>(group);
            SortLeftToRight(members);
            foreach (var m in members)
            {
                if (m == null) continue;
                if (m.IsSurprise) m.RevealSurprise();
                ShooterColumn.ColumnOf(m)?.RemoveShooter(m);
                if (conveyor.TryReserveSlot(out float dur, out float landing))
                    BoardConveyor(m, dur, landing);
            }
            return true;
        }

        /// <summary>A linked-group tap that can't board (a member isn't ready): play the
        /// negative cue and reject.</summary>
        private static bool RejectGroupTap()
        {
            PixelShoot.Audio.AudioManager.Instance?.PlayBlocked();
            return false;
        }

        /// <summary>Order buses by world X so the leftmost is first. Nulls sink to the end.</summary>
        private static void SortLeftToRight(List<Shooter> list)
        {
            list.Sort((a, b) =>
            {
                float xa = a != null ? a.transform.position.x : float.MaxValue;
                float xb = b != null ? b.transform.position.x : float.MaxValue;
                return xa.CompareTo(xb);
            });
        }

        /// <summary>
        /// Reserve / play-on slot click. The shooter is in InReserve state regardless of which
        /// reservoir owns it — we check both before boarding the conveyor.
        /// </summary>
        // ── Claw booster ─────────────────────────────────────────────────────
        /// <summary>
        /// True if the Claw booster could pull this shooter onto the conveyor: it must be in a
        /// column, unlocked, and the conveyor must have enough free slots (the whole link
        /// group for a linked bus). Unlike normal boarding this ignores column burial — the
        /// claw reaches any shooter. Used to highlight/filter selectable buses.
        /// </summary>
        public bool CanClawGrab(Shooter shooter)
        {
            if (state != GameState.Playing || conveyor == null) return false;
            if (shooter == null || shooter.State != ShooterState.InColumn) return false;
            if (shooter is PixelShoot.Shooters.Lock) return false; // the lock itself isn't a grabbable bus (claw ignores locks)

            if (shooter.IsLinked)
            {
                var g = shooter.LinkGroup;
                if (g == null || g.Count == 0) return false;
                foreach (var m in g)
                    if (m == null || m.State != ShooterState.InColumn || m is PixelShoot.Shooters.Lock) return false;
                return conveyor.FreeCount >= g.Count;
            }
            return conveyor.FreeCount >= 1;
        }

        /// <summary>
        /// Claw booster: pull a shooter (buried or not) straight onto the conveyor. Linked →
        /// the whole group boards at once. Surprises reveal on the way. Returns false (no
        /// change) if <see cref="CanClawGrab"/> would reject it.
        /// </summary>
        public bool ClawGrab(Shooter shooter)
        {
            if (!CanClawGrab(shooter)) return false;

            if (shooter.IsLinked)
            {
                var members = new List<Shooter>(shooter.LinkGroup);
                SortLeftToRight(members);
                foreach (var m in members)
                {
                    if (m == null) continue;
                    if (m.IsSurprise) m.RevealSurprise();
                    ShooterColumn.ColumnOf(m)?.RemoveShooter(m);
                    if (conveyor.TryReserveSlot(out float dur, out float landing))
                        BoardConveyor(m, dur, landing);
                }
                return true;
            }

            if (shooter.IsSurprise) shooter.RevealSurprise();
            ShooterColumn.ColumnOf(shooter)?.RemoveShooter(shooter);
            if (conveyor.TryReserveSlot(out float d, out float l))
            {
                BoardConveyor(shooter, d, l);
                return true;
            }
            return false;
        }

        public void RequestBoardFromReserve(Shooter shooter)
        {
            if (state != GameState.Playing) return;
            if (shooter == null || shooter.State != ShooterState.InReserve) return;

            // Linked buses that parked in reserve together board the conveyor together.
            if (shooter.IsLinked) { RequestBoardGroupFromReserve(shooter); return; }

            // A surprise that somehow reached reserve unrevealed reveals on the way out.
            if (shooter.IsSurprise) shooter.RevealSurprise();

            if (!conveyor.TryReserveSlot(out float boardingDuration, out float landingProgress))
            {
                PixelShoot.Audio.AudioManager.Instance?.PlayBlocked(); // conveyor full
                return;
            }

            FreeFromReservoir(shooter);
            BoardConveyor(shooter, boardingDuration, landingProgress);
        }

        /// <summary>
        /// Board an entire link group out of reserve at once. All-or-nothing: every member
        /// must currently be in reserve and the conveyor must have a free slot for each.
        /// </summary>
        private void RequestBoardGroupFromReserve(Shooter tapped)
        {
            var group = tapped.LinkGroup;
            if (group == null || group.Count == 0) return;

            // Every live member must be parked in reserve (they travel as a unit).
            foreach (var m in group)
                if (m == null || m.State != ShooterState.InReserve) { RejectGroupTap(); return; }

            // Need a free conveyor slot for the whole group — flash the capacity HUD if short.
            if (conveyor.FreeCount < group.Count) { conveyor.SignalFullSlotAttempt(); RejectGroupTap(); return; }

            // Snapshot — boarding mutates reservoirs and (on dissolve) the shared group list.
            // Board LEFT→RIGHT so the leftmost bus reaches the conveyor first.
            var members = new List<Shooter>(group);
            SortLeftToRight(members);
            foreach (var m in members)
            {
                if (m == null) continue;
                if (m.IsSurprise) m.RevealSurprise();
                FreeFromReservoir(m);
                if (conveyor.TryReserveSlot(out float dur, out float landing))
                    BoardConveyor(m, dur, landing);
            }
        }

        /// <summary>Remove a shooter from whichever reservoir currently holds it.</summary>
        private void FreeFromReservoir(Shooter shooter)
        {
            if (reserve != null && reserve.Contains(shooter))
                reserve.FreeSlot(shooter);
            else if (playOnReserve != null && playOnReserve.Contains(shooter))
                playOnReserve.Remove(shooter);
        }

        private void BoardConveyor(Shooter shooter, float boardingDuration, float landingProgress)
        {
            conveyor.EvaluatePath(landingProgress, out Vector3 worldPos, out _, out _);
            shooter.OnPathEnded -= HandleRiderPathEnded;
            shooter.OnPathEnded += HandleRiderPathEnded;
            shooter.JumpTo(worldPos, boardingDuration, () =>
            {
                conveyor.RegisterRider(shooter, landingProgress);
            }, ShooterState.OnConveyor);
        }

        private bool SendToReserve(Shooter shooter)
        {
            if (reserve == null) return false;
            int idx = reserve.FindFreeSlot();
            if (idx < 0) return false;
            reserve.Occupy(idx, shooter);
            var slotWorld = reserve.GetSlotPosition(idx);
            shooter.JumpTo(slotWorld, reserve.JumpDuration, reserve.NotifyIncomingLanded, ShooterState.InReserve, reserve.SlotScale, reserve.SlotRotation);
            return true;
        }

        private void HandleRiderPathEnded(Shooter shooter)
        {
            shooter.OnPathEnded -= HandleRiderPathEnded;

            // A spent UNLINKED bus leaves at the path end. A linked bus NEVER leaves alone —
            // even fully spent it stays bound and parks in reserve with its group; the link
            // only dissolves when EVERY member is spent (handled in Shooter.HandleShotsDepleted).
            if (shooter.ShotsRemaining <= 0 && !shooter.IsLinked)
            {
                conveyor.RemoveRider(shooter);
                shooter.Expire();
                return;
            }

            conveyor.RemoveRider(shooter);

            // Note: in endgame the belt is in LoopMode, so buses with shots WRAP inside
            // Shooter.Update and never reach here — this path only runs pre-endgame.

            // Has shots — try to park in reserve.
            if (SendToReserve(shooter)) return;

            // Reserve full + shots remain: KEEP the shooter on the conveyor at its current
            // end-of-path position. Fail() will pause the conveyor so it just sits there
            // (visible, idle) until the player presses Restart or Play On.
            conveyor.RegisterRider(shooter, conveyor.MaxPathProgress);
            Fail();
        }

        /// <summary>
        /// Player chose "Play On" after a fail. Charges ReviveCost up-front; if the
        /// player can't afford it, no state changes and OnPlayOnDenied fires so the
        /// UI can give feedback. On success, takes every conveyor rider plus one
        /// shooter out of reserve and stuffs them into the play-on reservoir, then
        /// resumes play. Other reserve shooters keep their original slots.
        /// </summary>
        public bool PlayOn()
        {
            if (playOnReserve == null)
            {
                Debug.LogWarning("PlayOn: no PlayOnReserveController bound.");
                return false;
            }

            int cost = ReviveCost;
            if (cost > 0 && !PlayerWallet.TrySpend(cost))
            {
                Debug.Log($"PlayOn denied — need {cost} coins, have {PlayerWallet.Balance}.");
                OnPlayOnDenied?.Invoke();
                return false;
            }

            var riders = conveyor.GetRidersSnapshot();
            // Keep linked buses ADJACENT in play-on (same LinkGroupId together), each group
            // ordered left→right. Unlinked buses (id 0) group up front, also left→right.
            riders.Sort((a, b) =>
            {
                int ga = a != null ? a.LinkGroupId : 0;
                int gb = b != null ? b.LinkGroupId : 0;
                if (ga != gb) return ga.CompareTo(gb);
                float xa = a != null ? a.transform.position.x : float.MaxValue;
                float xb = b != null ? b.transform.position.x : float.MaxValue;
                return xa.CompareTo(xb);
            });
            foreach (var s in riders)
            {
                if (s == null) continue;
                conveyor.RemoveRider(s);
                s.OnPathEnded -= HandleRiderPathEnded;
                playOnReserve.Append(s);
            }

            // Pull the LAST (rightmost) reserve shooter — least-recently used, keeps the front of the queue intact.
            var fromReserve = reserve != null ? reserve.TryPopLast() : null;
            if (fromReserve != null) playOnReserve.Append(fromReserve);

            // Resume normal play: unfreeze the conveyor so future boarders advance again.
            if (conveyor != null) conveyor.IsPaused = false;
            state = GameState.Playing;
            Debug.Log($"PlayOn paid {cost} coins. Balance now {PlayerWallet.Balance}.");
            return true;
        }

        public void ReloadScene()
        {
            // Two-scene: reload just the Game scene (InitializeScene + menu untouched).
            if (SceneFlow.Instance != null) { SceneFlow.Instance.ReloadGame(); return; }
            // Fallback (single-scene / no SceneFlow): reload the whole active scene.
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex >= 0 ? s.buildIndex : 0);
        }

        private void HandleGridCleared()
        {
            if (state != GameState.Playing) return;
            state = GameState.Won;
            Debug.Log("LEVEL WON: grid cleared.");
            PlayerStreak.RegisterWin(); // clearing a level extends the streak
            OnLevelWon?.Invoke();
        }

        private void Fail()
        {
            if (state != GameState.Playing) return;
            state = GameState.Failed;
            if (conveyor != null) conveyor.IsPaused = true;
            Debug.Log("LEVEL FAILED: a returning shooter still had shots but reserve was full.");
            OnLevelFailed?.Invoke();
        }

        // ── Debug helpers (used by SRDebugger options) ───────────────────────
        /// <summary>Force the level into the Won state regardless of grid contents.</summary>
        public void DebugForceWin() => HandleGridCleared();
        /// <summary>Force the level into the Failed state regardless of conveyor contents.</summary>
        public void DebugForceFail() => Fail();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (grid != null) grid.OnGridCleared -= HandleGridCleared;
        }
    }
}
