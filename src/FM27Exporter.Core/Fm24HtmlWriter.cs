using System.IO;
using System.Text;

namespace FM27Exporter.Core
{
    public static class Fm24HtmlWriter
    {
        private const string Head =
            "<html>\r\n<head>\r\n<title>{0}<br/></title> \r\n<style type =\"text/css\">\r\n" +
            "body,td,th {{\r\nfont-family: Verdana, Arial, Helvetica, sans-serif;\r\nfont-size: 12px;\r\n}}\r\n" +
            "th {{\r\npadding: 5px;\r\ntext-align: left;\r\n}}\r\n" +
            "td {{\r\npadding: 2px;\r\nborder-style: solid;\r\nborder-width: 1px\r\n}}\r\n" +
            "</style>\r\n</head>\r\n<body>\r\n<center><B>{0}<br/>\n</center></B>\r\n<p>\r\n" +
            "<table bordercolor=\"#000000\" width=\"90%\" align=\"center\">\r\n";

        private const string Tail =
            "</table>\r\n<p>\r\n<p align=\"center\">\r\n" +
            "<a HREF=\"https://www.sigames.com/\">https://www.sigames.com/</a></p>\r\n</body>\r\n</html>\r\n";

        public static string Render(ExportTable table)
        {
            var sb = new StringBuilder(256 + table.Rows.Count * table.ColumnCount * 24);
            sb.AppendFormat(Head, Escape(table.Title));
            AppendRow(sb, "th", table.Headers.Count, i => table.Headers[i]);
            for (var r = 0; r < table.Rows.Count; r++)
            {
                var row = r;
                AppendRow(sb, "td", table.ColumnCount, c => table.Cell(row, c));
            }
            sb.Append(Tail);
            return sb.ToString();
        }

        public static void Write(ExportTable table, string path) =>
            File.WriteAllText(path, Render(table), new UTF8Encoding(false));

        private static void AppendRow(StringBuilder sb, string tag, int count, System.Func<int, string> cell)
        {
            sb.Append("<tr bgcolor=\"#EEEEEE\">\r\n");
            for (var i = 0; i < count; i++)
                sb.Append("\t<").Append(tag).Append('>').Append(Escape(cell(i))).Append("</").Append(tag).Append(">\n\r\n");
            sb.Append("</tr>\r\n");
        }

        internal static string Escape(string text)
        {
            if (text.IndexOfAny(new[] { '&', '<', '>' }) < 0)
                return text;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
