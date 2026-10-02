using System.Collections.Generic;
using UnityEngine;
using PixelShoot.Shooters;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// "Shuffle" booster: randomly rearranges the buses occupying the front <see cref="rowsToShuffle"/>
    /// rows across ALL columns (columns ordered left→right by world X). Constraints:
    ///  • Linked buses (with ≥2 members in the shuffled set) end up on the SAME row in ADJACENT columns.
    ///  • Locked buses never land on the front row — they go to a random spot in rows 2..N.
    ///  • Surprises can land anywhere; if one surfaces to the front it reveals (handled by the column).
    /// The buses behind the shuffled rows stay put. A randomized constructive solver places the
    /// constrained buses first, then fills the rest; if it can't satisfy links it falls back to a
    /// lock-only placement so the booster always does something.
    /// </summary>
    public class ShuffleController : MonoBehaviour
    {
        [Tooltip("How many front rows (from the clickable top down) take part in the shuffle.")]
        [SerializeField, Min(1)] private int rowsToShuffle = 3;
        [Tooltip("Randomized solver attempts before falling back to lock-only placement.")]
        [SerializeField, Min(1)] private int solverAttempts = 300;

        private readonly System.Random rng = new System.Random();

        public void Shuffle()
        {
            // Columns left → right.
            var cols = new List<ShooterColumn>();
            foreach (var c in ShooterColumn.All) if (c != null) cols.Add(c);
            if (cols.Count == 0) return;
            cols.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            int C = cols.Count;
            int R = rowsToShuffle;

            int[] depth = new int[C];
            bool[,] exists = new bool[C, R];          // shuffle slot (c, row-1) present (a bus)?
            var fixedItem = new Shooter[C, R];        // lock barriers stay in their slot
            var buses = new List<Shooter>();
            for (int c = 0; c < C; c++)
            {
                var list = cols[c].Shooters;
                depth[c] = list.Count;
                for (int r = 1; r <= R; r++)
                {
                    if (depth[c] < r) continue;
                    var item = list[depth[c] - r]; // row r from the top
                    if (item == null) continue;
                    if (item is Lock) { fixedItem[c, r - 1] = item; continue; } // lock is fixed, not shuffled
                    exists[c, r - 1] = true;
                    buses.Add(item);
                }
            }
            if (buses.Count == 0) return;

            var occ = Solve(depth, exists, C, R, buses);
            if (occ == null) return;

            // Build each column's new bottom-to-top list from the ORIGINAL snapshot, then apply.
            var newLists = new List<List<Shooter>>(C);
            for (int c = 0; c < C; c++)
            {
                var list = cols[c].Shooters;
                int deepCount = Mathf.Max(0, depth[c] - R); // items below the shuffled rows stay
                var newList = new List<Shooter>(depth[c]);
                for (int i = 0; i < deepCount; i++) newList.Add(list[i]);
                for (int r = R; r >= 1; r--)                 // row R (lower) → row 1 (top)
                {
                    if (depth[c] < r) continue;
                    if (fixedItem[c, r - 1] != null) newList.Add(fixedItem[c, r - 1]); // lock keeps its slot
                    else if (exists[c, r - 1]) newList.Add(occ[c, r - 1]);
                }
                newLists.Add(newList);
            }
            for (int c = 0; c < C; c++) cols[c].ApplyShuffledStack(newLists[c]);
        }

        // ── Constraint solver ────────────────────────────────────────────────
        private Shooter[,] Solve(int[] depth, bool[,] exists, int C, int R, List<Shooter> buses)
        {
            // Link groups with ≥2 members present in the shuffled set.
            var groupsById = new Dictionary<int, List<Shooter>>();
            foreach (var b in buses)
            {
                if (!b.IsLinked || b.LinkGroupId == 0) continue;
                if (!groupsById.TryGetValue(b.LinkGroupId, out var g)) { g = new List<Shooter>(); groupsById[b.LinkGroupId] = g; }
                g.Add(b);
            }
            var multiGroups = new List<List<Shooter>>();
            foreach (var kv in groupsById) if (kv.Value.Count >= 2) multiGroups.Add(kv.Value);

            for (int attempt = 0; attempt < solverAttempts; attempt++)
            {
                var occ = new Shooter[C, R];
                var used = new bool[C, R];
                var placed = new HashSet<Shooter>();
                bool ok = true;

                // 1) Linked groups: same row, adjacent columns.
                foreach (var g in Shuffled(multiGroups))
                {
                    int k = g.Count;
                    var rows = new List<int>();
                    for (int r = 1; r <= R; r++) rows.Add(r);
                    Shuffle(rows);

                    bool placedGroup = false;
                    foreach (int r in rows)
                    {
                        var starts = new List<int>();
                        for (int c0 = 0; c0 + k <= C; c0++)
                        {
                            bool valid = true;
                            for (int j = 0; j < k; j++)
                                if (!exists[c0 + j, r - 1] || used[c0 + j, r - 1]) { valid = false; break; }
                            if (valid) starts.Add(c0);
                        }
                        if (starts.Count == 0) continue;

                        int c0pick = starts[rng.Next(starts.Count)];
                        var mem = new List<Shooter>(g); Shuffle(mem);
                        for (int j = 0; j < k; j++)
                        {
                            occ[c0pick + j, r - 1] = mem[j];
                            used[c0pick + j, r - 1] = true;
                            placed.Add(mem[j]);
                        }
                        placedGroup = true;
                        break;
                    }
                    if (!placedGroup) { ok = false; break; }
                }
                if (!ok) continue;

                // 2) Everyone else → the remaining slots (any row).
                var freeBuses = new List<Shooter>();
                foreach (var b in buses) if (!placed.Contains(b)) freeBuses.Add(b);
                var freeSlots = FreeSlots(exists, used, C, R, minRow: 1);
                if (freeBuses.Count != freeSlots.Count) continue; // safety
                Shuffle(freeBuses); Shuffle(freeSlots);
                for (int i = 0; i < freeBuses.Count; i++)
                {
                    var (c, r) = freeSlots[i];
                    occ[c, r - 1] = freeBuses[i]; used[c, r - 1] = true;
                }

                return occ;
            }

            Debug.LogWarning("[Shuffle] Couldn't satisfy link adjacency in the given layout → simple fallback.");
            return FallbackSolve(exists, C, R, buses);
        }

        // Best-effort placement (links may not be adjacent): just fill every slot with a bus.
        private Shooter[,] FallbackSolve(bool[,] exists, int C, int R, List<Shooter> buses)
        {
            var occ = new Shooter[C, R];
            var used = new bool[C, R];
            var free = new List<Shooter>(buses);
            Shuffle(free);

            var slots = FreeSlots(exists, used, C, R, minRow: 1);
            Shuffle(slots);
            for (int i = 0; i < slots.Count && i < free.Count; i++)
            {
                var (c, r) = slots[i]; occ[c, r - 1] = free[i]; used[c, r - 1] = true;
            }
            return occ;
        }

        private static List<(int c, int r)> FreeSlots(bool[,] exists, bool[,] used, int C, int R, int minRow)
        {
            var slots = new List<(int, int)>();
            for (int c = 0; c < C; c++)
                for (int r = minRow; r <= R; r++)
                    if (exists[c, r - 1] && !used[c, r - 1]) slots.Add((c, r));
            return slots;
        }

        // ── Fisher-Yates helpers ─────────────────────────────────────────────
        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private List<T> Shuffled<T>(List<T> src)
        {
            var copy = new List<T>(src);
            Shuffle(copy);
            return copy;
        }
    }
}
