using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PixelShoot.Data
{
    /// <summary>
    /// Parses the level-designer JSON export. The schema is fixed:
    /// <code>
    /// {
    ///   "gridSize": 64,
    ///   "palette": ["#RRGGBB", ...],
    ///   "rle": [[-1, 64], [-1, 64], ...],
    ///   "sortColumns": [
    ///     [ { "color": "#...", "colorIndex": 5, "count": 10 }, ... ],
    ///     ...
    ///   ]
    /// }
    /// </code>
    ///
    /// <para>Hand-rolled because Unity's <c>JsonUtility</c> doesn't handle the
    /// nested int[][] or list-of-list-of-object shapes we need. Lightweight
    /// bracket-balanced extractor + per-field regex.</para>
    /// </summary>
    public static class LevelJsonImporter
    {
        public class ColumnShooter
        {
            public int ColorIndex;
            public int Count;
            public bool IsSurprise;   // JSON key "surprise": true
            public int LinkGroupId;   // JSON key "link": N (0 = unlinked)
            public bool IsLock;       // JSON: { "lock": N } — a lock barrier stack item
            public int KeyId;         // the lock's "lock" value = the key id it waits for
        }

        public class Result
        {
            public bool Ok;
            public string Error;
            /// <summary>The JSON's top-level "name" (the level's match id). Empty if absent.</summary>
            public string Name;
            public int GridSize;
            public List<string> PaletteHex = new List<string>();
            /// <summary>Raw text of the rle array — pass directly to RLECodec.TryDecode.</summary>
            public string RleArrayText;
            public List<List<ColumnShooter>> Columns = new List<List<ColumnShooter>>();
            /// <summary>Bomb cells as [x, y] image coordinates (y = row from the top).</summary>
            public List<(int x, int y)> Bombs = new List<(int x, int y)>();
            /// <summary>ALL key cells flattened as [x, y] image coordinates (for counting / legacy use).</summary>
            public List<(int x, int y)> Keys = new List<(int x, int y)>();
            /// <summary>Key cells grouped: each inner list is one KEY (all its pixels share key id
            /// groupIndex+1). Supports both the grouped form <c>[[[x,y],...],[[x,y],...]]</c> and the old
            /// flat form <c>[[x,y],[x,y]]</c> (where each pair is its own 1-cell key).</summary>
            public List<List<(int x, int y)>> KeyGroups = new List<List<(int x, int y)>>();
        }

        public static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;
                return c == '{';
            }
            return false;
        }

        public static Result Parse(string text)
        {
            var result = new Result();
            if (!LooksLikeJson(text)) { result.Error = "Not JSON."; return result; }

            // name — the level's match id (a plain string). Stored on the LevelData so the
            // level-order table can look this level up by name.
            var mName = Regex.Match(text, "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (mName.Success) result.Name = mName.Groups[1].Value;

            // gridSize
            var mGrid = Regex.Match(text, "\"gridSize\"\\s*:\\s*(\\d+)");
            if (mGrid.Success && int.TryParse(mGrid.Groups[1].Value, out int gs)) result.GridSize = gs;

            // palette — collect entries IN ORDER, preserving null slots. The RLE and
            // sortColumns reference palette entries by index, so dropping the nulls would
            // shift every later colour's index (e.g. index 7 → 5) and those cells would
            // fail to map. A null slot is added as null to keep indices aligned.
            string paletteSection = ExtractJsonValue(text, "palette");
            if (!string.IsNullOrEmpty(paletteSection))
            {
                foreach (Match m in Regex.Matches(paletteSection, "#([0-9A-Fa-f]{6})|null"))
                    result.PaletteHex.Add(m.Groups[1].Success ? m.Groups[1].Value.ToUpperInvariant() : null);
            }

            // rle — raw text, RLECodec can parse it directly.
            result.RleArrayText = ExtractJsonValue(text, "rle");

            // sortColumns — array of arrays of shooter objects.
            string colsSection = ExtractJsonValue(text, "sortColumns");
            if (!string.IsNullOrEmpty(colsSection))
                result.Columns = ParseSortColumns(colsSection);

            // bombs — flat array of [x, y] image coordinates.
            result.Bombs = ParseCoordPairs(ExtractJsonValue(text, "bombs"));
            // keys — grouped (each key = a set of pixels) OR the old flat form. KeyGroups keeps the
            // grouping; Keys is the flattened list (all key cells) for counting.
            result.KeyGroups = ParseKeyGroups(ExtractJsonValue(text, "keys"));
            foreach (var g in result.KeyGroups) result.Keys.AddRange(g);

            result.Ok = true;
            return result;
        }

        /// <summary>
        /// Returns the raw substring (including outer braces / brackets) that the JSON value
        /// associated with <paramref name="key"/> spans, or null if the key isn't found.
        /// Handles strings, numbers, objects and arrays. Bracket-balanced; skips inside string literals.
        /// </summary>
        private static string ExtractJsonValue(string text, string key)
        {
            string token = "\"" + key + "\"";
            int keyIdx = text.IndexOf(token);
            if (keyIdx < 0) return null;
            int colon = text.IndexOf(':', keyIdx);
            if (colon < 0) return null;
            int i = colon + 1;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) return null;

            char first = text[i];
            if (first == '[' || first == '{')
            {
                int start = i;
                int depth = 0;
                for (; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '"') { i = SkipString(text, i); continue; }
                    if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}')
                    {
                        depth--;
                        if (depth == 0) { i++; break; }
                    }
                }
                return text.Substring(start, i - start);
            }
            if (first == '"')
            {
                int start = i;
                i = SkipString(text, i) + 1;
                return text.Substring(start, i - start);
            }
            // number / bool / null
            int valueStart = i;
            while (i < text.Length && ",}]\n\r\t ".IndexOf(text[i]) < 0) i++;
            return text.Substring(valueStart, i - valueStart);
        }

        /// <summary>Returns the index of the closing quote of the string starting at <paramref name="i"/>.</summary>
        private static int SkipString(string text, int i)
        {
            // text[i] is the opening "
            i++;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '"') return i;
                i++;
            }
            return text.Length - 1;
        }

        private static List<List<ColumnShooter>> ParseSortColumns(string section)
        {
            // section looks like "[ [{...}, {...}], [{...}, ...], ... ]"
            var result = new List<List<ColumnShooter>>();
            int i = section.IndexOf('[');
            if (i < 0) return result;
            int end = section.LastIndexOf(']');
            if (end <= i) return result;
            i++;
            while (i < end)
            {
                while (i < end && (char.IsWhiteSpace(section[i]) || section[i] == ',')) i++;
                if (i >= end) break;
                if (section[i] != '[') { i++; continue; }
                int colStart = i;
                int depth = 0;
                for (; i < end; i++)
                {
                    char c = section[i];
                    if (c == '"') { i = SkipString(section, i); continue; }
                    if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}')
                    {
                        depth--;
                        if (depth == 0) { i++; break; }
                    }
                }
                string colText = section.Substring(colStart, i - colStart);
                result.Add(ParseShootersInColumn(colText));
            }
            return result;
        }

        private static readonly Regex RxColorIndex = new Regex("\"colorIndex\"\\s*:\\s*(\\d+)");
        private static readonly Regex RxCount      = new Regex("\"count\"\\s*:\\s*(\\d+)");
        private static readonly Regex RxSurprise   = new Regex("\"surprise\"\\s*:\\s*(true|false)");
        private static readonly Regex RxLink       = new Regex("\"link\"\\s*:\\s*(-?\\d+)");
        private static readonly Regex RxLock       = new Regex("\"lock\"\\s*:\\s*(-?\\d+)");
        private static readonly Regex RxCoordPair  = new Regex("\\[\\s*(-?\\d+)\\s*,\\s*(-?\\d+)\\s*\\]");

        /// <summary>Parses a "[[x,y],[x,y],...]" section into a list of (x,y) pairs.</summary>
        private static List<(int x, int y)> ParseCoordPairs(string section)
        {
            var list = new List<(int x, int y)>();
            if (string.IsNullOrEmpty(section)) return list;
            foreach (Match m in RxCoordPair.Matches(section))
                list.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
            return list;
        }

        /// <summary>
        /// Parses the "keys" section into KEY GROUPS. Handles two shapes:
        /// <list type="bullet">
        /// <item><b>Grouped</b> — <c>[ [[x,y],[x,y],...], [[x,y],...] ]</c>: each outer element is one
        /// key made of many pixels.</item>
        /// <item><b>Flat</b> (legacy) — <c>[ [x,y], [x,y] ]</c>: each pair is its own 1-pixel key.</item>
        /// </list>
        /// It decides per top-level element: an element that itself contains a nested '[' is a group of
        /// pairs; otherwise the element IS a single pair (a 1-cell key).
        /// </summary>
        private static List<List<(int x, int y)>> ParseKeyGroups(string section)
        {
            var groups = new List<List<(int x, int y)>>();
            if (string.IsNullOrEmpty(section)) return groups;

            int i = section.IndexOf('[');
            int end = section.LastIndexOf(']');
            if (i < 0 || end <= i) return groups;
            i++; // step past the outer '['

            while (i < end)
            {
                while (i < end && (char.IsWhiteSpace(section[i]) || section[i] == ',')) i++;
                if (i >= end || section[i] != '[') { i++; continue; }

                // Capture this bracket-balanced top-level element.
                int elemStart = i, depth = 0;
                for (; i <= end; i++)
                {
                    char c = section[i];
                    if (c == '[') depth++;
                    else if (c == ']') { depth--; if (depth == 0) { i++; break; } }
                }
                string elem = section.Substring(elemStart, i - elemStart);
                var pairs = ParseCoordPairs(elem);
                if (pairs.Count == 0) continue;

                // A nested '[' after the first char means this element is an ARRAY OF PAIRS (one key
                // group). Otherwise the element is a single "[x,y]" pair = a 1-cell key.
                bool grouped = elem.IndexOf('[', 1) >= 0;
                groups.Add(grouped ? pairs : new List<(int x, int y)> { pairs[0] });
            }
            return groups;
        }

        private static List<ColumnShooter> ParseShootersInColumn(string colText)
        {
            var list = new List<ColumnShooter>();
            int depth = 0;
            int objStart = -1;
            for (int i = 0; i < colText.Length; i++)
            {
                char c = colText[i];
                if (c == '"') { i = SkipString(colText, i); continue; }
                if (c == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string obj = colText.Substring(objStart, i - objStart + 1);
                        var mLock = RxLock.Match(obj);
                        if (mLock.Success)
                        {
                            // A lock barrier stack item: { "lock": N } — N is the key id it waits for.
                            list.Add(new ColumnShooter
                            {
                                IsLock = true,
                                KeyId  = int.Parse(mLock.Groups[1].Value),
                            });
                        }
                        else
                        {
                            var mi = RxColorIndex.Match(obj);
                            var mc = RxCount.Match(obj);
                            if (mi.Success && mc.Success)
                            {
                                var ms = RxSurprise.Match(obj);
                                var ml = RxLink.Match(obj);
                                list.Add(new ColumnShooter
                                {
                                    ColorIndex   = int.Parse(mi.Groups[1].Value),
                                    Count        = int.Parse(mc.Groups[1].Value),
                                    IsSurprise   = ms.Success && ms.Groups[1].Value == "true",
                                    LinkGroupId  = ml.Success ? int.Parse(ml.Groups[1].Value) : 0,
                                });
                            }
                        }
                        objStart = -1;
                    }
                }
            }
            return list;
        }
    }
}
