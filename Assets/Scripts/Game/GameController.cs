using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        [Header("Endgame (last buses)")]
        [Tooltip("When this many (or fewer) buses remain in the whole level, the conveyor speeds up and buses stop using reserve — at path end they loop straight back to the start.")]
        [SerializeField] private int lastBusesThreshold = 5;
        [Tooltip("Conveyor speed multiplier once the endgame kicks in.")]
        [SerializeField, Min(1f)] private float endgameSpeedMultiplier = 2f;

        private bool endgameActive;
        private bool levelReady; // set once all buses have spawned, so the ramp-up during build doesn't trip it

        private GameState state = GameState.Playing;
        private CoinsConfig coinsConfig;

        public GameState State => state;
        public event Action OnLevelWon;
        public event Action OnLevelFailed;
        /// <summary>Fired when the player tries to Play-On but cannot afford the revive cost.</summary>
        public event Action OnPlayOnDenied;

        public int ReviveCost => coinsConfig != null ? coinsConfig.ReviveCost : 0;
        public bool CanAffordRevive => PlayerWallet.CanAfford(ReviveCost);

        public void SetCoinsConfig(CoinsConfig cfg) => coinsConfig = cfg;

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
        }

        private void OnDisable() => Shooter.AliveCountChanged -= EvaluateEndgame;

        /// <summary>Called by the loader once every bus has spawned, so the count ramp-up
        /// during build can't trip the endgame. Also evaluates immediately (covers levels
        /// that already have ≤ threshold buses).</summary>
        public void NotifyLevelReady()
        {
            levelReady = true;
            EvaluateEndgame();
        }

        /// <summary>Enter endgame once only the last few buses remain: crank the conveyor and
        /// switch path-end handling from "reserve" to "loop back to start".</summary>
        private void EvaluateEndgame()
        {
            if (!levelReady || endgameActive) return;
            if (Shooter.AliveCount > lastBusesThreshold) return;
            endgameActive = true;
            if (conveyor != null)
            {
                conveyor.SpeedMultiplier = endgameSpeedMultiplier;
                conveyor.LoopMode = true; // riders now wrap seamlessly at the path end
            }
            Debug.Log($"[GameController] Endgame: {Shooter.AliveCount} buses left → conveyor x{endgameSpeedMultiplier}, belt loops.");
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
            foreach (var m in group)
            {
                if (m == null || m.State != ShooterState.InColumn) return false;
                // A locked member blocks the whole group until its key is collected.
                if (m.IsLocked && !m.TryUnlock()) { m.PlayLockedFeedback(); return false; }
                var col = ShooterColumn.ColumnOf(m);
                if (col == null) return false;
                int idx = col.IndexOf(m);
                if (idx < 0) return false;
                // "Above" = higher index (top is the last element). Any non-member above → buried.
                for (int i = idx + 1; i < col.Shooters.Count; i++)
                    if (!group.Contains(col.Shooters[i])) return false;
            }

            // 2) Need a free conveyor slot for every member.
            if (conveyor.FreeCount < group.Count) return false;

            // 3) Reveal surprises, then detach + board each. Snapshot first — boarding
            //    mutates columns and (on dissolve) the shared group list.
            var members = new List<Shooter>(group);
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

        /// <summary>
        /// Reserve / play-on slot click. The shooter is in InReserve state regardless of which
        /// reservoir owns it — we check both before boarding the conveyor.
        /// </summary>
        public void RequestBoardFromReserve(Shooter shooter)
        {
            if (state != GameState.Playing) return;
            if (shooter == null || shooter.State != ShooterState.InReserve) return;

            // A surprise that somehow reached reserve unrevealed reveals on the way out.
            if (shooter.IsSurprise) shooter.RevealSurprise();

            if (!conveyor.TryReserveSlot(out float boardingDuration, out float landingProgress)) return;

            // Try the regular reserve first; fall back to the play-on reservoir.
            if (reserve != null && reserve.Contains(shooter))
                reserve.FreeSlot(shooter);
            else if (playOnReserve != null && playOnReserve.Contains(shooter))
                playOnReserve.Remove(shooter);

            BoardConveyor(shooter, boardingDuration, landingProgress);
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

            if (shooter.ShotsRemaining <= 0)
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
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex >= 0 ? s.buildIndex : 0);
        }

        private void HandleGridCleared()
        {
            if (state != GameState.Playing) return;
            state = GameState.Won;
            Debug.Log("LEVEL WON: grid cleared.");
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
            if (grid != null) grid.OnGridCleared -= HandleGridCleared;
        }
    }
}
