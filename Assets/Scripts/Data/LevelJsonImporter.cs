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
        }

        public class Result
        {
            public bool Ok;
            public string Error;
            public int GridSize;
            public List<string> PaletteHex = new List<string>();
            /// <summary>Raw text of the rle array — pass directly to RLECodec.TryDecode.</summary>
            public string RleArrayText;
            public List<List<ColumnShooter>> Columns = new List<List<ColumnShooter>>();
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

            // gridSize
            var mGrid = Regex.Match(text, "\"gridSize\"\\s*:\\s*(\\d+)");
            if (mGrid.Success && int.TryParse(mGrid.Groups[1].Value, out int gs)) result.GridSize = gs;

            // palette — collect all hex strings inside the "palette" array section.
            string paletteSection = ExtractJsonValue(text, "palette");
            if (!string.IsNullOrEmpty(paletteSection))
            {
                foreach (Match m in Regex.Matches(paletteSection, "#([0-9A-Fa-f]{6})"))
                    result.PaletteHex.Add(m.Groups[1].Value.ToUpperInvariant());
            }

            // rle — raw text, RLECodec can parse it directly.
            result.RleArrayText = ExtractJsonValue(text, "rle");

            // sortColumns — array of arrays of shooter objects.
            string colsSection = ExtractJsonValue(text, "sortColumns");
            if (!string.IsNullOrEmpty(colsSection))
                result.Columns = ParseSortColumns(colsSection);

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
                        var mi = RxColorIndex.Match(obj);
                        var mc = RxCount.Match(obj);
                        if (mi.Success && mc.Success)
                        {
                            list.Add(new ColumnShooter
                            {
                                ColorIndex = int.Parse(mi.Groups[1].Value),
                                Count      = int.Parse(mc.Groups[1].Value),
                            });
                        }
                        objStart = -1;
                    }
                }
            }
            return list;
        }
    }
}
