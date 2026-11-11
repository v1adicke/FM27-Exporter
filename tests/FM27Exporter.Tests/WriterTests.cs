using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FM27Exporter.Core;
using Xunit;

namespace FM27Exporter.Tests
{
    public class WriterTests
    {
        private static string Fixture(string name) =>
            Path.Combine(AppContext.BaseDirectory, "fixtures", "fm24", name);

        internal static ExportTable ParseFmHtml(string html)
        {
            var rows = Regex.Matches(html, "<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline)
                .Select(m => Regex.Matches(m.Groups[1].Value, "<t[hd]>(.*?)</t[hd]>", RegexOptions.Singleline)
                    .Select(c => System.Net.WebUtility.HtmlDecode(c.Groups[1].Value)).ToArray())
                .ToList();
            var title = Regex.Match(html, "<title>(.*?)<br/></title>").Groups[1].Value;
            return new ExportTable(title, rows[0], rows.Skip(1).Cast<IReadOnlyList<string>>().ToList());
        }

        [Theory]
        [InlineData("squad.html")]
        [InlineData("player-search.html")]
        public void Html_writer_reproduces_genuine_FM_export_byte_for_byte(string fixture)
        {
            var original = File.ReadAllBytes(Fixture(fixture));
            var table = ParseFmHtml(Encoding.UTF8.GetString(original));
            Assert.True(table.Rows.Count > 0);
            var written = Encoding.UTF8.GetBytes(Fm24HtmlWriter.Render(table));
            Assert.Equal(original, written);
        }

        [Fact]
        public void Html_writer_puts_title_and_escapes_markup()
        {
            var t = new ExportTable("Squad", new[] { "Name", "Note" }, new[] { new[] { "A & B", "<1" } });
            var html = Fm24HtmlWriter.Render(t);
            Assert.Contains("<title>Squad<br/></title> \r\n", html);
            Assert.Contains("<center><B>Squad<br/>\n</center></B>\r\n", html);
            Assert.Contains("\t<td>A &amp; B</td>\n\r\n", html);
            Assert.Contains("\t<td>&lt;1</td>\n\r\n", html);
            var back = ParseFmHtml(html);
            Assert.Equal("A & B", back.Cell(0, 0));
            Assert.Equal("<1", back.Cell(0, 1));
        }

        [Fact]
        public void Text_writer_matches_FM24_layout_exactly()
        {
            var t = new ExportTable("", new[] { "Name", "Age", "Wage" },
                new[] { new[] { "Max Aarons", "26", "£50,000 p/w" }, new[] { "Jo", "9", "" } });
            var expected =
                "| Name       | Age | Wage        | \r\n" +
                "| -------------------------------| \r\n" +
                "| Max Aarons | 26  | £50,000 p/w | \r\n" +
                "| -------------------------------| \r\n" +
                "| Jo         | 9   |             | \r\n" +
                "| -------------------------------| \r\n";
            Assert.Equal(expected, Fm24TextWriter.Render(t));
            foreach (var line in expected.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                Assert.Equal(expected.Split("\r\n")[0].Length, line.Length);
        }

        [Fact]
        public void Text_writer_keeps_one_line_per_row()
        {
            var t = new ExportTable("", new[] { "A" }, new[] { new[] { "x|y\nz" } });
            var lines = Fm24TextWriter.Render(t).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, lines.Length);
            Assert.Equal("| x/y z | ", lines[2]);
        }

        [Fact]
        public void Text_writer_follows_genuine_FM_text_export_rules()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "vendor", "fixtures", "scouting-text-sample.rtf");
            if (!File.Exists(path))
                return; // fixture not available on this machine
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB, "FM writes no BOM");
            var text = Encoding.UTF8.GetString(bytes);
            var lines = text.Split("\r\n");
            Assert.Equal("", lines[^1]); // ends with CRLF
            var content = lines[..^1];
            for (var i = 1; i < content.Length; i += 2)
                Assert.Matches("^\\| -+\\| $", content[i]); // dash line after every row, header included

            static string[] Cells(string line) => line.Substring(2, line.Length - 5).Split(" | ");
            var headers = Cells(content[0]).Select(s => s.TrimEnd()).ToArray();
            var rows = new List<IReadOnlyList<string>>();
            for (var i = 2; i < content.Length; i += 2)
                rows.Add(Cells(content[i]).Select(s => s.TrimEnd()).ToArray());
            Assert.All(rows, r => Assert.Equal(headers.Length, r.Count));

            var ours = Fm24TextWriter.Render(new ExportTable("", headers, rows)).Split("\r\n")[..^1];
            Assert.Equal(content.Length, ours.Length);
            for (var i = 0; i < content.Length; i += 2)
                Assert.Equal(Cells(content[i]).Select(s => s.TrimEnd()), Cells(ours[i]).Select(s => s.TrimEnd()));
        }

        [Fact]
        public void Csv_quotes_only_when_needed_and_has_bom()
        {
            var t = new ExportTable("", new[] { "Name", "Wage" },
                new[] { new[] { "O'Neil, Jr", "£1,200 p/w" }, new[] { "Say \"hi\"", "1 227" } });
            Assert.Equal("Name,Wage\r\n\"O'Neil, Jr\",\"£1,200 p/w\"\r\n\"Say \"\"hi\"\"\",1 227\r\n",
                CsvWriter.Render(t));
            Assert.Equal("Name;Wage\r\nO'Neil, Jr;£1,200 p/w\r\n\"Say \"\"hi\"\"\";1 227\r\n",
                CsvWriter.Render(t, ';'));
            var file = Path.GetTempFileName();
            try
            {
                CsvWriter.Write(t, file);
                var b = File.ReadAllBytes(file);
                Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, b.Take(3).ToArray());
            }
            finally { File.Delete(file); }
        }

        [Fact]
        public void Empty_selection_column_is_dropped()
        {
            var t = new ExportTable("", new[] { "", "Name", "" },
                new[] { new[] { "", "A", "x" }, new[] { "", "B", "" } });
            var d = t.WithoutEmptyColumns();
            Assert.Equal(new[] { "Name", "" }, d.Headers);
            Assert.Equal("x", d.Cell(0, 1));
        }
    }
}
