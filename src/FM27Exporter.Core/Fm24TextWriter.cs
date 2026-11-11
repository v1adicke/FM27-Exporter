using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FM27Exporter.Core
{
    public static class Fm24TextWriter
    {
        public static string Render(ExportTable table)
        {
            var n = table.ColumnCount;
            var widths = new int[n];
            for (var c = 0; c < n; c++)
            {
                widths[c] = Width(table.Headers[c]);
                for (var r = 0; r < table.Rows.Count; r++)
                    widths[c] = Math.Max(widths[c], Width(table.Cell(r, c)));
            }

            var sb = new StringBuilder();
            AppendLine(sb, widths, c => table.Headers[c]);
            var lineLength = 4 + Sum(widths) + 3 * Math.Max(0, n - 1) + 1;
            var dashes = "| " + new string('-', lineLength - 4) + "| \r\n";
            sb.Append(dashes);
            for (var r = 0; r < table.Rows.Count; r++)
            {
                var row = r;
                AppendLine(sb, widths, c => table.Cell(row, c));
                sb.Append(dashes);
            }
            return sb.ToString();
        }

        public static void Write(ExportTable table, string path) =>
            File.WriteAllText(path, Render(table), new UTF8Encoding(false));

        private static void AppendLine(StringBuilder sb, int[] widths, Func<int, string> cell)
        {
            sb.Append("| ");
            for (var c = 0; c < widths.Length; c++)
            {
                if (c > 0)
                    sb.Append(" | ");
                var text = Flatten(cell(c));
                sb.Append(text).Append(' ', widths[c] - Width(text));
            }
            sb.Append(" | \r\n");
        }

        internal static int Width(string text) => new StringInfo(Flatten(text)).LengthInTextElements;

        private static string Flatten(string text)
        {
            if (text.IndexOfAny(new[] { '\r', '\n', '|' }) < 0)
                return text;
            return text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        }

        private static int Sum(int[] values)
        {
            var total = 0;
            foreach (var v in values)
                total += v;
            return total;
        }
    }
}
