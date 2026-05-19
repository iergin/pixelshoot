using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Parses and emits the RLE row format produced by the pixel-art-encoder HTML tool.
    ///
    /// Format: a JS-like array of arrays.  Each inner array is one row of the image
    /// (top-to-bottom in image order), containing alternating (colorIndex, count) pairs.
    /// A colorIndex of -1 means an empty cell.
    ///
    /// Example row:
    ///   [-1, 10, 2, 2, -1, 2, 5, 1, 2, 2, 5, 1, -1, 12]
    ///   = 10 empty, 2x #2, 2 empty, 1x #5, 2x #2, 1x #5, 12 empty.
    /// </summary>
    public static class RLECodec
    {
        /// <summary>
        /// Decode RLE text into a flat int[] indexed by z*gridSize + x.
        /// Image row 0 (first inner array) is mapped to z = gridSize-1 (top of grid)
        /// so the level visually matches the encoder preview when looked at from -Z.
        /// </summary>
        public static bool TryDecode(string rleText, int gridSize, out int[] cells)
        {
            cells = null;
            if (string.IsNullOrWhiteSpace(rleText)) return false;

            var rows = ParseRows(rleText);
            if (rows == null || rows.Count == 0) return false;

            cells = new int[gridSize * gridSize];
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;

            int rowCount = Mathf.Min(rows.Count, gridSize);
            for (int r = 0; r < rowCount; r++)
            {
                var row = rows[r];
                int x = 0;
                for (int i = 0; i + 1 < row.Count && x < gridSize; i += 2)
                {
                    int colorIdx = row[i];
                    int count = row[i + 1];
                    int z = gridSize - 1 - r;
                    for (int k = 0; k < count && x < gridSize; k++)
                    {
                        cells[z * gridSize + x] = colorIdx;
                        x++;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Encode a flat int[] grid into RLE text. The top row of the image (r=0) is
        /// the highest-z row, matching TryDecode.
        /// </summary>
        public static string Encode(int[] cells, int gridSize)
        {
            if (cells == null || cells.Length != gridSize * gridSize) return "[]";
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int r = 0; r < gridSize; r++)
            {
                int z = gridSize - 1 - r;
                sb.Append("  [");
                int cur = cells[z * gridSize + 0];
                int cnt = 1;
                bool first = true;
                for (int x = 1; x < gridSize; x++)
                {
                    int v = cells[z * gridSize + x];
                    if (v == cur) { cnt++; continue; }
                    if (!first) sb.Append(",");
                    sb.Append(cur).Append(",").Append(cnt);
                    first = false;
                    cur = v;
                    cnt = 1;
                }
                if (!first) sb.Append(",");
                sb.Append(cur).Append(",").Append(cnt);
                sb.Append(r == gridSize - 1 ? "]\n" : "],\n");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // Walk the text and collect each level-2 bracket as one row of ints.
        private static List<List<int>> ParseRows(string text)
        {
            var rows = new List<List<int>>();
            int depth = 0;
            int rowStart = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '[')
                {
                    depth++;
                    if (depth == 2) rowStart = i + 1;
                }
                else if (c == ']')
                {
                    if (depth == 2 && rowStart >= 0)
                    {
                        rows.Add(ParseIntList(text.Substring(rowStart, i - rowStart)));
                        rowStart = -1;
                    }
                    depth--;
                }
            }
            return rows.Count > 0 ? rows : null;
        }

        private static List<int> ParseIntList(string content)
        {
            var list = new List<int>();
            var parts = content.Split(',');
            foreach (var p in parts)
            {
                var s = p.Trim();
                if (s.Length == 0) continue;
                if (int.TryParse(s, out int v)) list.Add(v);
            }
            return list;
        }
    }
}
