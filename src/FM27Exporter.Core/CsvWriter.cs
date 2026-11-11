using System.IO;
using System.Text;

namespace FM27Exporter.Core
{
    public static class CsvWriter
    {
        public static string Render(ExportTable table, char delimiter = ',')
        {
            var sb = new StringBuilder();
            AppendLine(sb, table.ColumnCount, c => table.Headers[c], delimiter);
            for (var r = 0; r < table.Rows.Count; r++)
            {
                var row = r;
                AppendLine(sb, table.ColumnCount, c => table.Cell(row, c), delimiter);
            }
            return sb.ToString();
        }

        public static void Write(ExportTable table, string path, char delimiter = ',') =>
            File.WriteAllText(path, Render(table, delimiter), new UTF8Encoding(true));

        private static void AppendLine(StringBuilder sb, int count, System.Func<int, string> cell, char delimiter)
        {
            for (var c = 0; c < count; c++)
            {
                if (c > 0)
                    sb.Append(delimiter);
                sb.Append(Quote(cell(c), delimiter));
            }
            sb.Append("\r\n");
        }

        internal static string Quote(string value, char delimiter)
        {
            var needs = value.IndexOf(delimiter) >= 0 || value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0
                        || (value.Length > 0 && (value[0] == ' ' || value[^1] == ' '));
            return needs ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
