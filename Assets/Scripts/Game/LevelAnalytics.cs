using UnityEngine;
using PixelShoot.Analytics;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    /// <summary>
    /// Runtime hub for the per-attempt level tracking events (Jira KAN-25). Holds the level context
    /// captured at start plus the live counters accumulated during play, and fires the three events at
    /// the right moments:
    /// <list type="bullet">
    /// <item><b>level_start</b> — <see cref="BeginLevel"/>, from LevelLoader once the grid is built.</item>
    /// <item><b>level_complete</b> — <see cref="FireComplete"/>, from LevelLoader on win.</item>
    /// <item><b>level_fail</b> — <see cref="FireFail"/>, from FailFlowPopup.Finish when the player declines
    /// to continue (reason "fail") or confirms a mid-level quit (reason "quit").</item>
    /// </list>
    ///
    /// <para>A single attempt spans any number of Play-On revives: the counters keep accumulating until a
    /// win or a give-up, so a revived-then-won attempt reports one level_complete (not a fail + a
    /// complete). The <see cref="Active"/> flag also makes every fire idempotent.</para>
    ///
    /// <para>Static (no scene object to wire): GameController, LevelLoader, PlayerBoosters and the fail
    /// popup all feed it directly.</para>
    /// </summary>
    public static class LevelAnalytics
    {
        // ── Context (captured at BeginLevel) ──
        private static string levelId;
        private static int levelIndex;
        private static int attemptNo;
        private static int totalPixels;      // fillable cells at start (for progress_pct)
        private static GridController grid;   // live remaining-box source for progress_pct
        private static bool active;

        // ── Live counters (reset each BeginLevel) ──
        private static int movesMade;
        private static int revivedCount;
        private static int boostersUsed;
        private static int maxSlotsUsed;
        private static int nearOverflowCount;
        private static int lastOccupied;
        private static float startUnscaledTime;

        /// <summary>True between level_start and the terminating event (win/fail/quit).</summary>
        public static bool Active => active;

        /// <summary>Begin an attempt: cache context, reset counters, and fire <c>level_start</c>.
        /// Call from LevelLoader after the grid is built and BEFORE powerups are consumed (the
        /// power_up_* flags in <paramref name="startData"/> must reflect the player's selection).</summary>
        public static void BeginLevel(AnalyticsEvents.LevelStartData startData, GridController gridRef, int totalFillablePixels)
        {
            levelId = startData.level_id;
            levelIndex = startData.level_index;
            attemptNo = startData.attempt_no;
            totalPixels = Mathf.Max(0, totalFillablePixels);
            grid = gridRef;

            movesMade = 0;
            revivedCount = 0;
            boostersUsed = 0;
            maxSlotsUsed = 0;
            nearOverflowCount = 0;
            lastOccupied = 0;
            startUnscaledTime = Time.unscaledTime;
            active = true;

            startData.Track();
        }

        /// <summary>One bus boarded the conveyor (a "move"). Called from GameController.BoardConveyor.</summary>
        public static void RecordMove() { if (active) movesMade++; }

        /// <summary>A successful Play-On revive this attempt. Called from GameController.PlayOn.</summary>
        public static void RecordRevive() { if (active) revivedCount++; }

        /// <summary>A booster was consumed. Called from PlayerBoosters.TryConsume on success.</summary>
        public static void RecordBooster() { if (active) boostersUsed++; }

        /// <summary>Sample conveyor occupancy (called each frame while playing). Tracks the peak and
        /// counts each rising crossing into the near-overflow band (occupancy ≥ capacity-1).</summary>
        public static void SampleOccupancy(int occupied, int capacity)
        {
            if (!active) return;
            if (occupied > maxSlotsUsed) maxSlotsUsed = occupied;
            if (capacity >= 2)
            {
                int band = capacity - 1;                 // e.g. 4 when capacity is 5
                if (occupied >= band && lastOccupied < band) nearOverflowCount++;
            }
            lastOccupied = occupied;
        }

        /// <summary>Fire <c>level_complete</c> on win. <paramref name="movesPar"/> is the solver's move
        /// count if known (0 = unknown).</summary>
        public static void FireComplete(int coinsEarned, int movesPar = 0)
        {
            if (!active) return;
            active = false; // idempotent + stops further counting
            new AnalyticsEvents.LevelCompleteData
            {
                level_id = levelId,
                level_index = levelIndex,
                attempt_no = attemptNo,
                time_sec = ElapsedSeconds(),
                moves_made = movesMade,
                moves_par = movesPar,
                max_slots_used = maxSlotsUsed,
                near_overflow_count = nearOverflowCount,
                boosters_used = boostersUsed,
                revived_count = revivedCount,
                coins_earned = coinsEarned,
            }.Track();
            PlayerAttempts.ClearAttempts(levelId); // won → a later replay restarts at attempt 1
        }

        /// <summary>Fire <c>level_fail</c> when the attempt ends without a win. <paramref name="reason"/>
        /// is "fail" (gave up after a natural fail) or "quit" (mid-level quit).</summary>
        public static void FireFail(string reason)
        {
            if (!active) return;
            active = false; // idempotent
            new AnalyticsEvents.LevelFailData
            {
                level_id = levelId,
                level_index = levelIndex,
                attempt_no = attemptNo,
                progress_pct = ProgressPct(),
                fail_reason = string.IsNullOrEmpty(reason) ? "fail" : reason,
                time_sec = ElapsedSeconds(),
                moves_made = movesMade,
                shooters_left = Shooter.AliveCount,
                near_overflow_count = nearOverflowCount,
                boosters_used = boostersUsed,
                continues_used = revivedCount,
            }.Track();
        }

        private static int ElapsedSeconds() => Mathf.RoundToInt(Mathf.Max(0f, Time.unscaledTime - startUnscaledTime));

        private static int ProgressPct()
        {
            if (totalPixels <= 0) return 0;
            int alive = grid != null ? grid.AliveCount : totalPixels;
            int filled = Mathf.Clamp(totalPixels - alive, 0, totalPixels);
            return Mathf.RoundToInt(100f * filled / totalPixels);
        }
    }
}
